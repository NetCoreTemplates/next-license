using System.Data;
using Microsoft.AspNetCore.Identity;
using MyApp.Data;
using MyApp.Licensing.Core;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
using Stripe;
using Stripe.Checkout;
using LicenseOrder = MyApp.ServiceModel.LicenseOrder;

namespace MyApp.ServiceInterface;

public sealed class LicenseStripeConfig
{
    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }
    public bool LiveMode { get; set; }
    public bool AllowPromotionCodes { get; set; }
    public bool AutomaticTax { get; set; }
    public string BaseUrl { get; set; } = "https://localhost:5001";
}

public interface ILicenseCheckoutReader
{
    Task<Session> Session(string id);
    Task<PaymentIntent> Payment(string id);
}
public interface ILicenseRefundGateway
{
    Task<Refund> CreateRefund(string paymentIntent, long amount, Guid requestId, Guid orderId);
    Task<Refund> Refund(string id);
}

public sealed class LicenseStripeGateway(LicenseStripeConfig config) : ILicenseCheckoutReader, ILicenseRefundGateway
{
    private StripeClient Client => new(config.SecretKey ?? throw new InvalidOperationException("Stripe is not configured."));
    public async Task<Session> CreateCheckout(LicenseOrder order, OrderLine line, string email)
    {
        var price = await new PriceService(Client).GetAsync(line.StripePriceId);
        if (!price.Active || price.Type != "one_time" || price.UnitAmount != line.UnitAmountCents || price.Currency != order.Currency
            || price.Livemode != config.LiveMode || (config.AutomaticTax && price.TaxBehavior != "exclusive")) throw new InvalidOperationException("Stripe Price does not match the local price book.");
        var metadata = new Dictionary<string, string> { ["orderId"] = order.Id.ToString("D"), ["userId"] = order.UserId, ["sku"] = line.Sku };
        return await new SessionService(Client).CreateAsync(new SessionCreateOptions {
            AllowPromotionCodes = config.AllowPromotionCodes, AutomaticTax = new SessionAutomaticTaxOptions { Enabled = config.AutomaticTax },
            Mode = "payment", CustomerEmail = email, CustomerCreation = "always", ClientReferenceId = order.UserId,
            LineItems = [new SessionLineItemOptions { Price = line.StripePriceId, Quantity = line.Quantity }],
            InvoiceCreation = new SessionInvoiceCreationOptions { Enabled = true }, Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata },
            SuccessUrl = config.BaseUrl.TrimEnd('/') + "/account?checkout=complete",
            CancelUrl = config.BaseUrl.TrimEnd('/') + "/pricing?checkout=cancelled",
        }, new RequestOptions { IdempotencyKey = "license-order-" + order.Id });
    }
    public Event Validate(string payload, string signature)
    {
        if (payload.Length > 1_000_000) throw HttpError.BadRequest("Webhook too large.");
        var evt = EventUtility.ConstructEvent(payload, signature, config.WebhookSecret
            ?? throw new InvalidOperationException("Stripe webhook secret is not configured."));
        if (evt.Livemode != config.LiveMode) throw HttpError.BadRequest("Stripe mode mismatch.");
        return evt;
    }
    public Task<Session> Session(string id) => new SessionService(Client).GetAsync(id, new SessionGetOptions { Expand = ["line_items", "discounts.promotion_code"] });
    public Task<PaymentIntent> Payment(string id) => new PaymentIntentService(Client).GetAsync(id, new PaymentIntentGetOptions { Expand = ["latest_charge"] });
    public Task<Refund> Refund(string id) => new RefundService(Client).GetAsync(id);
    public Task<Charge> Charge(string id) => new ChargeService(Client).GetAsync(id);
    public Task<Dispute> Dispute(string id) => new DisputeService(Client).GetAsync(id);
    public Task<Refund> CreateRefund(string paymentIntent, long amount, Guid requestId, Guid orderId) => new RefundService(Client).CreateAsync(
        new RefundCreateOptions { PaymentIntent = paymentIntent, Amount = amount, Reason = "requested_by_customer",
            Metadata = new() { ["orderId"] = orderId.ToString("D"), ["requestId"] = requestId.ToString("D") } },
        new RequestOptions { IdempotencyKey = "license-refund-" + requestId.ToString("D") });
    public Task<Invoice> Invoice(string id) => new InvoiceService(Client).GetAsync(id);
}

