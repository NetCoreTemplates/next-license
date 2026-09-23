using System.Security.Cryptography;
using MyApp.Licensing;
using MyApp.Licensing.Core;
using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack.OrmLite;
using Stripe;
using Stripe.Checkout;

namespace MyApp.Tests;
[Category("Database")]
public class LicensePaymentTests
{
    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public async Task Paid_replay_issues_exactly_one_license_and_rejects_amount_mismatch(bool lifetime, bool discounted, bool complimentary)
    {
        using var db = DatabaseTestRun.CreateFactory().OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        using var leaf = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTime.UtcNow;
        var order = new LicenseOrder { Id = Guid.NewGuid(), OrderNumber = Guid.NewGuid().ToString(), UserId = "user", Seats = 1,
            ExpectedAmountCents = 9900, StripeCheckoutSessionId = "cs_test", LicenseeName = "Buyer", Currency = "usd" };
        db.Insert(order);
        var expectedTotal = complimentary ? 0 : discounted ? 8800 : 9900;
        db.Insert(new LicenseCheckoutPolicy { OrderId=order.Id, AllowPromotionCodes=discounted, AutomaticTax=discounted && !complimentary });
        db.Insert(new OrderLine { OrderId = order.Id, Sku = "sku", StripePriceId = "price", Quantity = 1, UnitAmountCents = 9900,
            Edition = Edition.Pro, UpdateMode = lifetime ? UpdateMode.Lifetime : UpdateMode.ThroughDate, TermMonths = lifetime ? 0 : 12 });
        db.Insert(new LicenseAgreementAcceptance { OrderId = order.Id, UserId = "user", AcceptedAtUtc = now });
        var session = new Session { Id = "cs_test", Mode = "payment", PaymentStatus = "paid", PaymentIntentId = "pi_test",
            Currency = "usd", AmountTotal = 9901, ClientReferenceId = "user",
            Metadata = new() { ["orderId"] = order.Id.ToString(), ["userId"] = "user", ["sku"] = "sku" },
            LineItems = new StripeList<LineItem> { Data = [new LineItem { Price = new Price { Id = "price" }, Quantity = 1 }] } };
        var fulfillment = new LicenseFulfillment(new LicenseSigning(new LicensingConfig {
            LicensePrivateKeyPem = leaf.ExportPkcs8PrivateKeyPem(),
            ShortKeySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }), new LicenseStripeConfig());
        if (discounted) {
            session.AmountSubtotal=9900; session.TotalDetails=new SessionTotalDetails { AmountDiscount=complimentary?9900:1900, AmountTax=complimentary?0:800 };
            session.AutomaticTax=new SessionAutomaticTax { Status="complete" };
            session.AmountTotal=expectedTotal+1;
        }
        var policySnapshot = db.SingleById<LicenseCheckoutPolicy>(order.Id);
        db.DeleteById<LicenseCheckoutPolicy>(order.Id);
        Assert.That(Assert.Throws<InvalidOperationException>(() => fulfillment.ApplyPaid(db, session, now))!.Message,
            Is.EqualTo("Checkout policy snapshot missing."));
        Assert.That(db.Count<LicenseOrderSettlement>(), Is.Zero);
        db.Insert(policySnapshot);
        Assert.Throws<InvalidOperationException>(() => fulfillment.ApplyPaid(db, session, now));
        Assert.That(db.Count<SoftwareLicense>(), Is.Zero);
        session.AmountTotal = expectedTotal;
        if (complimentary) { session.PaymentStatus="no_payment_required"; session.Status="complete"; session.PaymentIntentId=null; }
        var reader = new CheckoutReader(session, new PaymentIntent { Id = "pi_test", Status = "succeeded", Currency = "usd", AmountReceived = 9900,
            LatestCharge = new Charge { Id = "ch_test", Paid = true, PaymentIntentId = "pi_test", Created = now } });
        var recovery = new LicenseCheckoutRecovery(reader, fulfillment);
        reader.CurrentPayment.AmountReceived = expectedTotal-1;
        if (!complimentary) {
        Assert.ThrowsAsync<InvalidOperationException>(() => recovery.Reconcile(db, order.Id));
        Assert.That(db.Count<SoftwareLicense>(), Is.Zero);
        reader.CurrentPayment.AmountReceived = expectedTotal;
        session.PaymentStatus = "unpaid"; session.Status = "complete";
        await recovery.Reconcile(db, order.Id);
        Assert.That(db.SingleById<LicenseOrder>(order.Id).Status, Is.EqualTo(OrderStatus.Pending));
        session.PaymentStatus = "paid";
        }
        await recovery.Reconcile(db, order.Id);
        await recovery.Reconcile(db, order.Id);
        fulfillment.ApplyPaid(db, session, now);
        Assert.That(db.Count<SoftwareLicense>(), Is.EqualTo(1));
        Assert.That(db.Count<LicenseBlob>(), Is.EqualTo(1));
        var settlement=db.SingleById<LicenseOrderSettlement>(order.Id);
        Assert.That(settlement.TotalCents,Is.EqualTo(expectedTotal));
        Assert.That(settlement.DiscountCents,Is.EqualTo(complimentary?9900:discounted?1900:0));
        Assert.That(db.Count<LicenseOrderSettlement>(), Is.EqualTo(1));
        var originalTax = session.TotalDetails?.AmountTax ?? 0;
        session.TotalDetails ??= new SessionTotalDetails();
        session.TotalDetails.AmountTax = originalTax + 1;
        Assert.Throws<InvalidOperationException>(() => fulfillment.ApplyPaid(db, session, now));
        Assert.That(db.SingleById<LicenseOrderSettlement>(order.Id).TaxCents, Is.EqualTo(originalTax));
        session.TotalDetails.AmountTax = originalTax;
        if (discounted && !complimentary)
        {
            // Still valid arithmetic with the same total: replay must compare the entire snapshot.
            session.TotalDetails.AmountDiscount++;
            session.TotalDetails.AmountTax++;
            Assert.That(Assert.Throws<InvalidOperationException>(() => fulfillment.ApplyPaid(db, session, now))!.Message,
                Is.EqualTo("Payment evidence changed."));
            Assert.That(db.SingleById<LicenseOrderSettlement>(order.Id).TaxCents, Is.EqualTo(originalTax));
            session.TotalDetails.AmountDiscount--;
            session.TotalDetails.AmountTax--;
        }
        var license = db.Single<SoftwareLicense>(x => x.OrderId == order.Id);
        Assert.That(license.UpdateMode, Is.EqualTo(lifetime ? UpdateMode.Lifetime : UpdateMode.ThroughDate));
        if (lifetime) Assert.That(license.UpdatesThroughUtc, Is.Null);
        else Assert.That(license.UpdatesThroughUtc!.Value, Is.EqualTo(now.AddMonths(12)).Within(TimeSpan.FromMilliseconds(1)));
        if (!lifetime)
        {
            var originalId = license.Id;
            var previousCutoff = license.UpdatesThroughUtc!.Value;
            foreach (var kind in new[] { OrderKind.Renewal, OrderKind.EditionUpgrade, OrderKind.LifetimeUpgrade })
            {
                var nextId = Guid.NewGuid();
                db.Insert(new LicenseOrder { Id = nextId, OrderNumber = nextId.ToString(), UserId = "user", Seats = 1,
                    Kind = kind, LicenseId = originalId, ExpectedAmountCents = 9900, StripeCheckoutSessionId = "cs_" + kind,
                    LicenseeName = "Buyer", Currency = "usd" });
                db.Insert(new LicenseCheckoutPolicy { OrderId = nextId });
                db.Insert(new OrderLine { OrderId = nextId, Sku = "sku", StripePriceId = "price", Quantity = 1, UnitAmountCents = 9900,
                    Edition = kind == OrderKind.EditionUpgrade ? Edition.Enterprise : Edition.Pro,
                    UpdateMode = kind == OrderKind.LifetimeUpgrade ? UpdateMode.Lifetime : UpdateMode.ThroughDate, TermMonths = 12 });
                db.Insert(new LicenseAgreementAcceptance { OrderId = nextId, UserId = "user", AcceptedAtUtc = now });
                session.Id = "cs_" + kind; session.PaymentIntentId = "pi_" + kind; session.Metadata["orderId"] = nextId.ToString();
                fulfillment.ApplyPaid(db, session, now); fulfillment.ApplyPaid(db, session, now);
                license = db.SingleById<SoftwareLicense>(originalId);
                Assert.That(db.Count<SoftwareLicense>(), Is.EqualTo(1));
                if (kind == OrderKind.Renewal)
                {
                    Assert.That(license.UpdatesThroughUtc, Is.EqualTo(previousCutoff.AddMonths(12)).Within(TimeSpan.FromMilliseconds(1)));
                    previousCutoff = license.UpdatesThroughUtc!.Value;
                }
                if (kind == OrderKind.EditionUpgrade) Assert.That(license.UpdatesThroughUtc, Is.EqualTo(previousCutoff));
                if (kind == OrderKind.LifetimeUpgrade)
                {
                    Assert.That(license.UpdateMode, Is.EqualTo(UpdateMode.Lifetime));
                    Assert.That(license.UpdatesThroughUtc, Is.Null);
                    Assert.That(license.Edition, Is.EqualTo(Edition.Enterprise));
                }
            }
            Assert.That(db.Count<LicenseBlob>(), Is.EqualTo(4));
        }
        Assert.That(LicenseJwt.Read(db.Single<LicenseBlob>(x => x.LicenseId == license.Id && x.Version == license.BlobVersion).Blob, leaf.ExportSubjectPublicKeyInfoPem(), "acme-studio", "acme-studio").Id, Is.EqualTo(license.Id.ToString()));
    }
    private sealed class CheckoutReader(Session session, PaymentIntent payment) : ILicenseCheckoutReader
    {
        public PaymentIntent CurrentPayment => payment;
        public Task<Session> Session(string id) => Task.FromResult(session);
        public Task<PaymentIntent> Payment(string id) => Task.FromResult(payment);
    }

}
