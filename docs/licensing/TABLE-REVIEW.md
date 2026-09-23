# Database review — simplified licensing template

The application baseline has **17 tables, down from 27, and no triggers**. Identity remains owned by EF Core: a SQLite migration and current-model bootstrap for server providers. `ApiKey` and `CrudEvent` initialization has also been removed: the deleted release-CI endpoints were the only API-key feature, and the template has no AutoQuery write APIs needing a second audit log.

## Retained application tables

| Table | Purpose and owner | Why retained |
| --- | --- | --- |
| `SoftwareLicense` | Current entitlement, portal owner and refresh-key hash; issuance/maintenance/fulfillment | Customers need a current license independent of their billing history. No device or seat enforcement. |
| `LicenseBlob` | Append-only signed JWT versions | Renewal/reissue delivers a new version while retaining evidence of previously issued offline entitlements. Unique `(LicenseId, Version)`. |
| `LicensingAuditEvent` | Support, approval and fulfillment actions | Explains operator changes without logging keys, tokens or webhook payloads. |
| `PriceBook` | Five configurable offerings and Stripe price mappings | Local approval controls public sale; Stripe controls actual pricing/payment. Unapproved drafts stay hidden. |
| `LicenseOrder` | Purchase identity, customer and payment/delivery status | Needed before redirect and for recovery, account history, invoices and refunds. |
| `OrderLine` | Accepted offering, quantity and entitlement snapshot | A later catalog edit must not change what an earlier purchase buys. One line per order is enforced by a unique index. |
| `LicenseCheckoutPolicy` | Accepted promotion/tax flags | Inserted with the pending order; fulfillment must use the original policy, not current settings. Missing evidence fails closed. |
| `LicenseAgreement` | Versioned Markdown terms | Each published version is insert-only; customers can accept a stable version. |
| `LicenseAgreementAcceptance` | Order-bound acceptance evidence | Retained separately from mutable order status and anonymized on account deletion. |
| `LicenseOrderSettlement` | Verified subtotal, discount, tax, total and Stripe promotion ID | One insert per paid order, in the same transaction as license issuance. Replay compares evidence instead of replacing it. |
| `StripeEventInbox` | Verified incoming event identity and processing state | Recovers a crash between webhook acceptance and job scheduling; retries read fresh provider state. |
| `LicenseRefundRequest` | Operator intent and Stripe idempotency key | Prevents uncertain retries from creating a second refund. |
| `LicenseRefund` | Provider refund result/status | Multiple partial refunds need separate provider identities. Updated only from verified Stripe state. |
| `LicenseDispute` | Provider dispute state plus local review | A payment dispute and a refund have different lifecycles; neither silently revokes an offline file. |
| `LicenseNotification` | Durable delivery/reminder/transfer outbox | Emails survive restarts and are deduplicated by their business identity. SMTP itself remains at-least-once. |
| `LicenseNotificationPreferences` | Customer email choices | Release/win-back messages are opt-in; update reminders can be disabled. |
| `LicenseTransfer` | Recipient acceptance/expiry/cancellation | The existing customer transfer flow changes portal custody without transferring invoices or rewriting old JWTs. |

These are needed for the retained storefront and customer-support features, not all for the cryptographic verifier. Transfers and lifecycle emails are optional product features, but their UI and workflows are retained. The one-to-one checkout snapshots could physically share the order table; keeping them separate makes their insert-only lifecycle explicit and prevents ordinary status updates from accidentally overwriting accepted evidence. This is a deliberate integrity boundary, not support for multi-product shopping carts.

## Removed application tables

| Table | Replacement/reason |
| --- | --- |
| `Product` | One product constant (`LicenseProduct.Id`), checked before signing. No singleton row or product triggers. |
| `ProductFeature` | Build-date JWT coverage replaced per-feature catalogs. |
| `ProductRelease` | Published releases come from the configured GitHub repository. |
| `ReleaseArtifact` | Download assets come from GitHub; platform packaging/signing belongs to the software repository. |
| `SigningKey` | JWT private key is supplied through server secrets; apps embed the public key. No wrapped leaf keys in SQL. |
| `ReleaseReconciliation` | No second release catalog to reconcile. GitHub responses have a persistent last-successful JSON cache. |
| `ActivationEvent` | Optional refresh no longer records telemetry. Offline verification needs no activation ledger. |
| `LicenseInstallation` | No installation tracking or enforcement; corresponding account UI and APIs removed. |
| `LicenseAbuseSignal` | Removed installation-count review feature and retention worker. |
| `LicensePromotion` | Stripe owns promotions. Settlement keeps the applied provider ID; no local promotion/referral mirror. |

