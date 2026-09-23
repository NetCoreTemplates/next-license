using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyApp.Data;
using System.Text.RegularExpressions;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
using Stripe;
namespace MyApp.ServiceInterface;
public class SoftwareSetupServices(LicenseStripeConfig stripe, LicensingConfig licensing, ApplicationDbContext identity, IMemoryCache? cache = null) : ServiceStack.Service {
    public async Task<object> Get(SearchLicenseCustomers request) {
        var query = identity.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Query)) query = query.Where(x => (x.Email != null && x.Email.Contains(request.Query)) || (x.DisplayName != null && x.DisplayName.Contains(request.Query)));
        return new LicenseCustomersResponse { Results = await query.OrderBy(x => x.Email).Skip(Math.Max(0, request.Skip)).Take(50)
            .Select(x => new LicenseCustomer { Id = x.Id, Email = x.Email ?? "", Name = x.DisplayName ?? x.UserName ?? "" }).ToListAsync() };
    }
    public object Get(GetSoftwareSetup request) => new SoftwareSetupResponse {
        StripeConfigured = !string.IsNullOrWhiteSpace(stripe.SecretKey), WebhookConfigured = !string.IsNullOrWhiteSpace(stripe.WebhookSecret),
        SigningConfigured = !string.IsNullOrWhiteSpace(licensing.LicensePrivateKeyPem), LiveMode = stripe.LiveMode, Repository = licensing.GitHubRepository,
    };
    public object Post(SetSoftwarePrice request) {
        if (request.UnitAmountCents is < 1 or > 100000000 || !Regex.IsMatch(request.Currency, "^[a-z]{3}$")) throw HttpError.BadRequest("Enter a positive price and three-letter currency.");
        var row = Db.SingleById<PriceBook>(request.Sku) ?? throw HttpError.NotFound("Plan not found.");
        if (row.UnitAmountCents != request.UnitAmountCents || row.Currency != request.Currency) {
            row.UnitAmountCents = request.UnitAmountCents; row.Currency = request.Currency; row.StripePriceId = ""; row.IsActive = false;
            row.ModifiedDate = DateTime.UtcNow; row.ModifiedBy = GetSession().UserAuthId; Db.Update(row);
        }
        return row;
    }
    public async Task<object> Post(CreateMissingStripe request) {
        if (string.IsNullOrWhiteSpace(stripe.SecretKey)) throw HttpError.BadRequest("Configure Stripe__SecretKey in .env and restart the server first.");
        var keyIsLive = stripe.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal) || stripe.SecretKey.StartsWith("rk_live_", StringComparison.Ordinal);
        if (keyIsLive != stripe.LiveMode) throw HttpError.BadRequest("Stripe key and Stripe__LiveMode must use the same mode.");
        var service = new PriceService(new StripeClient(stripe.SecretKey));
        var prices = Db.Select<PriceBook>();
        if (!prices.Any(x => x.UnitAmountCents > 0)) throw HttpError.BadRequest("Set and save a paid plan amount before creating Stripe prices.");
        foreach (var row in prices) {
            if (!string.IsNullOrEmpty(row.StripePriceId)) continue;
            if (row.UnitAmountCents <= 0) continue;
            var key = $"software-{row.Sku}-{row.Currency}-{row.UnitAmountCents}";
            var existing = await service.ListAsync(new PriceListOptions { LookupKeys = [key], Limit = 1 });
            var price = existing.Data.FirstOrDefault() ?? await service.CreateAsync(new PriceCreateOptions {
                Currency = row.Currency, UnitAmount = row.UnitAmountCents, LookupKey = key, TaxBehavior = "exclusive",
                ProductData = new PriceProductDataOptions { Name = row.Sku.Replace('-', ' ') }, Metadata = new() { ["sku"] = row.Sku },
            }, new RequestOptions { IdempotencyKey = key });
            Validate(price, row);
            Db.UpdateOnly(() => new PriceBook { StripePriceId = price.Id, IsActive = false, ModifiedDate = DateTime.UtcNow }, x => x.Sku == row.Sku && x.StripePriceId == "" && x.UnitAmountCents == row.UnitAmountCents && x.Currency == row.Currency);
        }
        return new LicensePricingResponse { Results = Db.Select<PriceBook>() };
    }
    public async Task<object> Post(ApproveSoftwarePrice request) {
        var row = Db.SingleById<PriceBook>(request.Sku) ?? throw HttpError.NotFound("Plan not found.");
        if (request.Approved) {
            if (string.IsNullOrWhiteSpace(stripe.SecretKey) || string.IsNullOrWhiteSpace(row.StripePriceId)) throw HttpError.BadRequest("Create the Stripe price first.");
            Validate(await new PriceService(new StripeClient(stripe.SecretKey)).GetAsync(row.StripePriceId), row);
        }
        row.IsActive = request.Approved; row.ModifiedDate = DateTime.UtcNow; row.ModifiedBy = GetSession().UserAuthId;
        using var transaction = Db.OpenTransaction();
        if (Db.UpdateOnly(() => new PriceBook { IsActive = request.Approved, ModifiedDate = row.ModifiedDate, ModifiedBy = row.ModifiedBy },
                x => x.Sku == row.Sku && x.StripePriceId == row.StripePriceId && x.UnitAmountCents == row.UnitAmountCents && x.Currency == row.Currency) != 1)
            throw HttpError.Conflict("The plan changed while it was being verified. Reload and review the latest price.");
        Db.Insert(new LicensingAuditEvent { Action = request.Approved ? "ApprovePrice" : "HidePrice", Subject = row.Sku, Actor = GetSession().UserAuthId, OccurredAtUtc = DateTime.UtcNow });
        transaction.Commit();
        return row;
    }
    private void Validate(Price price, PriceBook row) {
        if (!price.Active || price.Type != "one_time" || price.Currency != row.Currency || price.UnitAmount != row.UnitAmountCents || price.Livemode != stripe.LiveMode || (stripe.AutomaticTax && price.TaxBehavior != "exclusive"))
            throw HttpError.BadRequest("Stripe price does not match this plan or configured Stripe mode.");
    }
    public Task<GitHubDownloadsResponse> Get(GetGitHubDownloads request) => GitHubDownloads.Get(licensing.GitHubRepository, cache);
}
