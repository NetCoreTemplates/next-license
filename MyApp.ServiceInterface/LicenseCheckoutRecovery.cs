using System.Data;
using MyApp.ServiceModel;
using ServiceStack.OrmLite;
using Stripe.Checkout;

namespace MyApp.ServiceInterface;

/// <summary>Both webhook delivery and reconciliation use fresh provider evidence.</summary>
public sealed class LicenseCheckoutRecovery(ILicenseCheckoutReader stripe, LicenseFulfillment fulfillment)
{
    public async Task<bool> Settle(IDbConnection db, Session session)
    {
        if (session.PaymentStatus == "no_payment_required" && session.Status == "complete" && session.AmountTotal == 0)
        {
            // Fully discounted purchases have no paid charge; entitlement starts when completion is observed.
            fulfillment.ApplyPaid(db, session, DateTime.UtcNow); return true;
        }
        if (session.PaymentStatus != "paid") return false;
        var payment = await stripe.Payment(session.PaymentIntentId);
        if (payment.Id != session.PaymentIntentId || payment.Status != "succeeded"
            || payment.LatestCharge is not { Paid: true } charge || charge.PaymentIntentId != payment.Id
            || payment.Livemode != session.Livemode || payment.Currency != session.Currency
            || payment.AmountReceived != session.AmountTotal)
            throw new InvalidOperationException("Successful payment evidence does not match Checkout.");
        fulfillment.ApplyPaid(db, session, DateTime.SpecifyKind(charge.Created, DateTimeKind.Utc));
        return true;
    }

    public async Task Reconcile(IDbConnection db, Guid orderId)
    {
        var order = db.SingleById<LicenseOrder>(orderId);
        if (order?.Status != OrderStatus.Pending || order.StripeCheckoutSessionId == null) return;
        var session = await stripe.Session(order.StripeCheckoutSessionId);
        if (session.Id != order.StripeCheckoutSessionId
            || !session.Metadata.TryGetValue("orderId", out var id) || id != order.Id.ToString("D"))
            throw new InvalidOperationException("Checkout identity mismatch.");
        if (await Settle(db, session)) return;
        // An expired, unpaid session is terminal. Completed delayed payments remain pending.
        if (session.Status == "expired" && session.PaymentStatus == "unpaid")
            db.UpdateOnly(() => new LicenseOrder { Status = OrderStatus.Failed, ModifiedDate = DateTime.UtcNow,
                ModifiedBy = "reconciliation" }, x => x.Id == order.Id && x.Status == OrderStatus.Pending);
    }
}
