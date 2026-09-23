# Desktop integration

Use `LicenseJwt.Verify` in .NET or `verifyLicense` in `MyApp.Licensing.JavaScript/license.mjs`. Supply the public key, issuer, product and embedded `YYYY-MM-DD` build date. Enable paid features only for a valid covered license. Verification needs no server, activation, feature catalog or certificate chain.

The Avalonia sample starts with `dotnet run --project MyApp.SampleApp -- --desktop`. Publish with `-p:LicensePublicKeyFile=/absolute/path/license-public.pem -p:AppBuildDate=YYYY-MM-DD`; never ship its disposable demonstration key.

`EncryptedActivationStore` supplies optional encrypted local persistence with a host-provided 32-byte key. Use the OS secret store to protect that key, and user-owned storage. Manual paste/import remains available. `ActivationClient` can optionally fetch the latest renewed license from `/licensing/refresh`; failure must never block offline paid use. Only the bearer key or JWT is required; extra SDK metadata is ignored and never stored.

Public downloads come from GitHub releases via the website. The older standalone signed-update helper library is not wired into the licensing server: `/updates` and appcast endpoints have been removed. Applications choosing a native updater must implement it in their software repository, including platform signing/notarization, secure storage, download verification and installation consent. This template does not provide unattended installation.
