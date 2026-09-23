# Database deployment destinations

The template follows next-saas's Kamal destination layout:

| Destination | Application database | Accessory |
| --- | --- | --- |
| `sqlite` | SQLite file in `App_Data` | None |
| `postgres` | PostgreSQL | `postgres:18-alpine` |
| `mysql` | MySQL-compatible server | `mysql:8.4` |
| `sqlserver` | SQL Server | SQL Server 2022, Express by default |

`config/deploy.yml` is shared. `deploy.<destination>.yml` adds a private database accessory and sets `Database__Provider` for both OrmLite and EF Identity. Credentials come from `.kamal/secrets-common` and `.kamal/secrets.<destination>`, following [Kamal's destination secret handling](https://kamal-deploy.org/docs/configuration/environment-variables/). No database ports are published by these overlays.

## Configure a deployment

1. Set the GitHub Actions repository variable `DB_PROVIDER` to `sqlite`, `postgres`, `mysql` or `sqlserver` (default: `sqlite`).
2. For a server provider, set the `DB_PASSWORD` Actions secret. The bundled accessories use it for the administrator and the database-scoped `next_license` login. SQL Server requires a password satisfying its complexity policy.
3. Copy `appsettings.deploy.<provider>.example.json` to a private file. Replace the hostname `YOUR_SERVICE-<provider>` with the actual service name from `deploy.yml` and use the same password in the application connection string. Populate JWT, Stripe, SMTP and public URL settings. Save the JSON as the `APPSETTINGS_JSON` Actions secret; do not commit the populated file.
4. Supply the existing registry/SSH/deployment secrets. Place the JWT private-key file in the durable `App_Data/license-keys` directory, readable by application UID 1654, or supply `Licensing:LicensePrivateKeyPem` in the private settings JSON instead.
5. Run the Release workflow. It passes `-d "$DB_PROVIDER"` to every Kamal command, provisions/starts the selected accessory, deploys, and invokes the migration task. Manual `workflow_dispatch` is also supported.

The workflow encodes JSON as `APPSETTINGS_JSON_BASE64`. The application loads that configuration before hosting startup; explicit environment variables override it. The destination's provider and the configured connection string must agree. SQLite connection strings use **`DataSource=` without a space**.

For a manual deployment, run `kamal server bootstrap -d <provider>`, the matching `config/db/<provider>/pre-deploy.sh` if present, and `kamal deploy -d <provider>`. The PostgreSQL initializer and MySQL image create the application database/login. SQL Server's hook waits for readiness and creates its database/login idempotently inside the accessory, without putting passwords into SSH arguments. Hook failures stop deployment.

SQL Server defaults to Express rather than a development-only edition. Set `MSSQL_PID` to your appropriately licensed edition when required. The example trusts the accessory's self-signed SQL Server certificate inside the private Docker network; use a trusted certificate and `TrustServerCertificate=False` for a managed/remote database. Managed databases can use a custom destination without an accessory and their own connection/TLS settings.

Accessory versions are deployment defaults, not claims that every version was tested. The local ServiceStack compose stack uses PostgreSQL 17, MariaDB and SQL Server 2025; see `docs/licensing/VERIFICATION.md` for actual results. Changing providers selects a different database; it does not transfer existing customers or orders. Initializers do not rotate existing database passwords.

## Schema lifecycle

SQLite keeps the checked-in EF Identity migration. Server providers bootstrap the current Identity model using the matching EF provider, including when OrmLite tables already exist. This matches next-saas's approach for an unpublished template and is idempotent; it is not an upgrade mechanism for later Identity schema changes. Add proper provider-specific EF migrations before shipping such changes.

All providers use the same trigger-free OrmLite `Migration1000` for licensing. PostgreSQL preserves PascalCase names to match EF Identity; MySQL uses `DATETIME(6)` and SQL Server uses `DATETIME2` to retain timestamp precision. SQL Server's nullable Stripe-ID indexes are filtered. The .NET application includes globalization support required by database drivers.

Continue persisting `App_Data` even with a server database: background jobs, request logs, data-protection keys and the GitHub fallback cache still live there. The bundled jobs/email workers remain intended for a single application instance. Switching the main database does not by itself provide multi-instance worker coordination.

## Run live database tests

Set `LICENSE_TEST_PROVIDER` to `postgres`, `mysql` or `sqlserver`, and set `LICENSE_TEST_ADMIN_CONNECTION` to an administrator connection on a **test** server, with its administrative database selected (`postgres`, `mysql` or `master`). Then run:

```sh
dotnet test MyApp.Tests --filter TestCategory=Database -m:1
```

The fixture creates a random `next_license_test_*` database, exercises migrations, Identity, production host startup/restart and licensing workflows, then drops only that database. It never resets the database named in the administrator connection. Failed assertions still run cleanup; if the runner is forcibly killed, a test database may remain for manual removal. Do not run this against a production server.

Without those environment variables, ordinary tests remain SQLite-only. Stripe calls are faked in the database suite: this verifies persistence and fulfillment behavior, not live charges. Kamal configurations can be checked with `kamal config -d <provider>`; this does not deploy anything.
