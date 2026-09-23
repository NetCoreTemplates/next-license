/* Options:
Date: 2026-09-22 20:02:54
Version: 10.21
Tip: To override a DTO option, remove "//" prefix before updating
BaseUrl: https://localhost:5001

//GlobalNamespace: 
MakePropertiesOptional: True
//AddServiceStackTypes: True
//AddResponseStatus: False
//AddImplicitVersion: 
//AddDescriptionAsComments: True
//IncludeTypes: 
//ExcludeTypes: 
//DefaultImports: 
*/

// @ts-nocheck

export interface IReturn<T>
{
    createResponse(): T;
}

export interface IReturnVoid
{
    createResponse(): void;
}

export interface IHasSessionId
{
    sessionId?: string;
}

export interface IHasBearerToken
{
    bearerToken?: string;
}

export interface IPost
{
}

export interface IGet
{
}

export enum Edition
{
    Free = 0,
    Pro = 10,
    Enterprise = 20,
}

export enum UpdateMode
{
    ThroughDate = 'ThroughDate',
    Lifetime = 'Lifetime',
}

// @DataContract
export class QueryBase
{
    // @DataMember(Order=1)
    public skip?: number;

    // @DataMember(Order=2)
    public take?: number;

    // @DataMember(Order=3)
    public orderBy?: string;

    // @DataMember(Order=4)
    public orderByDesc?: string;

    // @DataMember(Order=5)
    public include?: string;

    // @DataMember(Order=6)
    public fields?: string;

    // @DataMember(Order=7)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<QueryBase>) { (Object as any).assign(this, init); }
}

export class QueryDb<T> extends QueryBase
{

    public constructor(init?: Partial<QueryDb<T>>) { super(init); (Object as any).assign(this, init); }
}

export class User
{
    public id?: string;
    public userName?: string;
    public firstName?: string;
    public lastName?: string;
    public displayName?: string;
    public profileUrl?: string;

    public constructor(init?: Partial<User>) { (Object as any).assign(this, init); }
}

// @DataContract
export class ResponseError
{
    // @DataMember(Order=1)
    public errorCode?: string;

    // @DataMember(Order=2)
    public fieldName?: string;

    // @DataMember(Order=3)
    public message?: string;

    // @DataMember(Order=4)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<ResponseError>) { (Object as any).assign(this, init); }
}

// @DataContract
export class ResponseStatus
{
    // @DataMember(Order=1)
    public errorCode?: string;

    // @DataMember(Order=2)
    public message?: string;

    // @DataMember(Order=3)
    public stackTrace?: string;

    // @DataMember(Order=4)
    public errors?: ResponseError[];

    // @DataMember(Order=5)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<ResponseStatus>) { (Object as any).assign(this, init); }
}

export enum OrderStatus
{
    Pending = 'Pending',
    Paid = 'Paid',
    Failed = 'Failed',
    PartiallyRefunded = 'PartiallyRefunded',
    Refunded = 'Refunded',
}

// @DataContract
export class AuditBase
{
    // @DataMember(Order=1)
    public createdDate?: string;

    // @DataMember(Order=2)
    // @Required()
    public createdBy?: string;

    // @DataMember(Order=3)
    public modifiedDate?: string;

    // @DataMember(Order=4)
    // @Required()
    public modifiedBy?: string;

    // @DataMember(Order=5)
    public deletedDate?: string;

    // @DataMember(Order=6)
    public deletedBy?: string;

    public constructor(init?: Partial<AuditBase>) { (Object as any).assign(this, init); }
}

export enum LicenseStatus
{
    Active = 'Active',
    Revoked = 'Revoked',
}

export class SoftwareLicense extends AuditBase
{
    public id?: string;
    public shortKeySuffix?: string;
    public productId?: string;
    public edition?: Edition;
    public updateMode?: UpdateMode;
    public updatesThroughUtc?: string;
    public licenseeName?: string;
    public licenseeOrganization?: string;
    public seats?: number;
    public issuedAtUtc?: string;
    public status?: LicenseStatus;
    public userId?: string;
    public orderId?: string;
    public revokedAtUtc?: string;
    // @StringLength(2147483647)
    public revokedReason?: string;

