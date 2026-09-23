# Implementation status

The primary flow is ES256 JWT issuance and standalone build-date coverage verification. Tokens contain registered name, organization, seats, edition and dated/Lifetime updates. .NET and Node/Electron clients verify with one public key and their embedded release date; no network, current-date comparison, feature catalog or certificate chain is needed. PLAN.md describes the superseded design.

## Software operations and checkout

Operations has separate overview, pricing, releases, licenses, orders, terms and integration pages. Paid offerings begin as drafts. Operators save amounts, create missing one-time Stripe prices and explicitly approve offerings. Only approved prices appear publicly; Free requires no Stripe setup. Administrative issuance uses searchable customer lookup. Support changes select a license and its current version.

The server loads local `.env` without logging values. `Stripe__SecretKey` and `Stripe__WebhookSecret` override the legacy `LicenseStripe` section. Persistent JWT signing credentials are required before checkout. Orders display product, coverage, amount and useful delivery status. The account return page rechecks pending payments against fresh provider evidence; background recovery also runs. License terms and GitHub release notes render as Markdown.

GitHub releases and assets are fetched from `Licensing__GitHubRepository`, with a durable last-successful JSON fallback across provider failures and restarts. Software builds, platform signing and release publishing belong in the software repository. The template does not include a release-publishing script or CI release workflow.

## Database simplification

The unpublished template has two migrations: one EF Identity baseline and one OrmLite licensing baseline. Migration1000 now creates **17 application tables and zero triggers**, down from 27 tables and six SQLite-only triggers. The full [table review](TABLE-REVIEW.md) records every application, Identity and framework table.

Removed: product singleton, feature/release/artifact catalog, database signing keys, release reconciliation, activation/installations/abuse tracking and promotion/referral mirror. Their server APIs, background workers and installation UI were removed. API-key and generic CRUD-audit schema initialization were also removed. Optional refresh accepts a bearer key or JWT without storing device data. GitHub-backed release email shares the download client/cache.

Checkout policy and settlement are insert-only application evidence. Missing policy fails closed; repeated paid evidence must match the complete original settlement. Transactions and unique identities protect against duplicate issuance. Product identity is checked before signing. Long evidence/email fields use text columns. SQL Server's nullable Stripe-ID uniqueness uses filtered indexes; other dialects use ordinary unique indexes. Direct privileged database writes can bypass application immutability.

The local SQLite database was backed up before removing obsolete tables/triggers; its existing order, license and settlement were preserved. Both OrmLite and EF Identity now select SQLite, PostgreSQL, MySQL/MariaDB or SQL Server from `Database:Provider`. SQLite retains its EF baseline; server providers bootstrap the current Identity model idempotently, following next-saas's unpublished-template approach. Future Identity changes require provider-specific migrations.

Live tests passed against PostgreSQL 17.6, MariaDB 12.3.3 and SQL Server 2025 (17.0.4075.5): 17 checks per server, including baseline recreation, zero triggers, Identity/customer lookup, licensing workflows, and production application startup/restart with encoded deployment settings. Disposable test databases were removed. Provider configuration preserves PostgreSQL Identity names, MySQL microseconds and SQL Server datetime2 values. Four next-saas-style Kamal destinations validate; see [deployment configuration](../../config/README.md). Production accessories have not been deployed.

## Provider acceptance limits

GitHub download acceptance previously passed against `NetCoreApps/acme-studio`, including v1.0.0 platform assets. Stripe test-mode catalog acceptance previously passed provisioning, retries, approval, removal from sale and archival of an isolated test product/price. The local $49 Stripe test purchase was verified paid and recovered through normal reconciliation, recording exactly one license and settlement after persistent signing credentials were configured.

Local webhook delivery remains unconfigured. Full webhook, refunds/disputes, tax/discount combinations, SMTP delivery, multi-instance concurrency, backup restoration and production deployment still need acceptance in a configured environment. No new external payment was made for the database simplification.

See [VERIFICATION.md](VERIFICATION.md) for the current regression checks. Passing these tests does not establish production readiness or multi-instance concurrency support.
