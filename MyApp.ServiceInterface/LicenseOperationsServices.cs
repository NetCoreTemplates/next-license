using System.Text.RegularExpressions;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.Jobs;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;

public class LicenseOperationsServices(IBackgroundJobs jobs) : Service
{
    public object Get(GetLicensingDashboard request) => new LicensingDashboardResponse {
        ActiveLicenses = Db.Count<SoftwareLicense>(x => x.Status == LicenseStatus.Active),
        OrdersRequiringReview = Db.Count<LicenseOrder>(x => x.RequiresReview),
        PendingStripeEvents = Db.Count<StripeEventInbox>(x => x.ProcessedAtUtc == null),
        Prices = Db.Select<PriceBook>(), Agreements = Db.Select(Db.From<LicenseAgreement>().OrderByDescending(x => x.EffectiveAtUtc).Limit(10)),
    };
    public object Post(PublishLicenseAgreement request)
    {
        if (!Regex.IsMatch(request.Version, "^[A-Za-z0-9.-]{1,60}$") || string.IsNullOrWhiteSpace(request.BodyMarkdown) || request.BodyMarkdown.Length > 100000)
            throw HttpError.BadRequest("Supply a version and agreement text.");
        using var tx = Db.OpenTransaction();
        if (Db.Exists<LicenseAgreement>(x => x.Version == request.Version)) throw HttpError.Conflict("Published agreement versions are immutable.");
        var now = DateTime.UtcNow;
        var agreement = new LicenseAgreement { Version = request.Version, BodyMarkdown = request.BodyMarkdown, EffectiveAtUtc = now,
            CreatedDate = now, ModifiedDate = now, CreatedBy = GetSession().UserAuthId, ModifiedBy = GetSession().UserAuthId };
        Db.Insert(agreement); Audit("PublishLicenseAgreement", agreement.Version); tx.Commit(); return agreement;
    }
    public object Get(SearchLicenses request)
    {
        var query = Db.From<SoftwareLicense>();
        if (!string.IsNullOrWhiteSpace(request.Query)) query.Where(x => x.LicenseeName.Contains(request.Query) || (x.LicenseeOrganization != null && x.LicenseeOrganization.Contains(request.Query)));
        if (!string.IsNullOrWhiteSpace(request.UserId)) query.Where(x => x.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.KeySuffix)) query.And(x => x.ShortKeySuffix == request.KeySuffix);
        return new AccountLicensesResponse { Results = Db.Select(query.OrderByDescending(x => x.CreatedDate).Limit(Math.Max(0, request.Skip), 50)) };
    }
    public object Get(SearchOrders request)
    {
        var query = Db.From<LicenseOrder>();
        if (!string.IsNullOrWhiteSpace(request.Query)) query.Where(x => x.LicenseeName.Contains(request.Query) || x.OrderNumber.Contains(request.Query));
        if (!string.IsNullOrWhiteSpace(request.UserId)) query.Where(x => x.UserId == request.UserId);
        if (request.RequiresReview.HasValue) query.And(x => x.RequiresReview == request.RequiresReview.Value);
        return new AccountOrdersResponse { Results = Db.Select(query.OrderByDescending(x => x.CreatedDate).Limit(Math.Max(0, request.Skip), 50)) };
    }
    public object Get(ListLicenseDisputes request)
    {
        var query = Db.From<LicenseDispute>();
        if (request.Reviewed.HasValue) query.Where(x => x.Reviewed == request.Reviewed.Value);
        return new LicenseDisputesResponse { Results = Db.Select(query.OrderByDescending(x => x.UpdatedAtUtc).Limit(Math.Max(0, request.Skip), 50)) };
    }
    public void Post(ReviewLicenseDispute request)
    {
        if (string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 2000) throw HttpError.BadRequest("Review notes are required.");
        using var tx = Db.OpenTransaction();
        if (Db.UpdateOnly(() => new LicenseDispute { Reviewed = true, ReviewedBy = GetSession().UserAuthId, ReviewNotes = request.Notes },
            x => x.StripeDisputeId == request.Id) != 1) throw HttpError.NotFound("Dispute not found.");
        Audit("ReviewLicenseDispute", request.Id); tx.Commit();
    }
    public void Post(RetryLicenseStripeEvent request)
    {
        var row = Db.SingleById<StripeEventInbox>(request.Id) ?? throw HttpError.NotFound("Event not found.");
        if (row.ProcessedAtUtc != null) throw HttpError.Conflict("Event is already processed.");
        jobs.EnqueueCommand<ProcessLicenseStripeEventCommand>(new ProcessLicenseStripeEvent { Id = row.Id });
        Audit("RetryStripeEvent", row.Id);
    }
    private void Audit(string action, string subject) => Db.Insert(new LicensingAuditEvent {
        Action = action, Subject = subject, Actor = GetSession().UserAuthId, OccurredAtUtc = DateTime.UtcNow,
    });
}