    public signingKeyId?: string;
    public blobVersion?: number;

    public constructor(init?: Partial<SoftwareLicense>) { super(init); (Object as any).assign(this, init); }
}

export enum OrderKind
{
    NewPurchase = 'NewPurchase',
    Renewal = 'Renewal',
    LifetimeUpgrade = 'LifetimeUpgrade',
    EditionUpgrade = 'EditionUpgrade',
}

export class LicenseOrder extends AuditBase
{
    public id?: string;
    public orderNumber?: string;
    public userId?: string;
    public kind?: OrderKind;
    public licenseId?: string;
    public seats?: number;
    public licenseeName?: string;
    public licenseeOrganization?: string;
    public currency?: string;
    public expectedAmountCents?: number;
    public finalAmountCents?: number;
    public agreementVersion?: string;
    public agreementAcceptedAtUtc?: string;
    // @StringLength(255)
    public stripeCheckoutSessionId?: string;

    // @StringLength(255)
    public stripePaymentIntentId?: string;

    public stripeInvoiceId?: string;
    public status?: OrderStatus;
    public paidAtUtc?: string;
    public requiresReview?: boolean;

    public constructor(init?: Partial<LicenseOrder>) { super(init); (Object as any).assign(this, init); }
}

export class LicenseDispute
{
    public stripeDisputeId?: string;
    public orderId?: string;
    public amountCents?: number;
    public currency?: string;
    public status?: string;
    // @StringLength(2147483647)
    public reason?: string;

    public createdAtUtc?: string;
    public updatedAtUtc?: string;
    public reviewed?: boolean;
    public reviewedBy?: string;
    // @StringLength(2147483647)
    public reviewNotes?: string;

    public constructor(init?: Partial<LicenseDispute>) { (Object as any).assign(this, init); }
}

export class LicenseRefund
{
    public stripeRefundId?: string;
    public orderId?: string;
    public amountCents?: number;
    public status?: string;
    // @StringLength(2147483647)
    public reason?: string;

    public createdAtUtc?: string;

    public constructor(init?: Partial<LicenseRefund>) { (Object as any).assign(this, init); }
}

export class LicenseCustomer
{
    public id?: string;
    public name?: string;
    public email?: string;

    public constructor(init?: Partial<LicenseCustomer>) { (Object as any).assign(this, init); }
}

export class GitHubDownloadAsset
{
    public name?: string;
    public url?: string;
    public size?: number;

    public constructor(init?: Partial<GitHubDownloadAsset>) { (Object as any).assign(this, init); }
}

export class GitHubDownloadRelease
{
    public name?: string;
    public tag?: string;
    public url?: string;
    public notes?: string;
    public publishedAt?: string;
    public prerelease?: boolean;
    public assets?: GitHubDownloadAsset[] = [];

    public constructor(init?: Partial<GitHubDownloadRelease>) { (Object as any).assign(this, init); }
}

// @DataContract
export class QueryResponse<T>
{
    // @DataMember(Order=1)
    public offset?: number;

    // @DataMember(Order=2)
    public total?: number;

    // @DataMember(Order=3)
    public results?: T[] = [];

    // @DataMember(Order=4)
    public meta?: { [index:string]: string; };

    // @DataMember(Order=5)
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<QueryResponse<T>>) { (Object as any).assign(this, init); }
}

export class LicenseBlobResponse
{
    public blob?: string;
    public status?: string;
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicenseBlobResponse>) { (Object as any).assign(this, init); }
}

export class CustomerOrder
{
    public id?: string;
    public productName?: string;
    public description?: string;
    public amountCents?: number;
    public currency?: string;
    public seats?: number;
    public status?: OrderStatus;
    public requiresReview?: boolean;
    public hasInvoice?: boolean;
    public createdAtUtc?: string;
    public message?: string;

