# TASKS — Actionable Backlog

Derived from [docs/roadmap.md](docs/roadmap.md); gap context in
[docs/known-issues.md](docs/known-issues.md). One task ID per change set; tick on merge and
update the known-issues row in the same PR.

Tasks added by the 2026-07 code audit carry *Priority · Effort · Deps* metadata and acceptance
criteria (AC). Effort: **S** < ½ day · **M** 1–2 days · **L** 3+ days. Each cross-references its
row in known-issues §1 by the same ID.

## Phase 0 — Harden the base (prerequisite plumbing)

- [x] **T-API-1** JSend everywhere: add `JSendResponse<T>` + `JSend` factories to
      `Sheba.Shared.Kernel/Responses/`; endpoint result filter on every module route group;
      replace ProblemDetails exception middleware with JSend `fail`/`error` mapping; 401/403
      challenge bodies; Swagger schema filter. Exempt `/connect/*`, `/.well-known/*`, Hangfire,
      Swagger. Spec: [docs/api-contract.md](docs/api-contract.md).
- [x] **T-DB-1** Create initial EF migrations for the 9 contexts without them (Citizen, Ministry,
      ServiceRequest, Document, Wallet, Payment, Notification, Audit, Admin); delete the
      `EnsureCreated()` fallback from `MigrationExtensions`; verify clean-volume
      `docker compose up`.
- [x] **T-EVT-1** Durable events: move outbox primitives to Shared.Kernel; per-module
      `outbox_messages` tables; Transaction behavior writes raised events to the outbox in the
      command's transaction (and register a real `IUnitOfWork`); Hangfire dispatcher (5 s poll,
      backoff, dead-letter); consumer inbox table keyed by `EventId` for idempotency.
- [x] **T-SEC-2** ASP.NET `RateLimiter`: strict sliding windows on `/api/identity/register`,
      `/login`, `/verify-otp`, `/connect/token`; sane defaults elsewhere; Redis-backed counters;
      429 as JSend `fail`.
- [x] **T-STD-1** Add `Result<T>`/`Error` to Shared.Kernel; adopt in Identity first (one module
      per pass, never mixed styles within a module).
- [x] Housekeeping: delete `Class1.cs` stubs in `Modules/Citizen`.
- [x] **T-AUTH-2** Finish authorization coverage *(High · S · deps: none · issue: T-AUTH-2)*:
      apply role policies to the Wallet, Admin, and Audit route groups; wallet/citizen-owned
      resources resolve the citizen from the token `sub` claim instead of a caller-supplied
      `citizenId`; intentionally public routes get explicit `AllowAnonymous` + a why-comment.
      AC: every mapped route group carries `RequireAuthorization` or a justified `AllowAnonymous`;
      contract tests prove anonymous → 401 and wrong-role → 403 on `/api/wallet`, `/api/admin`,
      `/api/admin/audit`.
- [x] **T-AUD-4** Activate audit logging safely *(High · S · deps: none · issue: T-AUD-4)*:
      register `AuditLoggingBehavior` (+ `IHttpContextAccessor`) in the MediatR pipeline after
      TransactionBehavior; replace raw request/response snapshots with a redaction allowlist so
      passwords, OTP codes, NIDs, and tokens never reach `audit_events`.
      AC: a state-changing command writes an audit row with the actor taken from the JWT `sub`;
      a captured `LoginCitizenCommand` snapshot contains no password; redaction is unit-tested.
- [x] **T-ARC-1** Restore module boundaries *(High · M · deps: T-EVT-1 ✓ · issue: T-ARC-1;
      unblocks T-CIT-1, T-NOT-2)*: move integration event contracts into `Sheba.Shared.Kernel`
      so consumers stop referencing producer assemblies; drop Admin.Application →
      Identity/ServiceRequest Domain refs and Wallet.Application → Identity.Application ref;
      replace ServiceRequest → Payment references with an event/port (coordinate with T-PAY-1);
      narrow ServiceRequest → Ministry to the sanctioned query-port surface.
      AC: every module csproj references only Shared.Kernel (plus the documented SR→Ministry
      port exception); solution builds; events still flow through the outbox end-to-end.
- [x] **T-GW-1** Pipeline completion per §3.5 *(Medium · S · deps: none · issue: T-GW-1)*:
      config-driven CORS policy + correlation-ID middleware (accept/emit `X-Correlation-Id`,
      enrich the Serilog scope).
      AC: responses carry a correlation id that appears on the request's log events; the
      configured SPA origin passes preflight and unlisted origins are blocked.
- [x] **T-STD-2** Housekeeping *(Low · S · deps: none · issue: T-STD-2)*: delete the stray
      `Admin.Domain/Entities/AdminUser.cs` (Identity.Domain owns the real one) and the three
      `Class1.cs` stubs in Audit; make the refresh-token grant path fully async (remove
      `GetAwaiter().GetResult()`).
      AC: build clean; single `AdminUser` type in the solution; no sync-over-async in OIDC
      endpoints.

