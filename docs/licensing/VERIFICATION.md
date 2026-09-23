# Local verification — 23 September 2026

- Backend regression suite: 55 passing tests; two server-only integration checks skip in the default SQLite run. Coverage includes a trigger-free baseline with revert/recreate, unique Stripe identities and multiple pending orders, missing checkout policy, replayed settlement evidence (including changed tax/discount with an unchanged total), JWT issuance/renewal/reissue, stateless refresh/key rotation, refunds, transfers, account deletion, GitHub cache fallback and opt-in release email.
- Schema generation passes with SQLite, PostgreSQL, MySQL and SQL Server OrmLite dialects. Live PostgreSQL 17.6, MariaDB 12.3.3 and SQL Server 2025 (17.0.4075.5) each pass all 17 database-category checks. These cover baseline revert/recreate, zero triggers, unique IDs, EF Identity bootstrap/customer lookup, commerce workflows, and production host startup/restart using encoded deployment settings. Each generated test database was removed. Stripe is faked in this suite; no live charges were made.
- Backend and sample builds pass. The model rebuild reports the existing nullable initialization warnings in User.cs.
- Client TypeScript checking, 14 frontend tests and production static export pass. DTOs are regenerated from the restarted backend. The build reports an advisory about the age of baseline-browser-mapping data.
- Browser checks: Operations overview loads with one active license and no pending payments/review orders; the account shows the preserved Acme Studio Pro license and $49 paid order. Installation tracking controls are gone.
- Local development database: backup taken before removal of ten obsolete application tables, unused ApiKey/CrudEvent tables and six triggers. Existing commercial data retained. SQLite integrity_check returns ok.
- All four Kamal destinations validate with Kamal 2.3.0 using dummy credentials; database hook shell syntax checks pass. No production accessory deployment was performed. Deployment image versions differ from the local compose servers; see [config/README.md](../../config/README.md).
- `git diff --check` passes.

External-provider acceptance is separate. See [STATUS.md](STATUS.md) for previously completed GitHub/Stripe checks and remaining production work, and [TABLE-REVIEW.md](TABLE-REVIEW.md) for the complete schema audit.
