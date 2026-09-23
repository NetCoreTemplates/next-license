# Licensing development

The application uses an ES256 JWT with build-date coverage. Start from README.md; PLAN.md describes the superseded feature-catalog design.

Run `dotnet test MyApp.Tests -m:1`, `dotnet build MyApp.SampleApp`, and `npm run typecheck`, `npm run test:run`, `npm run build` in MyApp.Client. After DTO changes, build and restart the backend, then regenerate `lib/dtos.ts` from `/types/typescript?MakePropertiesOptional=true` before checking the client.

There is one OrmLite licensing baseline (`Migration1000`, 17 tables, no triggers), plus the EF Identity baseline. The template is unpublished: recreate disposable development databases instead of adding compatibility migrations. After release, never edit deployed migrations. Empty databases bootstrap at startup; `npm run migrate` in MyApp runs migrations explicitly. Always spell SQLite connection strings `DataSource=`, without a space.

Use Operations to configure Stripe offerings and publish terms. Checkout snapshots price, acceptance and policy before redirect. Fulfillment verifies fresh provider state and inserts settlement/license/history atomically. Do not introduce generic write APIs for evidence tables. [TABLE-REVIEW.md](TABLE-REVIEW.md) records every retained and removed table and provider-specific considerations.

Set `Licensing__GitHubRepository=owner/repository` to list public releases and builds. Software CI publishes directly to GitHub. The licensing server has no reserve/sign/publish release endpoints, leaf-key store, activation ledger or promotion mirror. Optional notifications share the durable GitHub JSON fallback with Downloads.

Demo accounts are seeded only in Development. Tests use disposable keys. Never include real credentials in fixtures, output or shipped artifacts. External acceptance tests require an explicitly configured test environment; provider dialect SQL generation is not live PostgreSQL/MySQL/SQL Server acceptance.

### Example data

To seed a fresh Development database with example data, run `npm run seed:example-data` from `MyApp`. It applies the current migrations, then creates three clearly named demo customers, two valid signed demo licenses, two sample paid orders and one pending order. The operation is safe to rerun and leaves any database with existing non-demo orders or licenses untouched. It creates no Stripe IDs, does not approve paid prices, and all order numbers are prefixed `DEMO-`. For a server database, set `Database__Provider` and `ConnectionStrings__DefaultConnection` in the environment before running it. Use a disposable development database; the paid rows are presentation fixtures, not payment evidence.

Demo customer sign-in uses `Demo-Only-Change-Me!42`; account emails are `alex.morgan@example.test`, `jamie.chen@example.test`, and `riley.patel@example.test`. The existing Development admin account is `admin@email.com` / `p@55wOrd`. Replace demo signing configuration with a disposable key before seeding; never ship that key or these accounts.

For the hosted disposable preview, set `Licensing:GeneratePreviewKeys=true` in deployment settings, then manually dispatch the Release workflow with `seed_example_data=true`. The server creates and retains a separate signing key and salt under `App_Data/preview-license-keys` with private file permissions; back them up with the database. This mode requires Stripe checkout to be disabled. The seed accepts only SQLite with Stripe disabled and takes a database backup before adding fixtures. The workflow gives the demo users a generated password that is not displayed or retained. The `DEMO-` paid rows and signed licenses are presentation fixtures; recreate the database before enabling actual checkout or using the server for real customers.

For PostgreSQL, MySQL/MariaDB and SQL Server configuration, Kamal destinations and disposable live database tests, see [config/README.md](../../config/README.md). Both ORMs use `Database:Provider`; server Identity uses current-model bootstrap until provider-specific upgrade migrations are needed. Live coverage and actual server versions are recorded in [VERIFICATION.md](VERIFICATION.md).