    public constructor(init?: Partial<CustomerOrder>) { (Object as any).assign(this, init); }
}

export class CustomerOrdersResponse
{
    public results?: CustomerOrder[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<CustomerOrdersResponse>) { (Object as any).assign(this, init); }
}

// @DataContract
export class EmptyResponse
{
    // @DataMember(Order=1)
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<EmptyResponse>) { (Object as any).assign(this, init); }
}

export class LicenseNotificationPreferences
{
    public userId?: string;
    public updateReminders?: boolean;
    public releaseAnnouncements?: boolean;
    public winBack?: boolean;

    public constructor(init?: Partial<LicenseNotificationPreferences>) { (Object as any).assign(this, init); }
}

export class PriceBook extends AuditBase
{
    public sku?: string;
    public edition?: Edition;
    public updateMode?: UpdateMode;
    public termMonths?: number;
    public currency?: string;
    public unitAmountCents?: number;
    public stripePriceId?: string;
    public isActive?: boolean;

    public constructor(init?: Partial<PriceBook>) { super(init); (Object as any).assign(this, init); }
}

export class LicenseAgreement extends AuditBase
{
    public version?: string;
    public effectiveAtUtc?: string;
    // @StringLength(2147483647)
    public bodyMarkdown?: string;

    public constructor(init?: Partial<LicenseAgreement>) { super(init); (Object as any).assign(this, init); }
}

export class LicensingDashboardResponse
{
    public activeLicenses?: number;
    public ordersRequiringReview?: number;
    public pendingStripeEvents?: number;
    public prices?: PriceBook[] = [];
    public agreements?: LicenseAgreement[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicensingDashboardResponse>) { (Object as any).assign(this, init); }
}

export class AccountLicensesResponse
{
    public results?: SoftwareLicense[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<AccountLicensesResponse>) { (Object as any).assign(this, init); }
}

export class AccountOrdersResponse
{
    public results?: LicenseOrder[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<AccountOrdersResponse>) { (Object as any).assign(this, init); }
}

export class LicenseDisputesResponse
{
    public results?: LicenseDispute[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicenseDisputesResponse>) { (Object as any).assign(this, init); }
}

export class LicenseCheckoutResponse
{
    public orderId?: string;
    public url?: string;
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicenseCheckoutResponse>) { (Object as any).assign(this, init); }
}

export class OrderInvoiceResponse
{
    public url?: string;
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<OrderInvoiceResponse>) { (Object as any).assign(this, init); }
}

export class RefundLicenseOrderResponse
{
    public result?: LicenseRefund;
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<RefundLicenseOrderResponse>) { (Object as any).assign(this, init); }
}

export class LicensePricingResponse
{
    public results?: PriceBook[] = [];
    public agreement?: LicenseAgreement;
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicensePricingResponse>) { (Object as any).assign(this, init); }
}

export class LicenseTransfer
{
    public id?: string;
    public licenseId?: string;
    public fromUserId?: string;
    public toUserId?: string;
    public createdAtUtc?: string;
    public expiresAtUtc?: string;
    public acceptedAtUtc?: string;
    public cancelledAtUtc?: string;

    public constructor(init?: Partial<LicenseTransfer>) { (Object as any).assign(this, init); }
}

export class LicenseTransfersResponse
{
    public results?: LicenseTransfer[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicenseTransfersResponse>) { (Object as any).assign(this, init); }
}

export class HelloResponse
{
    public result?: string;

    public constructor(init?: Partial<HelloResponse>) { (Object as any).assign(this, init); }
}

// @DataContract
export class RegisterResponse implements IHasSessionId, IHasBearerToken
{
    // @DataMember(Order=1)
    public userId?: string;

    // @DataMember(Order=2)
    public sessionId?: string;

    // @DataMember(Order=3)
    public userName?: string;

    // @DataMember(Order=4)
    public referrerUrl?: string;

    // @DataMember(Order=5)
    public bearerToken?: string;

    // @DataMember(Order=6)
    public refreshToken?: string;