The corresponding release/catalog/signing/telemetry/promotion APIs and workers were removed, including the unused manual price-mapping API that bypassed Stripe approval checks. Release announcements and win-back email now use the same GitHub client/cache as Downloads.

## Identity and framework tables

| Table | Decision |
| --- | --- |
| `AspNetUsers` | Keep: customer/admin accounts and profile. |
| `AspNetRoles` | Keep: administrator role. |
| `AspNetUserRoles` | Keep: role membership. |
| `AspNetUserClaims` | Keep: Identity claim storage. |
| `AspNetRoleClaims` | Keep: Identity role-claim storage. |
| `AspNetUserLogins` | Keep: Identity external login storage. |
| `AspNetUserTokens` | Keep: Identity token storage. |
| `__EFMigrationsHistory` | Keep: EF migration bookkeeping. |
| `__EFMigrationsLock` | Keep: SQLite EF migration lock; provider-owned, not a business table. |
| `Migration` | Keep: OrmLite migration bookkeeping. |
| `BackgroundJob` | Keep: queued/running Stripe processing and Identity email jobs. |
| `JobSummary` | Keep: framework job status/administration. |
| `ScheduledTask` | Keep as part of the background-job framework schema, even when no recurring jobs are configured. |
| `CompletedJob` | Keep: framework completed-job archive in monthly jobs databases. |
| `FailedJob` | Keep: framework failure archive and recovery diagnostics. |
| `RequestLog` | Keep: sanitized operational diagnostics in a separate monthly SQLite database; optional infrastructure, not license state. |
| `ApiKey` | Remove: no remaining release-CI API-key consumers. |
| `CrudEvent` | Remove: no AutoQuery writes; licensing has an explicit audit trail. |

The claim/login/token tables belong to the standard EF Identity store even when empty today. Removing individual tables while retaining that store would break supported account features. The same applies to framework-managed job tables. Jobs and request logs use separate SQLite files independently of the application database provider.

## Integrity without triggers

- No application API updates/deletes accepted checkout policy, order lines or settlement snapshots. Published agreement versions cannot be reused. Account deletion deliberately anonymizes personal acceptance fields while keeping commercial evidence.
- All issuance, settlement and JWT-history writes share a transaction. Settlement's order primary key and blob's `(LicenseId, Version)` unique index prevent duplicate issuance/history versions; conflicting work rolls back.
- Repeated paid evidence is compared with the recorded settlement, including promotion/discount/tax details. Mismatches and missing policy/settlement records are rejected.
- The signing boundary rejects a different product ID. Build dates belong to the software build, not editable release rows in this database.
- These are application invariants plus database uniqueness constraints. A database administrator with direct write access can edit evidence; the template no longer claims database-level immutability against such writes. Admin database access is privileged and must not be used to rewrite evidence.

## Provider review and verification limits

All retained table definitions and indexes are generated with the SQLite, PostgreSQL, MySQL and SQL Server OrmLite dialects in tests. SQLite tests execute the migration, revert/recreate it, check zero triggers, and verify pending-order and real-ID uniqueness behavior. Long notification bodies, review reasons and audit details explicitly use text columns rather than default short strings.

SQL Server's nullable unique indexes require `WHERE ... IS NOT NULL`, so the migration filters its two Stripe-identity indexes on that provider. The other three retain ordinary unique indexes. This follows [Microsoft's index guidance](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-index-design-guide). Table creation and naming otherwise use [OrmLite's schema APIs](https://docs.servicestack.net/ormlite/apis/schema).

The live database suite now passes on PostgreSQL 17.6, MariaDB 12.3.3 and SQL Server 2025 (17.0.4075.5), with 17 checks per server. It executes baseline revert/recreate, verifies zero triggers and uniqueness constraints, tests Identity/customer lookup and commerce workflows, and starts/restarts the production host against the same database. PostgreSQL uses PascalCase names matching EF; MySQL uses DATETIME(6), and SQL Server uses DATETIME2. Both ORMs select the same configured provider. Server Identity bootstrap is suitable for this unpublished baseline, not subsequent schema upgrades. Production deployment and multi-instance concurrency acceptance remain separate; see [VERIFICATION.md](VERIFICATION.md).

The unpublished template edits its single baseline directly. The current local development database was backed up before dropping the ten obsolete application tables, `ApiKey`, `CrudEvent` and six triggers. Its existing order, license and payment evidence were retained. No deployed compatibility migration was added.
