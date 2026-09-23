using System.Data;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;

public class CustomerOrderServices(LicenseCheckoutRecovery recovery) : Service
{
    public object Get(GetAccountOrders request)
    {
        var orders = Db.Select(Db.From<LicenseOrder>().Where(x => x.UserId == GetSession().UserAuthId).OrderByDescending(x => x.CreatedDate));
        var ids = orders.Select(x => x.Id).ToArray();
        var lines = ids.Length == 0 ? [] : Db.Select<OrderLine>(x => Sql.In(x.OrderId, ids));
        return new CustomerOrdersResponse { Results = orders.Select(x => Describe(x, lines.FirstOrDefault(l => l.OrderId == x.Id))).ToList() };
    }

    public async Task<object> Post(RefreshAccountOrder request)
    {
        var order = Db.Single<LicenseOrder>(x => x.Id == request.Id && x.UserId == GetSession().UserAuthId)
            ?? throw HttpError.NotFound("Order not found.");
        if (order.Status == OrderStatus.Pending)
        {
            try { await recovery.Reconcile(Db, order.Id); }
            catch (Exception) {
                // Return an actionable status, never provider payloads or signing details.
                Db.UpdateOnly(() => new LicenseOrder { RequiresReview = true }, x => x.Id == order.Id && x.Status == OrderStatus.Pending);
            }
            order = Db.SingleById<LicenseOrder>(order.Id);
        }
        return Describe(order, Db.Single<OrderLine>(x => x.OrderId == order.Id));
    }

    public static CustomerOrder Describe(LicenseOrder order, OrderLine? line) => new() {
        Id = order.Id, ProductName = line == null ? "Acme Studio license" : "Acme Studio " + line.Edition,
        Description = line == null ? "Software license" : line.UpdateMode == Licensing.Core.UpdateMode.Lifetime ? "Lifetime updates" : $"{line.TermMonths} months of updates",
        AmountCents = order.FinalAmountCents ?? order.ExpectedAmountCents, Currency = order.Currency, Seats = order.Seats,
        Status = order.Status, RequiresReview = order.RequiresReview, HasInvoice = order.StripeInvoiceId != null,
        CreatedAtUtc = order.CreatedDate,
        Message = order.Status == OrderStatus.Pending
            ? order.RequiresReview ? "License delivery needs attention. Check payment status to retry; if this continues, contact support."
                : "Waiting for payment confirmation. If you have paid, check payment status to retrieve your license."
            : order.Status == OrderStatus.Paid ? "Payment confirmed. Your license is ready above."
            : order.Status == OrderStatus.Failed ? "Checkout was not completed. No license was issued."
            : order.Status == OrderStatus.Refunded ? "This payment has been refunded." : "This payment has been partially refunded.",
    };
}
