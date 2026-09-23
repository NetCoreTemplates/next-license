# Next License

Sell one-time licenses that unlock Pro in a .NET or Electron app. Verification is a local function call: no HTTP, activation, certificate chain, feature catalog or .NET helper.

Next License sells one-time licenses that unlock Pro in a desktop app. Customers purchase, copy their license key from their account, and paste it into the app. The app checks the key locally and displays who it is registered to and how many seats it covers.

A license is a standard **ES256-signed JWT**, not an encrypted JWE. The server holds one private key; applications bundle its public key. Verification requires no network, activation, certificate chain, feature catalog or .NET helper process.

A dated license covers builds released on or before `updatesThrough`. Lifetime covers future builds. Covered versions remain usable forever: there is no token `exp` and no comparison with today's date. A newer uncovered build can remain in Free mode and offer renewal.

## Quick start

1. Run `npm ci` in `MyApp.Client`, then `dotnet watch` in `MyApp`.
2. Run `dotnet run --project MyApp.Licensing.Tool -- jwt ./license-keys` from the repository root.
3. Configure `Licensing:LicensePrivateKeyPem`, `Licensing:LicenseIssuer` (default `acme-studio`) and a random base64 32-byte `Licensing:ShortKeySalt` in server secrets.
4. Configure Stripe prices and your agreement in Operations. No live prices, agreement or signing key are supplied by default.
5. Bundle `license-public.pem` with your app; never distribute the private key.

```csharp
var result = LicenseJwt.Verify(token, publicKeyPem, "acme-studio", "acme-studio", "2026-09-21");
bool enablePro = result.Valid;
// result.License: Name, Organization, Seats, Edition, UpdatesThrough, Lifetime
```

```js
import { verifyLicense } from './MyApp.Licensing.JavaScript/license.mjs';
const result = verifyLicense(token, { publicKey, issuer: 'acme-studio', product: 'acme-studio', buildDate: '2026-09-21' });
const enablePro = result.valid;
// result.license: name, organization, seats, edition, updatesThrough, lifetime
```

## Sample

`dotnet run --project MyApp.SampleApp -- --desktop` demonstrates registered details and covered/uncovered/Lifetime keys with disposable signing material. Publish with `-p:LicensePublicKeyFile=/absolute/path/license-public.pem -p:AppBuildDate=YYYY-MM-DD`.

## Tests

`dotnet test MyApp.Tests` includes .NET-to-Node JWT compatibility and payment/renewal/reissue tests. Node is required. `npm run build` and `npm run test:run` in MyApp.Client validate the storefront.

## Database scope

The baseline contains 17 licensing tables and no triggers. GitHub owns releases, Stripe owns promotions/payments, and signing keys stay in server configuration. Optional refresh stores no installation history. See [the complete table review](docs/licensing/TABLE-REVIEW.md) for retained tables, removed features, integrity rules and database-provider validation limits.

## Set up your software business

1. Copy `.env.example` to `.env` in the repository root. Set `Stripe__SecretKey`, `Stripe__WebhookSecret`, and `Licensing__GitHubRepository` (`owner/repository`). Restart the backend after configuration changes. Existing process environment variables take precedence over `.env`.
2. Open `/admin/settings` to check configuration. Keep `Stripe__LiveMode=false` for a Stripe test key. Configure the ES256 license signing key through server secrets before issuing licenses.
3. In `/admin/catalog`, save the price and currency for each offering you want to sell. Click **Create Missing Stripe** to provision one-time Stripe products/prices. Review each offering and click **Approve for sale**. Free is the only plan shown until paid offerings are approved. Editing an amount makes that offering a draft again; existing order evidence is preserved.
4. Publish your license terms at `/admin/agreements`. Configure Stripe to send the licensing payment events to `/stripe/webhook` (see the existing payment integration documentation). Fulfillment requires a signing key and working webhook configuration.
5. Publish GitHub releases with Windows, macOS or Linux builds attached. `/admin/releases` and `/download` read the latest 100 public releases from the configured repository. Updater metadata such as `.blockmap` and `.yml` files is omitted from download buttons. These listings do not alter signed license entitlement dates.
6. Use `/admin/licenses` to search customers, issue administrative licenses, or update a selected license. Customer lookup fills account identifiers automatically. `/admin/orders` links directly to the matching Stripe payment and supports completing order reviews.

The example repository is `NetCoreApps/acme-studio`. Stripe catalog provisioning is separate from approval; creating prices alone never exposes a paid plan. The optional explicit `SoftwareSetupTests.Stripe_test_catalog_provisioning_is_retryable_and_requires_separate_approval` test uses `LICENSE_STRIPE_ACCEPTANCE_KEY` and accepts only a Stripe test key. It provisions an isolated test catalog, checks retries and approval, then archives its Stripe price and product; it does not perform payments.

## Database providers and Kamal

SQLite, PostgreSQL, MySQL/MariaDB and SQL Server are selected with `Database:Provider` and `ConnectionStrings:DefaultConnection`. Both OrmLite and EF Identity use the selected provider. The [deployment guide](config/README.md) covers next-saas-style Kamal destinations, settings examples, secrets and repeatable live database tests.