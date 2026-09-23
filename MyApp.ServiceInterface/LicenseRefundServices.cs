using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;

public class LicenseRefundServices(ILicenseRefundGateway stripe, LicenseFulfillment fulfillment) : Service
{
    public async Task<object> Post(RefundLicenseOrder request)
    {
        if (request.RequestId == Guid.Empty || request.AmountCents < 1 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
            throw HttpError.BadRequest("A unique request ID, positive amount, and support reason are required.");
        var order = Db.SingleById<LicenseOrder>(request.Id) ?? throw HttpError.NotFound("Order not found.");
        if (string.IsNullOrEmpty(order.StripePaymentIntentId) || order.FinalAmountCents == null || request.AmountCents > order.FinalAmountCents
            || order.Status is OrderStatus.Pending or OrderStatus.Failed)
            throw HttpError.BadRequest("Only a paid order can be refunded, up to its paid amount.");
        LicenseRefundRequest attempt;
        using (var tx = Db.OpenTransaction())
        {
            attempt = Db.SingleById<LicenseRefundRequest>(request.RequestId) ?? new LicenseRefundRequest {
                Id = request.RequestId, OrderId = order.Id, AmountCents = request.AmountCents, Reason = request.Reason,
                Actor = GetSession().UserAuthId, CreatedAtUtc = DateTime.UtcNow,
            };
            if (attempt.OrderId != order.Id || attempt.AmountCents != request.AmountCents || attempt.Reason != request.Reason)
                throw HttpError.Conflict("This request ID belongs to a different refund request.");
            Db.Save(attempt); tx.Commit();
        }
        // Stay within Stripe's idempotency window when a previous response is uncertain.
        if (attempt.StripeRefundId == null && attempt.CreatedAtUtc < DateTime.UtcNow.AddHours(-20))
            throw HttpError.Conflict("Unresolved refund request: reconcile it with Stripe before attempting another refund.");
        var refund = attempt.StripeRefundId != null ? await stripe.Refund(attempt.StripeRefundId)
            : await stripe.CreateRefund(order.StripePaymentIntentId, request.AmountCents, request.RequestId, order.Id);
        Db.UpdateOnly(() => new LicenseRefundRequest { StripeRefundId = refund.Id }, x => x.Id == request.RequestId);
        fulfillment.ApplyRefund(Db, refund.Id, order.StripePaymentIntentId, refund.Amount, refund.Status, refund.Reason,
            DateTime.SpecifyKind(refund.Created, DateTimeKind.Utc));
        Db.Insert(new LicensingAuditEvent { Actor = GetSession().UserAuthId, Action = "RefundLicenseOrder", Subject = order.Id.ToString(),
            OccurredAtUtc = DateTime.UtcNow, Detail = System.Text.Json.JsonSerializer.Serialize(new { request.RequestId, request.Reason, refund.Id, refund.Amount }) });
        return new RefundLicenseOrderResponse { Result = Db.SingleById<LicenseRefund>(refund.Id) };
    }
    public void Post(ReviewLicenseOrder request)
    {
        if (string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 2000) throw HttpError.BadRequest("Review notes are required.");
        using var tx = Db.OpenTransaction();
        if (Db.Exists<LicenseDispute>(x => x.OrderId == request.Id && !x.Reviewed)) throw HttpError.Conflict("Review outstanding disputes first.");
        if (Db.UpdateOnly(() => new LicenseOrder { RequiresReview = false, ModifiedDate = DateTime.UtcNow, ModifiedBy = GetSession().UserAuthId },
            x => x.Id == request.Id) != 1) throw HttpError.NotFound("Order not found.");
        Db.Insert(new LicensingAuditEvent { Actor = GetSession().UserAuthId, Action = "ReviewLicenseOrder", Subject = request.Id.ToString(),
            OccurredAtUtc = DateTime.UtcNow, Detail = request.Notes });
        tx.Commit();
    }
}
