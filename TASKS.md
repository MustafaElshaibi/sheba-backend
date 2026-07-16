# TASKS — Actionable Backlog

Derived from [docs/roadmap.md](docs/roadmap.md); gap context in
[docs/known-issues.md](docs/known-issues.md). One task ID per change set; tick on merge and
update the known-issues row in the same PR.

## Phase 0 — Harden the base (prerequisite plumbing)

- [ ] **T-API-1** JSend everywhere: add `JSendResponse<T>` + `JSend` factories to
      `Sheba.Shared.Kernel/Responses/`; endpoint result filter on every module route group;
      replace ProblemDetails exception middleware with JSend `fail`/`error` mapping; 401/403
      challenge bodies; Swagger schema filter. Exempt `/connect/*`, `/.well-known/*`, Hangfire,
      Swagger. Spec: [docs/api-contract.md](docs/api-contract.md).
- [ ] **T-DB-1** Create initial EF migrations for the 9 contexts without them (Citizen, Ministry,
      ServiceRequest, Document, Wallet, Payment, Notification, Audit, Admin); delete the
      `EnsureCreated()` fallback from `MigrationExtensions`; verify clean-volume
      `docker compose up`.
- [ ] **T-EVT-1** Durable events: move outbox primitives to Shared.Kernel; per-module
      `outbox_messages` tables; Transaction behavior writes raised events to the outbox in the
      command's transaction (and register a real `IUnitOfWork`); Hangfire dispatcher (5 s poll,
      backoff, dead-letter); consumer inbox table keyed by `EventId` for idempotency.
- [ ] **T-SEC-2** ASP.NET `RateLimiter`: strict sliding windows on `/api/identity/register`,
      `/login`, `/verify-otp`, `/connect/token`; sane defaults elsewhere; Redis-backed counters;
      429 as JSend `fail`.
- [ ] **T-STD-1** Add `Result<T>`/`Error` to Shared.Kernel; adopt in Identity first (one module
      per pass, never mixed styles within a module).
- [ ] Housekeeping: delete `Class1.cs` stubs in `Modules/Citizen`.

## Phase 1 — Identity completion

- [ ] **T-SEC-1** Enforce TOTP at admin login (secret already on `AdminUser`; add enrollment +
      verification step; recovery codes).
- [ ] **T-SEC-4** Signing-cert rotation-by-overlap: config for multiple certs, rotation runbook in
      [docs/security.md](docs/security.md) §4, staging drill. Decide the open question on
      `RefreshTokenFamily` vs OpenIddict-native tracking (known-issues §3.5).
- [ ] **T-AUTH-1** Ministry-Admin scoping: `ministry_id` claim + ownership policy applied to every
      `/api/ministry` and admin ServiceRequest endpoint; contract tests per the permission matrix.
- [ ] Password reset flow (OTP-gated) + account recovery rules in
      [docs/business-rules.md](docs/business-rules.md).
- [ ] RP secret rotation endpoint + consent screen copy for `civil_data`.

## Phase 2 — Integration depth

- [ ] **T-SRV-1** Webhook hardening: timestamp window (±5 min) + `X-Sheba-Delivery-Id` dedup
      store on top of HMAC; store-and-alert invalid receipts; replay tests.
- [ ] **T-SRV-2** Add `JsonSchema.Net` to `Directory.Packages.props`; validate service form
      submissions against `form_schema_json` server-side; JSend `fail` with per-field keys.
- [ ] **T-INT-1** OpenCRVS `INationalIdProvider` adapter (GraphQL/REST) + contract tests.
- [ ] **T-INT-2** OTP provider failover ordering + spend alarm hooks.
- [ ] Ministry health dashboard: scheduled `TestConnectionAsync` sweep → Admin KPIs.

## Phase 3 — Money & credentials

- [ ] **T-PAY-1** Payment application layer: `CreatePaymentOrder` / `ConfirmPayment` / `Refund`
      commands + validators; `PaymentCompletedEvent` consumed by the ServiceRequest workflow
      (delete the direct `MarkPaymentComplete` coupling); `IPaymentGateway` seam + mock gateway.
- [ ] Wallet: VC verification + presentation endpoints; revocation-status API; revoke VCs on
      account suspension/deactivation (BR-WA-1).
- [ ] **T-NOT-1** Implement `NotificationTemplate` (bilingual) + template-keyed sends; replace
      hardcoded message strings; implement the `Notification` in-app entity or delete the stub.

## Phase 4 — Audit, tests & scale-readiness

- [ ] **T-AUD-1** INSERT-only Postgres grant for the app role on the `audit` schema.
- [ ] **T-AUD-2** Hash-chain: `prev_hash`/`entry_hash` columns, chain verification job, periodic
      anchor export.
- [ ] **T-AUD-3** Monthly partitioning for `audit_events`.
- [ ] **T-ADM-1** BI projection rebuild command (replay from outbox history).
- [ ] **T-TST-1** Identity test matrix: all 8 mock-citizen onboarding outcomes, generic-error
      indistinguishability, login status-gate, OTP policy (TTL/attempts/single-use/throttle),
      refresh-family reuse revocation. Introduce injectable `TimeProvider`.
- [ ] **T-TST-2** Ministry tests: 5 auth adapters' header/token shapes; AES-GCM round-trip +
      tamper failure; OAuth token-cache refresh.
- [ ] **T-TST-3** ServiceRequest tests: step branching, payment gate, webhook signature rejection,
      lifecycle guards.
- [ ] **T-TST-4** Contract tests: JSend golden shapes per route group; permission-matrix rows;
      OIDC endpoints still spec-shaped. Integration tests on Testcontainers (Postgres/Redis/MinIO).
- [ ] **T-SEC-6** DB volume encryption documented + enabled on prod host; **T-SEC-7** column
      crypto for `form_data_json`; **T-DOC-2** MinIO SSE + antivirus scan hook (**T-DOC-1**).
- [ ] Load test (k6/NBomber) vs [docs/performance.md](docs/performance.md) targets.

## Phase 5 — Production migration (post-pilot)

- [ ] Real civil-registry adapter + sandbox contract tests.
- [ ] Real SMS provider(s); SMTP relay replaces MailHog.
- [ ] **T-SEC-3** Secrets store (vault/Docker secrets); AES key-id rotation; production signing
      certs out of images.
- [ ] TLS reverse proxy (Caddy/Nginx) + HSTS + security headers.
- [ ] Backup: nightly `pg_dump` + WAL archiving off-box; restore drill documented.
- [ ] Extraction dry run: Notification module out-of-process + RabbitMQ behind the outbox.
