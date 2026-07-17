# Changelog

All notable changes to Sheba are documented here. Format:
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning:
[SemVer](https://semver.org/) once releases begin.

## [Unreleased]

### Added
- **Signing/encryption certificate rotation-by-overlap (T-SEC-4)**: `SigningCertificateLoader`
  (`Identity.Infrastructure/Security/`) reads an ordered certificate list from
  `Identity:SigningCertificates` / `Identity:EncryptionCertificates` config — each entry either a
  thumbprint (loads from the OS/container cert store, no secret in config) or a PFX file path +
  environment-injected password — and registers them with OpenIddict in array order (first entry
  = intended primary signer). An empty/unset list (every environment today) falls back to the
  ephemeral development certificate unchanged. Replaces the dead `IsProduction` branch in
  `IdentityModule.cs` that called the same dev-cert method either way. Rotation runbook added at
  [security.md §4.1](docs/security.md); the live staging drill itself is flagged as still pending
  rather than claimed. Also **decided the open `RefreshTokenFamily` vs OpenIddict-native tracking
  question** (known-issues §3.5): keep `RefreshTokenFamily` — OpenIddict's built-in rotation only
  rejects a replayed token itself, not the RFC 9700 "revoke the whole family" cascade the design
  targets — implementation is T-SEC-9. Covered by 7 new unit tests exercising the loader against
  real self-signed PFX files (missing section, missing password, unknown thumbprint, multi-entry
  precedence, round-trip).
- **Admin TOTP enrollment + MFA-gated login (T-SEC-1)**: two new self-service endpoints under
  `/api/admin/mfa` (`AnyAdmin` policy, actor id always the caller's own token `sub`) —
  `POST /enroll` generates a TOTP secret (Otp.NET, RFC 6238 defaults) and stores it AES-256-GCM
  encrypted but unconfirmed; `POST /verify` proves the authenticator app has it with a live code,
  flips `AdminUser.MfaEnabled` on, and issues 10 single-use recovery codes (CSPRNG,
  Argon2id-hashed, shown once). Once enabled, the `urn:sheba:grant:admin_password` OIDC grant
  requires a valid `mfa_code` (live TOTP or an unused recovery code) alongside the password —
  missing/wrong codes fail with distinguishable JSend keys (`mfa_required` vs `mfa`) so a client
  can re-prompt; five consecutive invalid codes lock the second factor for `2^(n-4)` minutes,
  mirroring the existing password-lockout formula (BR-LG-3). Admins who haven't enrolled keep the
  password-only baseline — enrollment requires an authenticated admin token, so it can't be a
  login precondition. New `AdminRecoveryCode` entity + `admin_recovery_codes` table
  (migration `AddAdminMfaSupport`); new `IMfaSecretEncryptor`/`ITotpService` ports in
  `Sheba.Identity.Domain.Interfaces` with AES-GCM/Otp.NET implementations in
  Identity.Infrastructure. Covered by 33 new unit tests: `AdminUser`/`AdminRecoveryCode` domain
  state machine, `LoginAdminHandler`'s MFA gate (all pass/fail/lockout branches),
  `EnrollAdminMfaHandler`/`ConfirmAdminMfaHandler`, and AES-GCM round-trip/tamper +
  Otp.NET service round-trip coverage. See [sheba.md
  §6.9](docs/sheba.md#69-admin-totp-enrollment--mfa-gate-t-sec-1) for the full enrollment +
  login sequence.
- **Module boundaries restored (T-ARC-1)**: every one of the 10 modules' `.csproj` files now
  references only `Sheba.Shared.Kernel` (plus its own Domain/Application layer) — zero
  cross-module `ProjectReference`s remain anywhere in the solution, verified by a full sweep of
  all 30 module project files. Specifically:
  - **Integration events relocated**: `IdentityRequestDecidedEvent`,
    `ServiceRequestSubmittedEvent`, and `ServiceRequestCompletedEvent` — the three event types
    with cross-module subscribers — moved from their producer's `*.Domain/DomainEvents` into
    `Sheba.Shared.Kernel.Events.IntegrationEvents`. `AccountRegisteredEvent`,
    `IdentityRequestSubmittedEvent`, and `WorkflowStepCompletedEvent` have no cross-module
    subscriber today and stay put — they move only if/when one gains one.
  - **Wallet.Application → Identity fixed**: `IssueIdentityCredentialHandler` now resolves the
    citizen's verified identity claims via `ICitizenAccountQueryService` (an existing
    Shared.Kernel port, already implemented by Identity but previously unused) instead of
    dispatching `GetAccountByIdQuery` directly through `Sheba.Identity.Application`.
    `IssueCredentialOnApprovalHandler` subscribes to the relocated event. The
    `Sheba.Identity.Application` project reference is gone from `Sheba.Wallet.Application.csproj`.
  - **Admin.Application → Identity.Domain/ServiceRequest.Domain fixed**:
    `OnIdentityRequestDecidedHandler`, `OnServiceRequestSubmittedHandler`, and
    `OnServiceRequestCompletedHandler` now subscribe to the relocated Shared.Kernel event types.
    Both illegal `ProjectReference`s are gone from `Sheba.Admin.Application.csproj`.
  - **New `IPaymentOrderPort` (Shared.Kernel)**: create/get/mark-paid over a read-only
    `PaymentOrderInfo` DTO and a Shared.Kernel-mirrored `PaymentOrderStatus` enum — no raw
    `PaymentOrder` entity crosses the boundary. Implemented by `PaymentOrderPortAdapter` in
    Payment.Infrastructure. `PaymentStepHandler` and `MarkPaymentCompleteHandler` in ServiceRequest
    now depend only on this port; `Sheba.Payment.Domain`/`Infrastructure` references are gone from
    ServiceRequest's projects.
  - **New `IMinistryCallPort` (Shared.Kernel)**: a single `InvokeAsync(endpointId, citizenId,
    requestBodyJson)` call that does the entire endpoint lookup → auth-config/credential lookup →
    authenticate → send, *inside* Ministry.Infrastructure (`MinistryCallPortAdapter`) — decrypted
    `MinistryAuthCredential` material never leaves the Ministry module. `MinistryCallStepHandler`
    in ServiceRequest shrank from ~120 lines of endpoint/credential/HTTP plumbing to a single port
    call plus JSON result formatting.
  - **`IMinistryWebhookVerifier` relocated**: moved from `Sheba.Ministry.Domain.Interfaces` to
    `Sheba.Shared.Kernel.Interfaces` (it was already narrow and read-only-shaped — the right
    exception, just declared in the wrong assembly). `HandleWebhookCallbackHandler` in
    ServiceRequest now depends on the Shared.Kernel copy.
  - **Two dead `ProjectReference`s removed**: `Sheba.ServiceRequest.Infrastructure` referenced
    `Sheba.Ministry.Infrastructure` and `Sheba.Payment.Infrastructure` with zero actual type usage
    (confirmed by grep) — vestigial, deleted.
  - Verified live end-to-end against Postgres/Redis: registered and approved a citizen — the
    relocated `IdentityRequestDecidedEvent` correctly fanned out through the outbox to both
    Wallet (issued a `DigitalIdentityCredential` with the right subject DID, proving the
    `ICitizenAccountQueryService` swap works) and Admin (`[AdminAnalytics] ... APPROVED` snapshot
    update); submitted a service request as that citizen — the relocated
    `ServiceRequestSubmittedEvent` correctly reached Admin's analytics handler. The
    `IPaymentOrderPort`/`IMinistryCallPort` DI wiring is validated by ASP.NET Core's
    `ValidateOnBuild` (Development) succeeding at startup — a misregistration throws immediately,
    as seen earlier in this same change set with the MFA services fix below — and by the passing
    `Sheba.ServiceRequest.Tests` suite; the seeded demo services have no configured workflow steps
    (a pre-existing gap, T-SRV-3/T-SRV-4, unrelated to this change) so the Payment/Ministry step
    handlers weren't reachable via a full live HTTP round-trip in this pass.
- **Authorization coverage completed (T-AUTH-2)**: every mapped route group now carries either
  `.RequireAuthorization(...)` or a justified `.AllowAnonymous()` (verified live: an audit sweep
  of every `MapGroup` in the solution found exactly one gap beyond the three named in the task —
  the Document module — all now fixed):
  - **Wallet** (`WalletModule.cs`): `/api/wallet` split into a `CitizenOnly` group (`GET
    /credentials` — the caller's own, resolved from the token `sub`, not a caller-supplied
    `citizenId` as before) and a new `/api/admin/wallet` `IdentityReviewer` group for the manual
    "force-issue a credential for any account" escape hatch, which must never share a route or
    policy with citizen self-service endpoints.
  - **Admin** (`AdminEndpoints.cs`): `/api/admin` (KPIs, trends, PDF/Excel/CSV reports) now
    requires the `AnyAdmin` policy — previously fully anonymous.
  - **Audit** (`AuditModule.cs`): `/api/admin/audit` now requires the `Auditor` policy (SuperAdmin
    or Auditor) — previously fully anonymous.
  - **Document** (`DocumentModule.cs`, found during the sweep, not in the original task list):
    `POST /api/documents`, `GET /api/documents/mine/{ownerId}`, and `DELETE
    /api/documents/{id}` had **no authorization at all**, and the first two took a
    caller-supplied `ownerId`/`ownerId` route param with no ownership check — any anonymous
    caller could upload a document under any citizen's identity, list any citizen's document
    metadata, or **delete any document in the system by id**. Fixed: the whole `/api/documents`
    group now requires authentication; `ownerId` for upload/list is always the token `sub` (the
    route param was removed — `GET /mine/{ownerId}` → `GET /mine`); `DeleteDocumentCommand` gained
    `ActorId`/`IsAdmin` and `DeleteDocumentHandler` now enforces BR-DO-1 (owner-or-admin,
    `NotFoundException` for everyone else — same anti-enumeration shape as the existing
    `GetDocumentDownloadUrlHandler`).
  - Verified live: all 7 previously-open endpoints now return 401 for anonymous callers; a
    SuperAdmin token gets 200 on `/api/admin/analytics/kpis` and `/api/admin/audit` but 403 on the
    `CitizenOnly` `/api/wallet/credentials`, proving the role-mismatch direction also works.
- **Audit logging activated (T-AUD-4)**: `AuditLoggingBehavior` is now registered in the MediatR
  pipeline (`Program.cs`, `IHttpContextAccessor` added) as the innermost behavior — after
  `TransactionBehavior` — so it intercepts every `*Command` across all 10 modules, takes the actor
  from the JWT `sub` claim (`Guid.Empty` for anonymous calls), and writes a row to
  `audit.audit_events` on its own `AuditDbContext` (cross-schema, so intentionally outside the
  business transaction). Request/response snapshots go through a new
  `AuditSnapshotRedactor` (`Sheba.Audit.Infrastructure/Behaviors/`) — a redaction *allowlist*, not
  a denylist: only property names on an explicit safe list (entity ids, enums, booleans,
  non-secret scalars) are written verbatim; everything else — passwords, OTP/TOTP codes, national
  IDs, phone numbers, recovery codes, tokens, and any future command field nobody has reviewed
  yet — is replaced with `"[REDACTED]"` by default, per the repo's no-PII-logging rule. The
  allowlist also treats `Sheba.Shared.Kernel.Results.Result<T>`'s wrapper shape
  (`value`/`isSuccess`/`isFailure`/`error`) as structural passthrough keys so the safe fields
  nested inside a T-STD-1 module's `Result<T>` response still surface instead of the whole
  envelope being blanket-redacted. New `Sheba.Audit.Tests` project (11 tests): redactor coverage
  (password/OTP/NID/TOTP/recovery-code redaction, unknown-field-redacted-by-default, safe-field
  passthrough, `Result<T>`-wrapper passthrough) plus `AuditLoggingBehavior` coverage (actor from
  `sub`, no audit row for queries, failure path writes a `Succeeded = false` row and re-throws,
  anonymous caller gets `ActorId = Guid.Empty`). Verified live against Postgres: a citizen
  registration wrote an `audit_events` row with `request_snapshot` containing no national ID or
  phone number, and a forced unique-constraint failure on a second registration wrote a
  `Succeeded = false` row with a safe generic error message — no data leaked on either path.
- **CORS + correlation-ID middleware (T-GW-1)**: `CorrelationIdMiddleware` assigns every request a
  correlation id — reusing an inbound `X-Correlation-Id` header if the caller sent one, otherwise
  minting one from `HttpContext.TraceIdentifier` — pushes it into the Serilog scope (so
  `UseSerilogRequestLogging`'s message template and every log line for the request carry it), and
  echoes it on the response. It's registered right after `ExceptionHandlerMiddleware` so the id is
  available to the 500 handler's `data.correlation_id` field even when the exception originates
  deep in the pipeline (`ExceptionHandlerMiddleware` updated to read it via
  `CorrelationIdMiddleware.GetCorrelationId` instead of the raw trace identifier). A new
  `AddShebaCors`/`ShebaSpa` named CORS policy reads `Cors:AllowedOrigins` from config — no
  wildcard, since bearer tokens ride in headers set by JS callers — and is wired into the pipeline
  after the rate limiter and before authentication, matching docs/sheba.md §3.5. An empty/missing
  `Cors:AllowedOrigins` blocks all cross-origin calls (safe default); `appsettings.Development.json`
  seeds `http://localhost:3000` and `http://localhost:5173` for local SPA dev. Verified live: a
  configured origin gets `Access-Control-Allow-Origin`/`-Methods`/`-Headers` on an OPTIONS
  preflight and an unlisted origin gets none (browser blocks it); `X-Correlation-Id` round-trips
  both when the caller supplies one and when the server mints one, and appears in the request's
  log line.
- **Housekeeping (T-STD-2)**: deleted the stray `Admin.Domain/Entities/AdminUser.cs` stub
  (Identity.Domain owns the real, EF-mapped `AdminUser`) and the three empty `Class1.cs`
  template stubs left over in `Modules/Audit` (Application/Domain/Infrastructure). Made the
  `refresh_token` grant path in `OidcEndpoints` fully async — `IssueFromRefreshTokenAsync` now
  awaits `HttpContext.AuthenticateAsync` instead of blocking on `.GetAwaiter().GetResult()`,
  removing a sync-over-async hazard on a hot token-issuance path. Build and full test suite
  (52 tests across Ministry/ServiceRequest/Identity/Integration) verified green.
- **Webhook replay hardening (T-SRV-1)**: `MinistryWebhookVerifier` now gates every ministry
  callback on three checks in order — constant-time HMAC-SHA256 signature, a ±5-minute
  `X-Sheba-Timestamp` window, and `X-Sheba-Delivery-Id` dedup (Redis `SET NX`, fail-open on dedup
  only if Redis is unreachable — signature + timestamp still gate the callback). Rejections are
  logged as structured warnings (ministry id, status, reason — never the raw body or secret) for
  alerting. Covered by 7 unit tests (`MinistryWebhookVerifierTests`): valid delivery, tampered
  body, wrong secret, stale timestamp, replayed delivery id, no active webhook, missing headers.
- **Server-side JSON Schema validation for service form submissions (T-SRV-2)**:
  `SubmitServiceRequestValidator` (FluentValidation) loads the submitted service's
  `ServiceFormSchema.FormSchemaJson` and validates `FormDataJson` against it with `JsonSchema.Net`
  before the handler creates the request aggregate. Malformed JSON and schema violations both
  render as JSend `fail` with per-field keys (e.g. `{"formData.fullNameEn": "...", "formData":
  "..."}` for root-level errors); a service published without a form schema (workflow-steps-only
  services are valid) passes through unvalidated, matching existing `Publish()` rules. Covered by
  6 new unit tests in `SubmitServiceRequestValidatorTests`. `JsonSchema.Net` added to
  `Directory.Packages.props`.
- **JSend envelopes on every REST endpoint (T-API-1)**: `JSendResponse<T>` + `JSend` factories in
  `Sheba.Shared.Kernel/Responses/`, a `JSendWrappingFilter` on every module route group, JSend
  `fail`/`error` mapping in the global exception middleware (with correlation ids on 500s and no
  stack traces), JSend bodies on bare 401/403 challenges, and a Swagger operation filter that
  documents the envelope. OIDC protocol routes (`/connect/*`, `/.well-known/*`), Hangfire, and
  Swagger stay exempt per spec.
- **`Result<T>` for expected failures, adopted in Identity (T-STD-1)**: `Sheba.Shared.Kernel.Results`
  adds `Result`/`Result<T>` + `Error`/`ErrorType`, plus `ResultHttpExtensions.ToHttpResult()`
  which renders a `Result<T>` as the same JSend shape/HTTP status the exception middleware already
  produces for the equivalent exception type — so converting a handler never changes the wire
  contract. All 12 Identity command/query handlers now return `Result<T>` instead of throwing
  `DomainException`/`NotFoundException` or returning ad-hoc `{succeeded, message}` DTOs; the six
  citizen-facing REST endpoints and both OIDC-grant consumers (`OidcEndpoints.IssueCitizenTokenAsync`,
  `IssueAdminTokenAsync`) were updated to match. One deliberate shape fix: the four handlers that
  used to return the whole `{succeeded, message}` DTO as `fail` data (`VerifyOtp`,
  `VerifyLoginOtp`, `LoginCitizen`, `VerifyEmail`) now use the same field-keyed `fail` shape as
  every other endpoint (e.g. `{"otp": "Invalid code. 2 attempt(s) remaining."}` at 400, same as
  before). Verified live end-to-end: registration anti-enumeration (BR-ON-3), OTP wrong/correct
  codes, login anti-enumeration, admin token issuance (success + wrong-password OAuth error), and
  account lookup (200 + 404) all produce identical status codes and JSend shapes to the
  pre-conversion exception-based behavior. Other modules remain exception-based; adopt the same
  way, one module per pass.
- **Rate limiting on auth-sensitive endpoints (T-SEC-2)**: a custom `RedisSlidingWindowRateLimiter`
  (sorted-set sliding-window-log, atomic via a Lua script) backs named ASP.NET Core `RateLimiter`
  policies on `/api/identity/register` (5/5 min), `/api/identity/login` +
  `/api/identity/login/verify-otp` (10/5 min), `/api/identity/verify-otp` (10/5 min), and
  `/connect/token` (30/min) — all partitioned by caller IP. An in-memory global limiter
  (300/min per IP) covers every other route as a sane default. 429 responses render as JSend
  `fail` (`{"rate_limit": "..."}`) with a `Retry-After` header everywhere except `/connect/*`,
  which gets an OAuth-shaped `{"error":"slow_down", ...}` body to match the OIDC wire format.
  Verified live against Redis + Postgres: flooding `/api/identity/register` returns 429 on the
  6th request within the window; flooding `/connect/token` returns the OAuth-shaped 429 on the
  31st request.
- **Durable events: outbox, Hangfire dispatcher, consumer inbox (T-EVT-1)**: `OutboxMessage` /
  `InboxMessage` moved to `Sheba.Shared.Kernel.Outbox`, mapped into every module's own schema.
  `OutboxSaveChangesInterceptor` converts raised domain events into outbox rows in the same
  `SaveChanges` call as the aggregate write (replacing the old in-process publish-at-SaveChanges
  pattern, which had no durability and, outside Identity, wasn't wired up at all). A single
  Hangfire recurring job (`OutboxDispatcherJob`) polls every module's outbox table, publishes
  pending/retryable rows via MediatR, and marks them published/failed/dead-lettered with
  exponential backoff. `EfUnitOfWork<TContext>` and `EfInboxGuard<TContext>` are now registered
  per module — `IUnitOfWork` was previously unregistered everywhere, so `TransactionBehavior`
  silently ran without a transaction. The six existing cross-module notification handlers
  (Wallet's credential issuance, Admin's two analytics handlers, Identity's three email handlers)
  now guard themselves with `IInboxGuard` against at-least-once redelivery. Also fixed a
  correctness bug the outbox surfaced: the six domain event records exposed `EventId`/`OccurredAt`
  as get-only with no setter, so JSON deserialization minted a new `EventId` instead of restoring
  the original — changed to `init` accessors so inbox idempotency keys survive the round-trip.
  Verified end-to-end against a clean-volume database: a registration event was written to the
  outbox and picked up, published, and marked published by the dispatcher within one poll cycle.
- **EF Core migrations for all ten contexts (T-DB-1)**: `InitialCreate` migrations added for the
  nine contexts that previously relied on `EnsureCreated()` (Citizen, Ministry, ServiceRequest,
  Document, Wallet, Payment, Notification, Audit, Admin); the `EnsureCreated()` fallback is
  removed from `MigrationExtensions`, so `MigrateAllModulesAsync` now applies real migrations for
  every module and a context without one fails loudly instead of silently no-oping. Verified with
  a clean-volume run: fresh Postgres container, all ten contexts migrate and seed successfully.
- Master architecture & implementation plan [docs/sheba.md](docs/sheba.md) (single source of
  truth) with requirements-traceability table, STRIDE threat model, JSend API standard, and
  extraction roadmap.
- Focused doc set: architecture, coding-standards, business-rules, database-design (all 10
  bounded-context ERDs), api-contract (JSend envelopes + endpoint catalog), security,
  performance, testing, roadmap, known-issues.
- Mermaid diagram sources under `docs/diagrams/` (architecture overview, onboarding state
  machine, login+OTP sequence, ministry integration flow, service-request lifecycle, 10 ERDs).
- Root project files: README (quickstart + seeded fixtures), CLAUDE.md and AGENTS.md (AI
  contributor rules), TASKS.md (phased backlog with T-XXX ids).

### Fixed
- Added `Sheba.Audit.Application` to `Program.cs`'s MediatR assembly-scan list. Every other
  module with Application-layer handlers was already listed (or self-registers, as Identity
  does); Audit's was never added, so `GetAuditLogHandler` had no registered
  `IRequestHandler<GetAuditLogQuery, GetAuditLogResponse>` and `GET /api/admin/audit` has 500'd
  since the endpoint was written — found while live-verifying T-AUTH-2's new `Auditor` policy on
  that same route. Verified live: the endpoint now returns 200 with paginated audit rows for a
  SuperAdmin token.
- Added the missing `AddAdminMfaSupport` EF Core migration for `IdentityDbContext` (the
  `AdminRecoveryCode` entity and the `AdminUser.Mfa*` columns from the in-progress T-SEC-1 work
  had no corresponding migration). Without it, `IdentityDbContext.Database.MigrateAsync()` failed
  its `PendingModelChangesWarning` check on *every* startup — and because
  `MigrationExtensions.MigrateAllModulesAsync` iterates all ten module contexts in one loop and
  re-throws on the first failure, this single unmigrated context aborted migrations for **every**
  module, not just Identity. On a clean-volume `docker compose up`, no module's schema was ever
  provisioned. Verified live: all ten contexts now migrate successfully on a fresh volume.
- Registered `IMfaSecretEncryptor` → `AesGcmMfaSecretEncryptor` and `ITotpService` →
  `OtpNetTotpService` in `IdentityModule`'s DI container. Both adapters (and the
  `EnrollAdminMfa`/`ConfirmAdminMfa`/`LoginAdmin` handlers that depend on them, part of the
  in-progress T-SEC-1 admin-MFA work) already existed in the tree but weren't wired up, so
  `WebApplication.Build()` failed service-provider validation and the API could not start at all
  in Development. This is a DI-registration fix only — T-SEC-1 itself (enrollment/confirmation
  endpoints, migration for `AdminRecoveryCode`) remains open.

### Changed
- Credential-vault encryption standard confirmed as **AES-256-GCM**, superseding the old
  ADR-011 (RSA-OAEP) — see [docs/security.md](docs/security.md) §3.
- API response standard set to **JSend**; the RFC 7807 ProblemDetails responses have been
  replaced by JSend envelopes (retrofit T-API-1, see Added above).

### Deprecated
- `docs/SHEBA_ARCHITECTURE.md` moved to [docs/archive/](docs/archive/) — historical reference
  only; superseded by [docs/sheba.md](docs/sheba.md).

[Unreleased]: https://example.invalid/sheba/compare/HEAD