## Phase 1 — Identity completion

- [x] **T-SEC-1** Enforce TOTP at admin login (secret already on `AdminUser`; add enrollment +
      verification step; recovery codes).
- [x] **T-SEC-4** Signing-cert rotation-by-overlap: config for multiple certs, rotation runbook in
      [docs/security.md](docs/security.md) §4, staging drill. Decide the open question on
      `RefreshTokenFamily` vs OpenIddict-native tracking (known-issues §3.5).
- [x] **T-AUTH-1** Ministry-Admin scoping: `ministry_id` claim + ownership policy applied to every
      `/api/ministry` and admin ServiceRequest endpoint; contract tests per the permission matrix.
- [x] **T-AUTH-3** Admin/KPI ministry-slice filtering *(Low · S · deps: T-AUTH-1 ✓ · issue:
      T-AUTH-3)*: filter `GetKpiSummary` and report generation by the caller's `ministry_id` claim
      when present (SuperAdmin/Auditor keep the global view). Found as a residual gap while closing
      T-AUTH-1 — sheba.md §10.2 documents "own ministry slice" for this row but it was never in
      T-AUTH-1's literal scope.
      AC: a MinistryManager's KPI/report endpoints return only their own ministry's numbers;
      SuperAdmin/Auditor unaffected.
- [x] **T-SEC-5** Access-token hardening for external RPs in production: enable OpenIddict
      access-token encryption (or reference tokens + introspection); keep unencrypted JWTs in
      dev for inspectability.
- [x] Password reset flow (OTP-gated) + account recovery rules in
      [docs/business-rules.md](docs/business-rules.md) (BR-LG-7).
- [x] RP secret rotation endpoint + consent screen copy for `civil_data`.
- [x] **T-OIDC-1** Implement `/connect/authorize` + consent *(High · L · deps: T-AUTH-2 ·
      issue: T-OIDC-1; pairs with the consent-screen bullet above)*: authorization-code + PKCE
      handler behind the already-enabled passthrough; login redirect for unauthenticated users;
      consent prompt for `civil_data` gated on LoA ≥ 2; stop defaulting `civil_data` into the
      custom grant's scope set.
      AC: an external RP completes code+PKCE end-to-end against the seeded `sheba-portal` client;
      `civil_data` is granted only after recorded consent at LoA ≥ 2; `/connect/*` responses stay
      OIDC-spec-shaped (JSend-exempt).
- [x] **T-OIDC-2** Family-reuse tracking for authorize-flow refresh tokens *(Low · S · deps:
      T-OIDC-1 ✓, T-SEC-9 ✓ · issue: T-OIDC-2)*: attach `family_id`/`family_generation` claims in
      `AuthorizeEndpoints.IssueAuthorizationCode` the same way the two custom grants already do, so
      a stolen refresh token from the browser PKCE flow also gets cascade-revoked on reuse.
      AC: a refresh token minted via `/connect/authorize` behaves identically to one from the
      custom grants under `RotateRefreshTokenFamilyHandler`.
- [ ] **T-OIDC-3** Fix `DELETE /api/admin/relying-parties/{clientId}` 500 *(Medium · S · deps:
      none · issue: T-OIDC-3)*: `OpenIddict.EntityFrameworkCore` 5.7.0's `DeleteAsync` calls an EF
      Core `ExecuteDeleteAsync` overload missing at runtime against EF Core 9.0.6
      (`MissingMethodException`) — find the OpenIddict version actually compatible with EF Core 9
      and bump the pin in `Directory.Packages.props`.
      AC: deleting a registered relying party returns 204 and the row is gone; full test suite
      still green after the version bump (shared package, broader regression risk than one route).
- [x] **T-ID-1** Account lifecycle completion *(High · M · deps: T-AUTH-2 · issue: T-ID-1)*:
      `Suspend`/`Reinstate`/`Deactivate` on the `Account` aggregate + admin endpoints (audited,
      citizen notified); persist the rejection reason; re-application path for Rejected accounts
      (new `IdentityRequest` per §6.2); Hangfire purge job for expired PendingVerification
      accounts + spent OTP records so an abandoned registration frees its NID.
      AC: the §10.2 suspend/reactivate matrix rows are enforceable; a rejected citizen can
      re-apply; an abandoned registration no longer blocks its NID after the purge window; all
      transitions guarded on the aggregate.
- [x] **T-CIT-1** Citizen module completion *(High · M · deps: T-ARC-1 · issue: T-CIT-1)*:
      consume `IdentityRequestDecidedEvent(approved)` via the kernel event + inbox to create the
      `CitizenProfile` (§5.2); map `/api/citizens` endpoints (get/update own profile,
      ownership-checked).
      AC: approving an identity request produces exactly one profile row (idempotent under
      event redelivery); a citizen can read/update only their own profile.
