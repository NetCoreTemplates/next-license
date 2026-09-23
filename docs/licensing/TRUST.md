# License trust and recovery

A standard ES256 JWT is signed with the server's private P-256 key. Applications embed the public key, issuer, product identity and immutable build date. Verification has no network dependency and does not use today's date. Dated licenses cover builds through their signed cutoff; Lifetime covers all builds. Tokens do not expire. Signed name, organization, edition and seats are display/entitlement data; the template does not enforce devices or seats.

Generate production signing material outside source control. Configure `Licensing__LicensePrivateKeyPath` or `Licensing__LicensePrivateKeyPem` and a separate stable 32-byte base64 `Licensing__ShortKeySalt`. The salt hashes bearer refresh keys in SQL. Keep the private key, salt and issued JWTs out of logs. JWTs are signed, not encrypted: anyone holding one can read its claims and use its refresh key.

Back up the application database, private/public key, salt and ASP.NET data-protection keys in controlled storage. Prove restoration using a disposable copy before selling. Changing the signing key requires deliberate public-key distribution to applications; the template does not provide transparent rotation of already shipped trust. Rotating a refresh key does not invalidate an already issued offline JWT.

GitHub releases are the download source and have a persistent last-successful JSON cache. Their dates never grant entitlements by themselves: the app's embedded build date is compared with the signed license. Platform executable signing/notarization and updater integrity belong to the software repository. There is no database release-signing hierarchy or signed catalog endpoint in this template.

Support changes append a JWT version instead of replacing signed history. Checkout and settlement evidence use insert-only application paths and transaction/unique-key protection. Direct privileged database writes can bypass application policy; see [TABLE-REVIEW.md](TABLE-REVIEW.md).
