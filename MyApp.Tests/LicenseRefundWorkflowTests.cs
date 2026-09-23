using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Host;
using ServiceStack.OrmLite;
using ServiceStack.Testing;
using Stripe;
namespace MyApp.Tests;

[NonParallelizable]
[Category("Database")]
public class LicenseRefundWorkflowTests
{
    [Test]
    public async Task Refund_retries_use_saved_identity_and_old_uncertain_requests_are_blocked()
    {
        var factory = DatabaseTestRun.CreateFactory();
        using var db = factory.OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        using var host = new BasicAppHost { ConfigureContainer = c => c.Register<IDbConnectionFactory>(factory) }.Init();
        var context = new BasicRequest();
        context.Items[Keywords.Session] = new AuthUserSession { UserAuthId = "admin", IsAuthenticated = true, Roles = ["Admin"] };
        var id = Guid.NewGuid();
        db.Insert(new LicenseOrder { Id = id, OrderNumber = "refund", StripePaymentIntentId = "pi", Status = OrderStatus.Paid, FinalAmountCents = 100 });
        var stripe = new RefundGateway();
        using var service = new LicenseRefundServices(stripe, new LicenseFulfillment(new LicenseSigning(new()), new())) { Request = context };
        var request = new RefundLicenseOrder { Id = id, RequestId = Guid.NewGuid(), AmountCents = 40, Reason = "Customer request" };
        await service.Post(request); await service.Post(request);
        Assert.That(stripe.Creates, Is.EqualTo(1));
        Assert.That(db.Count<LicenseRefund>(), Is.EqualTo(1));
        request.AmountCents = 50;
        Assert.ThrowsAsync<HttpError>(() => service.Post(request));
        var uncertain = Guid.NewGuid();
        db.Insert(new LicenseRefundRequest { Id = uncertain, OrderId = id, AmountCents = 40, Reason = "Uncertain", CreatedAtUtc = DateTime.UtcNow.AddDays(-2) });
        Assert.ThrowsAsync<HttpError>(() => service.Post(new RefundLicenseOrder { Id = id, RequestId = uncertain, AmountCents = 40, Reason = "Uncertain" }));
        Assert.That(stripe.Creates, Is.EqualTo(1));
    }
    private sealed class RefundGateway : ILicenseRefundGateway
    {
        public int Creates { get; private set; }
        private readonly Refund refund = new() { Id = "re_test", Amount = 40, Status = "succeeded", Created = DateTime.UtcNow };
        public Task<Refund> CreateRefund(string paymentIntent, long amount, Guid requestId, Guid orderId) { Creates++; return Task.FromResult(refund); }
        public Task<Refund> Refund(string id) => Task.FromResult(refund);
    }
}