    // @DataMember(Order=7)
    public refreshTokenExpiry?: string;

    // @DataMember(Order=8)
    public roles?: string[];

    // @DataMember(Order=9)
    public permissions?: string[];

    // @DataMember(Order=10)
    public redirectUrl?: string;

    // @DataMember(Order=11)
    public responseStatus?: ResponseStatus;

    // @DataMember(Order=12)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<RegisterResponse>) { (Object as any).assign(this, init); }
}

export class LicenseCustomersResponse
{
    public results?: LicenseCustomer[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<LicenseCustomersResponse>) { (Object as any).assign(this, init); }
}

export class SoftwareSetupResponse
{
    public stripeConfigured?: boolean;
    public webhookConfigured?: boolean;
    public signingConfigured?: boolean;
    public liveMode?: boolean;
    public repository?: string;
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<SoftwareSetupResponse>) { (Object as any).assign(this, init); }
}

export class GitHubDownloadsResponse
{
    public repository?: string;
    public results?: GitHubDownloadRelease[] = [];
    public responseStatus?: ResponseStatus;

    public constructor(init?: Partial<GitHubDownloadsResponse>) { (Object as any).assign(this, init); }
}

// @DataContract
export class AuthenticateResponse implements IHasSessionId, IHasBearerToken
{
    // @DataMember(Order=1)
    public userId?: string;

    // @DataMember(Order=2)
    public sessionId?: string;

    // @DataMember(Order=3)
    public userName?: string;

    // @DataMember(Order=4)
    public displayName?: string;

    // @DataMember(Order=5)
    public referrerUrl?: string;

    // @DataMember(Order=6)
    public bearerToken?: string;

    // @DataMember(Order=7)
    public refreshToken?: string;

    // @DataMember(Order=8)
    public refreshTokenExpiry?: string;

    // @DataMember(Order=9)
    public profileUrl?: string;

    // @DataMember(Order=10)
    public roles?: string[];

    // @DataMember(Order=11)
    public permissions?: string[];

    // @DataMember(Order=12)
    public authProvider?: string;

    // @DataMember(Order=13)
    public responseStatus?: ResponseStatus;

    // @DataMember(Order=14)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<AuthenticateResponse>) { (Object as any).assign(this, init); }
}

// @Route("/licensing/activate", "POST")
// @Route("/licensing/refresh", "POST")
export class ActivateLicense implements IReturn<LicenseBlobResponse>, IPost
{
    public key?: string;
    public blob?: string;

    public constructor(init?: Partial<ActivateLicense>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ActivateLicense'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseBlobResponse(); }
}

// @Route("/account/orders", "GET")
// @ValidateRequest(Validator="IsAuthenticated")
export class GetAccountOrders implements IReturn<CustomerOrdersResponse>, IGet
{

    public constructor(init?: Partial<GetAccountOrders>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetAccountOrders'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new CustomerOrdersResponse(); }
}

// @ValidateRequest(Validator="IsAuthenticated")
export class RefreshAccountOrder implements IReturn<CustomerOrder>, IPost
{
    public id?: string;

    public constructor(init?: Partial<RefreshAccountOrder>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'RefreshAccountOrder'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new CustomerOrder(); }
}

// @Route("/stripe/webhook", "POST")
export class LicenseStripeWebhook implements IReturn<EmptyResponse>, IPost
{

