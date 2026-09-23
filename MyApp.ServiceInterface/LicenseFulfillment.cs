using System.Data;
using MyApp.Licensing.Core;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Jobs;
using ServiceStack.OrmLite;
using Stripe.Checkout;

namespace MyApp.ServiceInterface;

public class LicenseWebhookServices(LicenseStripeGateway stripe, IBackgroundJobs jobs) : Service
{
    public async Task<object> Post(LicenseStripeWebhook request)
    {
        var payload = await Request!.GetRawBodyAsync() ?? "";
        var evt = stripe.Validate(payload, Request!.Headers["Stripe-Signature"] ?? "");
        using (var tx = Db.OpenTransaction())
        {
            if (!Db.Exists<StripeEventInbox>(x => x.Id == evt.Id))
                Db.Insert(new StripeEventInbox { Id = evt.Id, Type = evt.Type, Payload = payload, ReceivedAtUtc = DateTime.UtcNow });
            tx.Commit();
        }
        // Replay also requeues an existing pending row, repairing a crash between insert and enqueue.
        jobs.EnqueueCommand<ProcessLicenseStripeEventCommand>(new ProcessLicenseStripeEvent { Id = evt.Id });
        return new EmptyResponse();
    }
}
public class ProcessLicenseStripeEvent { public string Id { get; set; } = ""; }
public class ProcessLicenseStripeEventCommand(IDbConnectionFactory factory, LicenseStripeGateway stripe, LicenseFulfillment fulfillment, LicenseCheckoutRecovery recovery)
    : AsyncCommand<ProcessLicenseStripeEvent>
{
    protected override async Task RunAsync(ProcessLicenseStripeEvent request, CancellationToken token)
    {
        using var db = factory.OpenDbConnection();
        var inbox = db.SingleById<StripeEventInbox>(request.Id) ?? throw new InvalidOperationException("Inbox event missing.");
        if (inbox.ProcessedAtUtc != null) return;
        try
        {
            using var payload = System.Text.Json.JsonDocument.Parse(inbox.Payload);
            var data = payload.RootElement.GetProperty("data").GetProperty("object");
            if (inbox.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded" or "checkout.session.async_payment_failed")
            {
                // Retrieve current provider state so out-of-order failure events cannot undo successful payment.
                var session = await stripe.Session(data.GetProperty("id").GetString()!);
                if (session.PaymentStatus is "paid" or "no_payment_required")
                {
                    await recovery.Settle(db, session);
                }
                else if (inbox.Type == "checkout.session.async_payment_failed" && session.Metadata.TryGetValue("orderId", out var value) && Guid.TryParse(value, out var id))
                    db.UpdateOnly(() => new LicenseOrder { Status = OrderStatus.Failed }, x => x.Id == id && x.Status == OrderStatus.Pending && x.StripeCheckoutSessionId == session.Id);
            }
            else if (inbox.Type is "refund.created" or "refund.updated" or "refund.failed")
            {
                var refund = await stripe.Refund(data.GetProperty("id").GetString()!);
                var charge = await stripe.Charge(refund.ChargeId);
                fulfillment.ApplyRefund(db, refund.Id, charge.PaymentIntentId, refund.Amount, refund.Status, refund.Reason,
                    DateTime.SpecifyKind(refund.Created, DateTimeKind.Utc));
                if (refund.Metadata.TryGetValue("requestId", out var requestId) && Guid.TryParse(requestId, out var refundRequestId))
                {
                    var attempt = db.SingleById<LicenseRefundRequest>(refundRequestId);
                    var order = attempt == null ? null : db.SingleById<LicenseOrder>(attempt.OrderId);
                    if (attempt != null && attempt.AmountCents == refund.Amount && order?.StripePaymentIntentId == charge.PaymentIntentId)
                        db.UpdateOnly(() => new LicenseRefundRequest { StripeRefundId = refund.Id }, x => x.Id == attempt.Id);
                }
            }
            else if (inbox.Type.StartsWith("charge.dispute.", StringComparison.Ordinal))
            {
                var dispute = await stripe.Dispute(data.GetProperty("id").GetString()!);
                var charge = await stripe.Charge(dispute.ChargeId);
                fulfillment.ApplyDispute(db, dispute.Id, charge.PaymentIntentId, dispute.Amount, dispute.Currency,
                    dispute.Status, dispute.Reason, DateTime.SpecifyKind(dispute.Created, DateTimeKind.Utc));
            }
            db.UpdateOnly(() => new StripeEventInbox { ProcessedAtUtc = DateTime.UtcNow, LastError = null, Attempts = inbox.Attempts + 1 }, x => x.Id == inbox.Id);
        }
        catch
        {
            // Persist no raw provider exception or credential-bearing payload in application logs.
            db.UpdateOnly(() => new StripeEventInbox { LastError = "Processing failed; retry or review the provider event.", Attempts = inbox.Attempts + 1 }, x => x.Id == inbox.Id);
            throw;
        }
    }
}

public sealed class LicenseFulfillment(LicenseSigning signing, LicenseStripeConfig config)
{
    public void ApplyDispute(IDbConnection db, string id, string paymentIntent, long amount, string currency,
        string status, string reason, DateTime createdUtc)
    {
        Utc.Require(createdUtc);
        if (string.IsNullOrWhiteSpace(id) || amount < 1) throw new InvalidOperationException("Invalid dispute.");
        using var tx = db.OpenTransaction();
        var order = db.Single<LicenseOrder>(x => x.StripePaymentIntentId == paymentIntent)
            ?? throw new InvalidOperationException("Disputed order is not fulfilled yet; retry.");
        if (order.Currency != currency) throw new InvalidOperationException("Dispute currency mismatch.");
        var row = db.SingleById<LicenseDispute>(id);
        if (row != null && (row.OrderId != order.Id || row.AmountCents != amount || row.Currency != currency))
            throw new InvalidOperationException("Dispute identity changed.");
        if (row?.Status == status) return;
        row ??= new LicenseDispute { StripeDisputeId = id, OrderId = order.Id, AmountCents = amount, Currency = currency, CreatedAtUtc = createdUtc };
        row.Status = status; row.Reason = reason; row.UpdatedAtUtc = DateTime.UtcNow;
        row.Reviewed = false; row.ReviewedBy = null; row.ReviewNotes = null;
        db.Save(row);
        db.UpdateOnly(() => new LicenseOrder { RequiresReview = true }, x => x.Id == order.Id);
        db.Insert(new LicensingAuditEvent { Actor = "stripe", Action = "DisputeUpdated", Subject = order.Id.ToString(),
            OccurredAtUtc = DateTime.UtcNow, Detail = System.Text.Json.JsonSerializer.Serialize(new { id, status, amount }) });
        tx.Commit();
    }
    public void ApplyRefund(IDbConnection db, string refundId, string paymentIntentId, long amount, string status, string? reason, DateTime createdUtc)
    {
        if (amount < 0 || string.IsNullOrWhiteSpace(refundId) || string.IsNullOrWhiteSpace(paymentIntentId)) throw new InvalidOperationException("Invalid refund.");
        using var tx = db.OpenTransaction();
        var order = db.Single<LicenseOrder>(x => x.StripePaymentIntentId == paymentIntentId)
            ?? throw new InvalidOperationException("Refunded order is not fulfilled yet; retry.");
        var prior = db.SingleById<LicenseRefund>(refundId);
        if (prior != null && (prior.OrderId != order.Id || prior.AmountCents != amount)) throw new InvalidOperationException("Refund identity changed.");
        db.Save(new LicenseRefund { StripeRefundId = refundId, OrderId = order.Id, AmountCents = amount, Status = status, Reason = reason, CreatedAtUtc = createdUtc });
        var refunded = db.Select<LicenseRefund>(x => x.OrderId == order.Id && x.Status == "succeeded").Sum(x => x.AmountCents);
        db.UpdateOnly(() => new LicenseOrder { RequiresReview = true,
            Status = refunded >= order.FinalAmountCents ? OrderStatus.Refunded : refunded > 0 ? OrderStatus.PartiallyRefunded : OrderStatus.Paid }, x => x.Id == order.Id);
        // Revocation is an explicit operator decision; previously delivered blobs remain usable offline.
        tx.Commit();
    }
    public void ApplyPaid(IDbConnection db, Session session, DateTime paidAtUtc)
    {
        Utc.Require(paidAtUtc);
        var complimentary = session.PaymentStatus == "no_payment_required" && session.Status == "complete" && session.AmountTotal == 0 && string.IsNullOrEmpty(session.PaymentIntentId);
        if (session.Mode != "payment" || (session.PaymentStatus != "paid" && !complimentary) || session.Livemode != config.LiveMode
            || !session.Metadata.TryGetValue("orderId", out var orderId) || !Guid.TryParse(orderId, out var id)
            || (!complimentary && string.IsNullOrWhiteSpace(session.PaymentIntentId))) throw new InvalidOperationException("Invalid payment evidence.");
        using var tx = db.OpenTransaction();
        var order = db.SingleById<LicenseOrder>(id) ?? throw new InvalidOperationException("Pending order missing.");
        var policy = db.SingleById<LicenseCheckoutPolicy>(id) ?? throw new InvalidOperationException("Checkout policy snapshot missing.");
        var line = db.Single<OrderLine>(x => x.OrderId == id) ?? throw new InvalidOperationException("Order snapshot missing.");
        if (order.StripeCheckoutSessionId != session.Id || order.UserId != session.ClientReferenceId
            || session.Metadata.GetValueOrDefault("userId") != order.UserId || session.Metadata.GetValueOrDefault("sku") != line.Sku
            || session.Currency != order.Currency
            || session.LineItems?.Data.Count != 1 || session.LineItems.Data[0].Price.Id != line.StripePriceId
            || session.LineItems.Data[0].Quantity != line.Quantity || !db.Exists<LicenseAgreementAcceptance>(x => x.OrderId == id))
            throw new InvalidOperationException("Payment does not match the accepted order snapshot.");
        var subtotal = session.AmountSubtotal ?? order.ExpectedAmountCents;
        var discount = session.TotalDetails?.AmountDiscount ?? 0;
        var tax = session.TotalDetails?.AmountTax ?? 0;
        var total = session.AmountTotal ?? -1;
        if (subtotal != order.ExpectedAmountCents || subtotal != checked(line.UnitAmountCents * line.Quantity)
            || discount < 0 || discount > subtotal || tax < 0 || total != checked(subtotal-discount+tax)
            || (session.TotalDetails?.AmountShipping ?? 0) != 0 || (!policy.AllowPromotionCodes && discount != 0)
            || (!policy.AutomaticTax && tax != 0) || (policy.AutomaticTax && session.AutomaticTax?.Status != "complete")
            || ((policy.AutomaticTax || policy.AllowPromotionCodes) && (session.AmountSubtotal == null || session.TotalDetails == null))
            || (complimentary && (!policy.AllowPromotionCodes || discount != subtotal)))
            throw new InvalidOperationException("Checkout totals do not match the accepted price and tax/discount policy.");
        var promotionId = session.Discounts?.SingleOrDefault()?.PromotionCodeId;
        if (order.Status is OrderStatus.Paid or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
        {
            var recorded = db.SingleById<LicenseOrderSettlement>(id)
                ?? throw new InvalidOperationException("Settlement evidence missing.");
            if (order.StripePaymentIntentId != session.PaymentIntentId || order.FinalAmountCents != total
                || recorded.SubtotalCents != subtotal || recorded.DiscountCents != discount || recorded.TaxCents != tax
                || recorded.TotalCents != total || recorded.StripePromotionCodeId != promotionId)
                throw new InvalidOperationException("Payment evidence changed.");
            return;
        }
        // Insert only. The order primary key arbitrates concurrent fulfillment; a loser rolls back.
        db.Insert(new LicenseOrderSettlement { OrderId=id, SubtotalCents=subtotal, DiscountCents=discount, TaxCents=tax,
            TotalCents=total, StripePromotionCodeId=promotionId });
        var now = DateTime.UtcNow;
        SoftwareLicense license;
        string key;
        var previousCutoff = (DateTime?)null;
        if (order.Kind == OrderKind.NewPurchase)
        {
            key = ShortKey.Create();
            license = new SoftwareLicense { Id = Guid.NewGuid(), ShortKeyHash = signing.HashShortKey(key), ShortKeySuffix = key[^4..],
                UserId = order.UserId, OrderId = id, Edition = line.Edition, UpdateMode = line.UpdateMode,
                UpdatesThroughUtc = line.UpdateMode == UpdateMode.ThroughDate ? paidAtUtc.AddMonths(line.TermMonths) : null,
                LicenseeName = order.LicenseeName, LicenseeOrganization = order.LicenseeOrganization, Seats = order.Seats,
                IssuedAtUtc = now, BlobVersion = 1, CreatedDate = now, ModifiedDate = now, CreatedBy = "stripe", ModifiedBy = "stripe" };
        }
        else
        {
            license = db.SingleById<SoftwareLicense>(order.LicenseId) ?? throw new InvalidOperationException("Target license missing.");
            if (license.UserId != order.UserId || license.Status != LicenseStatus.Active || license.Seats != order.Seats)
                throw new InvalidOperationException("Target license changed; operator review required.");
            previousCutoff = license.UpdatesThroughUtc;
            var oldBlob = db.Single<LicenseBlob>(x => x.LicenseId == license.Id && x.Version == license.BlobVersion)
                ?? throw new InvalidOperationException("Previous license blob missing.");
            key = signing.ReadLicense(oldBlob.Blob).ShortKey;
            if (signing.HashShortKey(key) != license.ShortKeyHash) throw new InvalidOperationException("Stored key mismatch.");
            switch (order.Kind)
            {
                case OrderKind.Renewal:
                    if (license.UpdateMode != UpdateMode.ThroughDate) throw new InvalidOperationException("Lifetime cannot become dated.");
                    license.UpdatesThroughUtc = RenewalPolicy.Extend(DateTime.SpecifyKind(license.UpdatesThroughUtc!.Value, DateTimeKind.Utc), paidAtUtc, line.TermMonths);
                    break;
                case OrderKind.LifetimeUpgrade:
                    if (license.UpdateMode != UpdateMode.ThroughDate) throw new InvalidOperationException("Already lifetime; duplicate purchase requires review.");
                    license.UpdateMode = UpdateMode.Lifetime; license.UpdatesThroughUtc = null;
                    break;
                case OrderKind.EditionUpgrade:
                    if (line.Edition <= license.Edition) throw new InvalidOperationException("Already upgraded; duplicate purchase requires review.");
                    license.Edition = line.Edition;
                    break;
                default: throw new InvalidOperationException("Unsupported order kind.");
            }
            license.BlobVersion++; license.ModifiedDate = now; license.ModifiedBy = "stripe";
        }
        if (license.UpdatesThroughUtc.HasValue) license.UpdatesThroughUtc = DateTime.SpecifyKind(license.UpdatesThroughUtc.Value, DateTimeKind.Utc);
        _ = new PaidEntitlement(license.ProductId, license.Edition, license.UpdateMode, license.UpdatesThroughUtc);
        var signed = signing.SignLicense(db, license, key, now);
        license.SigningKeyId = signed.KeyId;
        if (order.Kind == OrderKind.NewPurchase) db.Insert(license); else db.Update(license);
        db.Insert(new LicenseBlob { LicenseId = license.Id, Version = license.BlobVersion, Blob = signed.Blob,
            CreatedDate = now, ModifiedDate = now, CreatedBy = "stripe", ModifiedBy = "stripe" });
        db.UpdateOnly(() => new LicenseOrder { LicenseId = license.Id, Status = OrderStatus.Paid, PaidAtUtc = paidAtUtc, RequiresReview = false,
            StripePaymentIntentId = session.PaymentIntentId, StripeInvoiceId = session.InvoiceId, FinalAmountCents = session.AmountTotal,
            ModifiedDate = now, ModifiedBy = "stripe" }, x => x.Id == order.Id);
        db.Insert(new LicensingAuditEvent { Action = "FulfillOrder", Actor = "stripe", Subject = order.Id.ToString(), OccurredAtUtc = now,
            Detail = System.Text.Json.JsonSerializer.Serialize(new { order.Kind, previousCutoff, license.UpdatesThroughUtc, paidAtUtc }) });
        LicenseNotifications.Queue(db, "order:" + order.Id, license, "Delivery", "Your Acme Studio license is ready",
            "Your payment is confirmed. Sign in to My licenses to download your current license file and open your invoice.");
        tx.Commit();
    }
}