public class LicenseCheckoutServices(LicenseStripeGateway stripe, UserManager<ApplicationUser> users, LicensingConfig licensing, LicenseStripeConfig stripeConfig, LicenseSigning signing) : ServiceStack.Service
{
    public async Task<object> Post(CreateLicenseCheckout request)
    {
        try { signing.ValidateLicenseIssuance(); }
        catch (Exception) { throw HttpError.ServiceUnavailable("License delivery is not configured. Please contact the store before purchasing."); }
        var userId = GetSession().UserAuthId;
        var user = await users.FindByIdAsync(userId) ?? throw HttpError.Unauthorized("Account required.");
        if (!user.EmailConfirmed) throw HttpError.Forbidden("Verify your email before purchasing.");
        if (request.Seats is < 1 or > 10000 || string.IsNullOrWhiteSpace(request.LicenseeName) || request.LicenseeName.Length > 200
            || request.LicenseeOrganization?.Length > 200) throw HttpError.BadRequest("Invalid licensee or seats.");
        var price = Db.Single<PriceBook>(x => x.Sku == request.Sku && x.IsActive) ?? throw HttpError.BadRequest("Price unavailable.");
        var kind = price.Sku switch {
            "pro-12m-new" or "pro-lifetime-new" => OrderKind.NewPurchase,
            "pro-12m-renewal" => OrderKind.Renewal,
            "pro-lifetime-upgrade" => OrderKind.LifetimeUpgrade,
            "pro-edition-upgrade" => OrderKind.EditionUpgrade,
            _ => throw HttpError.BadRequest("Unsupported SKU."),
        };
        SoftwareLicense? existing = null;
        if (kind != OrderKind.NewPurchase)
        {
            existing = Db.Single<SoftwareLicense>(x => x.Id == request.LicenseId && x.UserId == userId)
                ?? throw HttpError.NotFound("License not found.");
            if (existing.Status != LicenseStatus.Active) throw HttpError.Conflict("License is revoked.");
            if (kind is OrderKind.Renewal or OrderKind.LifetimeUpgrade && existing.UpdateMode == UpdateMode.Lifetime)
                throw HttpError.Conflict("This license already includes lifetime updates.");
            if (kind == OrderKind.EditionUpgrade && price.Edition <= existing.Edition)
                throw HttpError.Conflict("The new edition must rank above your current edition.");
            if (kind == OrderKind.Renewal && !RenewalPolicy.DiscountEligible(DateTime.SpecifyKind(existing.UpdatesThroughUtc!.Value, DateTimeKind.Utc), DateTime.UtcNow, licensing.RenewalDiscountGraceDays))
                price = Db.Single<PriceBook>(x => x.Sku == "pro-12m-new" && x.IsActive) ?? throw HttpError.BadRequest("Renewal pricing is unavailable.");
            request.Seats = existing.Seats; request.LicenseeName = existing.LicenseeName; request.LicenseeOrganization = existing.LicenseeOrganization;
        }
        else if (request.LicenseId != null) throw HttpError.BadRequest("New purchases cannot target an existing license.");
        if (price.UnitAmountCents < 1 || string.IsNullOrWhiteSpace(price.StripePriceId)
            || (kind is OrderKind.NewPurchase or OrderKind.Renewal && price.UpdateMode == UpdateMode.ThroughDate && price.TermMonths != 12)
            || (kind == OrderKind.LifetimeUpgrade && price.UpdateMode != UpdateMode.Lifetime)) throw HttpError.BadRequest("Invalid price configuration.");
        var now = DateTime.UtcNow;
        var agreement = Db.Single(Db.From<LicenseAgreement>().Where(x => x.EffectiveAtUtc <= now).OrderByDescending(x => x.EffectiveAtUtc));
        if (!request.AcceptAgreement || agreement == null || agreement.Version != request.AgreementVersion)
            throw HttpError.BadRequest("Accept the current license agreement before checkout.");
        var order = new LicenseOrder { Id = Guid.NewGuid(), Kind = kind, LicenseId = existing?.Id, OrderNumber = "ACME-" + Guid.NewGuid().ToString("N"), UserId = userId,
            Seats = request.Seats, LicenseeName = request.LicenseeName, LicenseeOrganization = request.LicenseeOrganization,
            Currency = price.Currency, ExpectedAmountCents = checked(price.UnitAmountCents * request.Seats),
            AgreementVersion = agreement.Version, AgreementAcceptedAtUtc = now, CreatedDate = now, ModifiedDate = now, CreatedBy = userId, ModifiedBy = userId };
        var line = new OrderLine { OrderId = order.Id, Sku = price.Sku, Description = "Acme Studio Pro — " + (price.UpdateMode == UpdateMode.Lifetime ? "lifetime feature updates" : "12 months of feature updates"),
            Edition = price.Edition, UpdateMode = price.UpdateMode, TermMonths = price.TermMonths, Quantity = request.Seats,
            UnitAmountCents = price.UnitAmountCents, StripePriceId = price.StripePriceId, CreatedDate = now, ModifiedDate = now, CreatedBy = userId, ModifiedBy = userId };
        using (var tx = Db.OpenTransaction())
        {
            Db.Insert(order); Db.Insert(line);
            Db.Insert(new LicenseCheckoutPolicy { OrderId=order.Id, AllowPromotionCodes=stripeConfig.AllowPromotionCodes, AutomaticTax=stripeConfig.AutomaticTax });
            Db.Insert(new LicenseAgreementAcceptance { OrderId = order.Id, UserId = userId, AgreementVersion = agreement.Version,
                AcceptedAtUtc = now, IpAddress = Request!.RemoteIp });
            tx.Commit();
        }
        var session = await stripe.CreateCheckout(order, line, user.Email!);
        Db.UpdateOnly(() => new LicenseOrder { StripeCheckoutSessionId = session.Id }, x => x.Id == order.Id);
        return new LicenseCheckoutResponse { OrderId = order.Id, Url = session.Url };
    }
    public async Task<object> Get(GetOrderInvoice request)
    {
        var user = GetSession().UserAuthId;
        var order = Db.Single<LicenseOrder>(x => x.Id == request.Id && x.UserId == user) ?? throw HttpError.NotFound("Order not found.");
        if (order.StripeInvoiceId == null) throw HttpError.NotFound("Invoice is not yet available.");
        var invoice = await stripe.Invoice(order.StripeInvoiceId);
        return new OrderInvoiceResponse { Url = invoice.HostedInvoiceUrl };
    }
}