    public constructor(init?: Partial<LicenseStripeWebhook>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'LicenseStripeWebhook'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new EmptyResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class ExtendUpdatesThrough implements IReturn<LicenseBlobResponse>, IPost
{
    public id?: string;
    public expectedBlobVersion?: number;
    public updatesThroughUtc?: string;
    public reason?: string;

    public constructor(init?: Partial<ExtendUpdatesThrough>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ExtendUpdatesThrough'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseBlobResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class UpgradeToLifetimeUpdates implements IReturn<LicenseBlobResponse>, IPost
{
    public id?: string;
    public expectedBlobVersion?: number;
    public reason?: string;

    public constructor(init?: Partial<UpgradeToLifetimeUpdates>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'UpgradeToLifetimeUpdates'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseBlobResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class ReissueLicense implements IReturn<LicenseBlobResponse>, IPost
{
    public id?: string;
    public expectedBlobVersion?: number;
    public rotateShortKey?: boolean;
    public reason?: string;

    public constructor(init?: Partial<ReissueLicense>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ReissueLicense'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseBlobResponse(); }
}

// @ValidateRequest(Validator="IsAuthenticated")
export class GetLicenseNotificationPreferences implements IReturn<LicenseNotificationPreferences>, IGet
{

    public constructor(init?: Partial<GetLicenseNotificationPreferences>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetLicenseNotificationPreferences'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicenseNotificationPreferences(); }
}

// @ValidateRequest(Validator="IsAuthenticated")
export class SaveLicenseNotificationPreferences implements IReturnVoid, IPost
{
    public updateReminders?: boolean;
    public releaseAnnouncements?: boolean;
    public winBack?: boolean;

    public constructor(init?: Partial<SaveLicenseNotificationPreferences>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'SaveLicenseNotificationPreferences'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @Route("/account/licenses/{Id}/resend", "POST")
// @ValidateRequest(Validator="IsAuthenticated")
export class ResendLicense implements IReturnVoid, IPost
{
    public id?: string;

    public constructor(init?: Partial<ResendLicense>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ResendLicense'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @ValidateRequest(Validator="IsAdmin")
export class GetLicensingDashboard implements IReturn<LicensingDashboardResponse>, IGet
{

    public constructor(init?: Partial<GetLicensingDashboard>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetLicensingDashboard'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicensingDashboardResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class PublishLicenseAgreement implements IReturn<LicenseAgreement>, IPost
{
    public version?: string;
    public bodyMarkdown?: string;

    public constructor(init?: Partial<PublishLicenseAgreement>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'PublishLicenseAgreement'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseAgreement(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class SearchLicenses implements IReturn<AccountLicensesResponse>, IGet
{
    public query?: string;
    public userId?: string;
    public keySuffix?: string;
    public skip?: number;

    public constructor(init?: Partial<SearchLicenses>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'SearchLicenses'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new AccountLicensesResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class SearchOrders implements IReturn<AccountOrdersResponse>, IGet
{
    public query?: string;
    public userId?: string;
    public requiresReview?: boolean;
    public skip?: number;

    public constructor(init?: Partial<SearchOrders>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'SearchOrders'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new AccountOrdersResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class ListLicenseDisputes implements IReturn<LicenseDisputesResponse>, IGet
{
    public reviewed?: boolean;
    public skip?: number;

    public constructor(init?: Partial<ListLicenseDisputes>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ListLicenseDisputes'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicenseDisputesResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class ReviewLicenseDispute implements IReturnVoid, IPost
{
    public id?: string;
    public notes?: string;

    public constructor(init?: Partial<ReviewLicenseDispute>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ReviewLicenseDispute'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @ValidateRequest(Validator="IsAdmin")
export class RetryLicenseStripeEvent implements IReturnVoid, IPost
{
    public id?: string;

    public constructor(init?: Partial<RetryLicenseStripeEvent>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'RetryLicenseStripeEvent'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @Route("/checkout", "POST")
// @ValidateRequest(Validator="IsAuthenticated")
export class CreateLicenseCheckout implements IReturn<LicenseCheckoutResponse>, IPost
{
    public sku?: string;
    public licenseId?: string;
    public seats?: number;
    public licenseeName?: string;
    public licenseeOrganization?: string;
    public agreementVersion?: string;
    public acceptAgreement?: boolean;

    public constructor(init?: Partial<CreateLicenseCheckout>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'CreateLicenseCheckout'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseCheckoutResponse(); }
}

// @Route("/account/orders/{Id}/invoice", "GET")
// @ValidateRequest(Validator="IsAuthenticated")
export class GetOrderInvoice implements IReturn<OrderInvoiceResponse>, IGet
{
    public id?: string;

    public constructor(init?: Partial<GetOrderInvoice>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetOrderInvoice'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new OrderInvoiceResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class RefundLicenseOrder implements IReturn<RefundLicenseOrderResponse>, IPost
{
    public id?: string;
    public requestId?: string;
    public amountCents?: number;
    // @StringLength(2147483647)
    public reason?: string;

    public constructor(init?: Partial<RefundLicenseOrder>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'RefundLicenseOrder'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new RefundLicenseOrderResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class ReviewLicenseOrder implements IReturnVoid, IPost
{
    public id?: string;
    public notes?: string;

    public constructor(init?: Partial<ReviewLicenseOrder>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ReviewLicenseOrder'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @Route("/licensing/pricing", "GET")
export class GetLicensePricing implements IReturn<LicensePricingResponse>, IGet
{

    public constructor(init?: Partial<GetLicensePricing>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetLicensePricing'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicensePricingResponse(); }
}

// @Route("/account/licenses", "GET")
// @ValidateRequest(Validator="IsAuthenticated")
export class GetAccountLicenses implements IReturn<AccountLicensesResponse>, IGet
{

    public constructor(init?: Partial<GetAccountLicenses>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetAccountLicenses'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new AccountLicensesResponse(); }
}

// @Route("/account/licenses/{Id}/blob", "GET")
// @ValidateRequest(Validator="IsAuthenticated")
export class GetLicenseBlob implements IReturn<LicenseBlobResponse>, IGet
{
    public id?: string;

    public constructor(init?: Partial<GetLicenseBlob>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetLicenseBlob'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicenseBlobResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class IssueLicense implements IReturn<LicenseBlobResponse>, IPost
{
    public userId?: string;
    public licenseeName?: string;
    public licenseeOrganization?: string;
    public seats?: number;
    public edition?: Edition;
    public updateMode?: UpdateMode;
    public updatesThroughUtc?: string;

    public constructor(init?: Partial<IssueLicense>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'IssueLicense'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseBlobResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class RevokeLicense implements IReturnVoid, IPost
{
    public id?: string;
    // @Validate(Validator="NotEmpty")
    public reason?: string;

    public constructor(init?: Partial<RevokeLicense>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'RevokeLicense'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @Route("/account/licenses/{Id}/transfer", "POST")
// @ValidateRequest(Validator="IsAuthenticated")
export class TransferLicense implements IReturn<LicenseTransfer>, IPost
{
    public id?: string;
    public recipientEmail?: string;

    public constructor(init?: Partial<TransferLicense>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'TransferLicense'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicenseTransfer(); }
}

// @ValidateRequest(Validator="IsAuthenticated")
export class ListLicenseTransfers implements IReturn<LicenseTransfersResponse>, IGet
{

    public constructor(init?: Partial<ListLicenseTransfers>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ListLicenseTransfers'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicenseTransfersResponse(); }
}

// @ValidateRequest(Validator="IsAuthenticated")
export class CancelLicenseTransfer implements IReturnVoid, IPost
{
    public id?: string;

    public constructor(init?: Partial<CancelLicenseTransfer>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'CancelLicenseTransfer'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @ValidateRequest(Validator="IsAuthenticated")
export class AcceptLicenseTransfer implements IReturnVoid, IPost
{
    public id?: string;

    public constructor(init?: Partial<AcceptLicenseTransfer>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'AcceptLicenseTransfer'; }
    public getMethod() { return 'POST'; }
    public createResponse() {}
}

// @Route("/hello/{Name}")
export class Hello implements IReturn<HelloResponse>, IGet
{
    public name?: string;

    public constructor(init?: Partial<Hello>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'Hello'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new HelloResponse(); }
}

/** @description Sign Up */
// @Api(Description="Sign Up")
// @DataContract
export class Register implements IReturn<RegisterResponse>, IPost
{
    // @DataMember(Order=1)
    public userName?: string;

    // @DataMember(Order=2)
    public firstName?: string;

    // @DataMember(Order=3)
    public lastName?: string;

    // @DataMember(Order=4)
    public displayName?: string;

    // @DataMember(Order=5)
    public email?: string;

    // @DataMember(Order=6)
    public password?: string;

    // @DataMember(Order=7)
    public confirmPassword?: string;

    // @DataMember(Order=8)
    public autoLogin?: boolean;

    // @DataMember(Order=10)
    public errorView?: string;

    // @DataMember(Order=11)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<Register>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'Register'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new RegisterResponse(); }
}

// @Route("/confirm-email")
export class ConfirmEmail implements IReturnVoid, IGet
{
    public userId?: string;
    public code?: string;
    public returnUrl?: string;

    public constructor(init?: Partial<ConfirmEmail>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ConfirmEmail'; }
    public getMethod() { return 'GET'; }
    public createResponse() {}
}

// @ValidateRequest(Validator="IsAdmin")
export class SearchLicenseCustomers implements IReturn<LicenseCustomersResponse>, IGet
{
    public query?: string;
    public skip?: number;

    public constructor(init?: Partial<SearchLicenseCustomers>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'SearchLicenseCustomers'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new LicenseCustomersResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class GetSoftwareSetup implements IReturn<SoftwareSetupResponse>, IGet
{

    public constructor(init?: Partial<GetSoftwareSetup>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetSoftwareSetup'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new SoftwareSetupResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class SetSoftwarePrice implements IReturn<PriceBook>, IPost
{
    public sku?: string;
    public unitAmountCents?: number;
    public currency?: string;

    public constructor(init?: Partial<SetSoftwarePrice>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'SetSoftwarePrice'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new PriceBook(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class CreateMissingStripe implements IReturn<LicensePricingResponse>, IPost
{

    public constructor(init?: Partial<CreateMissingStripe>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'CreateMissingStripe'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new LicensePricingResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class ApproveSoftwarePrice implements IReturn<PriceBook>, IPost
{
    public sku?: string;
    public approved?: boolean;

    public constructor(init?: Partial<ApproveSoftwarePrice>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'ApproveSoftwarePrice'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new PriceBook(); }
}

export class GetGitHubDownloads implements IReturn<GitHubDownloadsResponse>, IGet
{

    public constructor(init?: Partial<GetGitHubDownloads>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'GetGitHubDownloads'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new GitHubDownloadsResponse(); }
}

/** @description Sign In */
// @Route("/auth", "GET,POST")
// @Route("/auth/{provider}", "POST")
// @Api(Description="Sign In")
// @DataContract
export class Authenticate implements IReturn<AuthenticateResponse>, IPost
{
    /** @description AuthProvider, e.g. credentials */
    // @DataMember(Order=1)
    public provider?: string;

    // @DataMember(Order=2)
    public userName?: string;

    // @DataMember(Order=3)
    public password?: string;

    // @DataMember(Order=4)
    public rememberMe?: boolean;

    // @DataMember(Order=5)
    public accessToken?: string;

    // @DataMember(Order=6)
    public accessTokenSecret?: string;

    // @DataMember(Order=7)
    public returnUrl?: string;

    // @DataMember(Order=8)
    public errorView?: string;

    // @DataMember(Order=9)
    public meta?: { [index:string]: string; };

    public constructor(init?: Partial<Authenticate>) { (Object as any).assign(this, init); }
    public getTypeName() { return 'Authenticate'; }
    public getMethod() { return 'POST'; }
    public createResponse() { return new AuthenticateResponse(); }
}

// @ValidateRequest(Validator="IsAdmin")
export class QueryUsers extends QueryDb<User> implements IReturn<QueryResponse<User>>
{
    public id?: string;

    public constructor(init?: Partial<QueryUsers>) { super(init); (Object as any).assign(this, init); }
    public getTypeName() { return 'QueryUsers'; }
    public getMethod() { return 'GET'; }
    public createResponse() { return new QueryResponse<User>(); }
}

