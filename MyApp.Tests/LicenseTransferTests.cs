using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyApp.Data;
using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Host;
using ServiceStack.OrmLite;
using ServiceStack.Testing;
namespace MyApp.Tests;
[NonParallelizable]
[Category("Database")]
public class LicenseTransferTests
{
    [Test]
    public async Task Only_verified_recipient_can_accept_and_orders_stay_with_original_buyer()
    {
        var factory=DatabaseTestRun.CreateFactory();
        using var db=factory.OpenDbConnection();
        new Migration1000 { Db=db }.Up();
        using var host=new BasicAppHost { ConfigureContainer=c=>c.Register<IDbConnectionFactory>(factory) }.Init();
        var id=Guid.NewGuid(); var orderId=Guid.NewGuid();
        db.Insert(new SoftwareLicense { Id=id,UserId="buyer",ShortKeyHash="hash",LicenseeName="Original Buyer",Status=LicenseStatus.Active });
        db.Insert(new LicenseOrder { Id=orderId,OrderNumber="order",UserId="buyer" });
        using var users=new Users();
        var request=new BasicRequest(); var session=new AuthUserSession { UserAuthId="buyer",IsAuthenticated=true };
        request.Items[Keywords.Session]=session;
        using var service=new LicenseTransferServices(users) { Request=request };
        var transfer=(LicenseTransfer)await service.Post(new TransferLicense { Id=id,RecipientEmail="recipient@example.invalid" });
        Assert.ThrowsAsync<HttpError>(()=>service.Post(new AcceptLicenseTransfer { Id=transfer.Id }));
        session.UserAuthId="recipient";
        await service.Post(new AcceptLicenseTransfer { Id=transfer.Id });
        Assert.That(db.SingleById<SoftwareLicense>(id).UserId,Is.EqualTo("recipient"));
        Assert.That(db.SingleById<SoftwareLicense>(id).LicenseeName,Is.EqualTo("Original Buyer"));
        Assert.That(db.SingleById<LicenseOrder>(orderId).UserId,Is.EqualTo("buyer"));
        Assert.ThrowsAsync<HttpError>(()=>service.Post(new AcceptLicenseTransfer { Id=transfer.Id }));
        session.UserAuthId="buyer";
        Assert.Throws<HttpError>(()=>service.Post(new CancelLicenseTransfer { Id=transfer.Id }));
    }
    private sealed class Users() : UserManager<ApplicationUser>(new Store(),Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),new PasswordHasher<ApplicationUser>(),[],[],new UpperInvariantLookupNormalizer(),new IdentityErrorDescriber(),null!,NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public override Task<ApplicationUser?> FindByEmailAsync(string email)=>Task.FromResult<ApplicationUser?>(new ApplicationUser { Id="recipient",Email=email,EmailConfirmed=true });
        public override Task<ApplicationUser?> FindByIdAsync(string id)=>Task.FromResult<ApplicationUser?>(new ApplicationUser { Id=id,EmailConfirmed=true });
    }
    private sealed class Store : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user,CancellationToken token)=>Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user,CancellationToken token)=>Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user,string? name,CancellationToken token)=>Task.CompletedTask;
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user,CancellationToken token)=>Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user,string? name,CancellationToken token)=>Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(ApplicationUser user,CancellationToken token)=>throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user,CancellationToken token)=>throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user,CancellationToken token)=>throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string id,CancellationToken token)=>throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string name,CancellationToken token)=>throw new NotSupportedException();
    }
}
