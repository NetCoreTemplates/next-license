# Operating the licensing template

Configure `.env` using `.env.example`, then restart the backend. Set the JWT private-key path/PEM, stable base64 32-byte `Licensing__ShortKeySalt`, Stripe credentials and `Licensing__GitHubRepository=owner/repository`. Keep private keys and salts out of Git, browser bundles and logs. Apps embed only the public key and their immutable build date.

In Operations, save offering amounts, use **Create Missing Stripe**, then explicitly **Approve for sale**. Publish your own versioned Markdown license agreement before selling. Only Free appears by default. Stripe controls payment, promotion eligibility, tax and refunds; local approval controls visibility. Editing an offering does not change accepted orders. There is no local promotion-code or referral mirror.

Configure `/stripe/webhook` for Checkout completion/async success/failure, refund changes and disputes. Webhooks are signature-verified and queued durably; processors retrieve fresh provider evidence. The accepted tax/promotion policy is inserted before redirect. Paid fulfillment atomically inserts settlement evidence and the signed JWT. Duplicate fulfillment cannot create another settlement/license. Changed or missing evidence requires investigation rather than replacement.

Pending webhook recovery runs every five minutes; checkout reconciliation runs every ten minutes. The account return page can also refresh an owned order. Refund requests carry a durable idempotency identity; uncertain retries reuse it. Offline JWTs cannot be remotely disabled by a refund or revocation. Revocation prevents later delivery/refresh.

Downloads and release announcements use public GitHub releases and assets. Successful JSON is cached on disk under `App_Data/github-downloads`; provider failure falls back to the last successful response. Back up this directory if fallback must survive replacement of the host. Publishing and platform signing happen in the software repository; no release catalog, release signing keys or CI release API exists here.

Configure SMTP for Identity and licensing email. Delivery messages link to the authenticated portal rather than attaching bearer JWTs. Release/win-back messages default off; customers can disable reminders. GitHub failure does not prevent delivery emails or renewal reminders. SMTP delivery is at-least-once; run one worker instance unless adding shared leases/provider idempotency for scaling.

`/up` is liveness. `/ready` checks local schema, license/Stripe configuration, agreement and active prices; it does not prove external delivery or payment acceptance. Use Operations orders and framework job diagnostics to investigate failures. Request logging excludes sensitive bodies, tokens and network identifiers.

Optional license refresh is stateless apart from reading current entitlement and in-memory throttling. It stores no device or activation history. Account deletion removes portal ownership links, preferences and transfers while preserving minimum commercial evidence and signed history. Define your retention policy before launch. Transfers require a verified recipient's acceptance, transfer portal custody only, and do not invalidate old offline files.

Back up SQLite using its backup API or while stopped, plus private key, public key, salt, ASP.NET data-protection keys and GitHub cache. Never replace signing material as a shortcut to fixing delivery. See [TRUST.md](TRUST.md) and the complete [table review](TABLE-REVIEW.md).

Before launch, use a configured Stripe test environment, webhook endpoint and SMTP sink to exercise paid/discounted checkout, delayed and repeated events, renewal, upgrades, refunds, disputes and recovery. Local regression tests do not replace provider acceptance or a real backup/restore exercise. See [STATUS.md](STATUS.md) for completed checks and remaining limits.
