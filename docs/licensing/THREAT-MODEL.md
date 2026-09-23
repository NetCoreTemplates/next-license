# Security boundaries

Paid feature checks run offline against a signed JWT and the embedded build date. A modified application can bypass its own checks; licensing does not provide DRM or hardware enforcement. Refunds/revocation prevent future server delivery but cannot erase distributed files.

The signing private key and refresh-key salt are server secrets. A copied JWT is a bearer credential and contains readable personal details. Do not log tokens, keys, webhook bodies or signing material. Optional refresh validates a key/token and current license state; it stores no installation history and is rate-limited. Failed refresh never changes offline entitlement.

Customer APIs scope access to the authenticated owner; Operations requires Admin. Cookie-bearing mutations require a same-origin request. Checkout requires a verified account and explicit acceptance of the current agreement. Browser totals are not authoritative. Stripe webhooks are signature-checked, and fulfillment retrieves provider evidence and validates it against accepted snapshots. Unique order/settlement/blob identities and transactions prevent replay from issuing duplicate entitlements.

Removing SQL triggers means immutability is enforced by the application's write paths, not against a privileged DBA. Restrict database administration and audit support actions. Account deletion anonymizes ownership while retaining commercial evidence; the operator must define a retention policy.

Downloads trust the configured public GitHub repository. The last successful response is available during outages and can be stale. The application does not authenticate/install binaries on behalf of desktop clients; use the software repository's platform signing and updater protections. No local release catalog or CI release credentials exist.

See [STATUS.md](STATUS.md) for validation limits and [TABLE-REVIEW.md](TABLE-REVIEW.md) for database ownership and constraints.