- [x] **T-SEC-8** Move OTP generation into the application layer *(Medium · S · deps: none ·
      issue: T-SEC-8)*: one CSPRNG generation + hashing service in Identity.Application;
      `IOtpProvider.SendAsync` takes the ready code and only delivers (§6.6) — it no longer
      returns a raw code.
      AC: no provider generates or returns codes; policy (TTL/attempts/invalidations) unchanged
      and still covered by the existing OTP tests.
- [x] **T-SEC-9** Refresh-token family reuse detection *(Medium · M · deps: T-SEC-4 decision ·
      issue: T-SEC-9)*: settle known-issues §3.5 (custom `RefreshTokenFamily` vs
      OpenIddict-native tracking), then either implement family tracking + reuse-revocation or
      delete the dead entity and update §6.4 to describe the OpenIddict mechanism actually used.
      AC: presenting a superseded refresh token revokes the whole family (integration test), or
      the entity is removed and §6.4 matches reality.

## Phase 2 — Integration depth

- [x] **T-SRV-1** Webhook hardening: timestamp window (±5 min) + `X-Sheba-Delivery-Id` dedup
      store on top of HMAC; store-and-alert invalid receipts; replay tests.
- [x] **T-SRV-2** Add `JsonSchema.Net` to `Directory.Packages.props`; validate service form
      submissions against `form_schema_json` server-side; JSend `fail` with per-field keys.
- [ ] **T-INT-1** OpenCRVS `INationalIdProvider` adapter (GraphQL/REST) + contract tests.
- [ ] **T-INT-2** OTP provider failover ordering + spend alarm hooks.
- [ ] Ministry health dashboard: scheduled `TestConnectionAsync` sweep → Admin KPIs.
- [x] **T-SRV-3** Submission gates + lifecycle integrity *(High · M · deps: T-AUTH-2 (loa claim) ·
      issue: T-SRV-3)*: enforce `RequiredLoa` (from the token's `loa` claim), eligibility, and
      required documents at submit (§5.4.1); transition guards on `ServiceRequestEntity`
      (invalid transitions throw `DomainException`); citizen cancel endpoint; scheduled SLA sweep
      moving overdue requests to `Expired`.
      AC: an LoA-1 citizen submitting an LoA-2 service gets 422; a missing required document
      blocks submission; `Complete()` from a terminal state throws; an overdue AwaitingMinistry
      request is expired by the job.
- [ ] **T-SRV-4** Step-engine failure routing *(Medium · M · deps: T-SRV-3 · issue: T-SRV-4)*:
      honor `on_failure_step` on step failure (fallback: request → `ActionRequired`); unhandled
      step types fail loudly instead of silently auto-completing; single source of truth for step
      executions (drop pre-creation at submit or reuse the pre-created rows — one mechanism, not
      both).
      AC: a failing step with `on_failure_step` routes there; an unknown step type marks the step
      `Failed` and the request `ActionRequired`; exactly one execution row exists per executed
      step.
- [x] **T-MIN-1** Ministry seed + per-endpoint outbound rate limit *(Low · S · deps: none ·
      issue: T-MIN-1)*: seed the five demo ministries referenced by the catalog's hardcoded
      GUIDs; enforce `MinistryEndpoint.RateLimitPerMinute` in `MinistryCallStepHandler`.
      AC: clean-volume `docker compose up` yields a resolvable ministry for every seeded service;
      outbound calls beyond the per-endpoint limit are deferred/failed per policy, covered by a
      test.

## Phase 3 — Money & credentials

- [ ] **T-PAY-1** Payment application layer: `CreatePaymentOrder` / `ConfirmPayment` / `Refund`
      commands + validators; `PaymentCompletedEvent` consumed by the ServiceRequest workflow
      (delete the direct `MarkPaymentComplete` coupling); `IPaymentGateway` seam + mock gateway.
- [ ] Wallet: VC verification + presentation endpoints; revocation-status API; revoke VCs on
      account suspension/deactivation (BR-WA-1).
- [ ] **T-NOT-1** Implement `NotificationTemplate` (bilingual) + template-keyed sends; replace
      hardcoded message strings; implement the `Notification` in-app entity or delete the stub.
- [ ] **T-NOT-2** Notification ownership *(Medium · M · deps: T-ARC-1 · issue: T-NOT-2;
      complements T-NOT-1)*: move the identity email handlers out of Identity.Application into
      Notification.Application (consuming kernel events via the inbox); add
      `ServiceRequestSubmitted`/`Completed` citizen notifications; write a `NotificationRecord`
      row for every send; delete the duplicate `IEmailService`/`ISmsService` from
      Notification.Domain.
      AC: Identity.Application contains no email-sending handlers; submitting/completing a
      request notifies the citizen; every send is logged in `notification_records`.
- [ ] **T-WAL-1** Persistent VC issuer key *(Medium · S · deps: none · issue: T-WAL-1)*: require
      `Wallet:IssuerPrivateKeyPem` outside Development (fail fast at startup); keep dev
      auto-generation behind a loud warning; document rotation alongside T-SEC-4.
      AC: production-mode boot without a configured key fails with a clear error; with a
      configured key, VCs issued before a restart still verify after it.

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
