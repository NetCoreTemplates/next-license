using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack.OrmLite;
namespace MyApp.Tests;
[Category("Database")]
public class LicenseRefundTests
{
    [Test]
    public void Multiple_partial_refunds_are_distinct_idempotent_and_advisory()
    {
        using var db = DatabaseTestRun.CreateFactory().OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var id = Guid.NewGuid();
        db.Insert(new LicenseOrder { Id = id, OrderNumber = "test", StripePaymentIntentId = "pi", Status = OrderStatus.Paid, FinalAmountCents = 100 });
        var fulfillment = new LicenseFulfillment(new LicenseSigning(new()), new());
        fulfillment.ApplyRefund(db, "re_1", "pi", 40, "succeeded", null, DateTime.UtcNow);
        fulfillment.ApplyRefund(db, "re_1", "pi", 40, "succeeded", null, DateTime.UtcNow);
        Assert.That(db.SingleById<LicenseOrder>(id).Status, Is.EqualTo(OrderStatus.PartiallyRefunded));
        fulfillment.ApplyRefund(db, "re_2", "pi", 60, "pending", null, DateTime.UtcNow);
        Assert.That(db.SingleById<LicenseOrder>(id).Status, Is.EqualTo(OrderStatus.PartiallyRefunded));
        fulfillment.ApplyRefund(db, "re_2", "pi", 60, "succeeded", null, DateTime.UtcNow);
        Assert.That(db.Count<LicenseRefund>(), Is.EqualTo(2));
        Assert.That(db.SingleById<LicenseOrder>(id).Status, Is.EqualTo(OrderStatus.Refunded));
        Assert.That(db.SingleById<LicenseOrder>(id).RequiresReview, Is.True);
    }
    [Test]
    public void Dispute_replay_keeps_review_but_provider_status_change_reopens_it()
    {
        using var db = DatabaseTestRun.CreateFactory().OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var id = Guid.NewGuid(); var now = DateTime.UtcNow;
        db.Insert(new LicenseOrder { Id = id, OrderNumber = "disputed", StripePaymentIntentId = "pi", Currency = "usd", Status = OrderStatus.Paid, FinalAmountCents = 100 });
        var fulfillment = new LicenseFulfillment(new LicenseSigning(new()), new());
        fulfillment.ApplyDispute(db, "dp", "pi", 100, "usd", "needs_response", "fraudulent", now);
        db.UpdateOnly(() => new LicenseDispute { Reviewed = true, ReviewedBy = "admin" }, x => x.StripeDisputeId == "dp");
        fulfillment.ApplyDispute(db, "dp", "pi", 100, "usd", "needs_response", "fraudulent", now);
        Assert.That(db.SingleById<LicenseDispute>("dp").Reviewed, Is.True);
        fulfillment.ApplyDispute(db, "dp", "pi", 100, "usd", "lost", "fraudulent", now);
        Assert.That(db.Count<LicenseDispute>(), Is.EqualTo(1));
        Assert.That(db.SingleById<LicenseDispute>("dp").Reviewed, Is.False);
        Assert.That(db.SingleById<LicenseOrder>(id).Status, Is.EqualTo(OrderStatus.Paid));
        Assert.That(db.Count<LicensingAuditEvent>(), Is.EqualTo(2));
        Assert.Throws<InvalidOperationException>(() => fulfillment.ApplyDispute(db, "dp", "pi", 90, "usd", "won", "fraudulent", now));
    }

}
