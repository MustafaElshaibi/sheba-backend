# Changelog

All notable changes to Sheba are documented here. Format:
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning:
[SemVer](https://semver.org/) once releases begin.

## [Unreleased]

### Added
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

### Changed
- Credential-vault encryption standard confirmed as **AES-256-GCM**, superseding the old
  ADR-011 (RSA-OAEP) — see [docs/security.md](docs/security.md) §3.
- API response standard set to **JSend**; the RFC 7807 ProblemDetails responses have been
  replaced by JSend envelopes (retrofit T-API-1, see Added above).

### Deprecated
- `docs/SHEBA_ARCHITECTURE.md` moved to [docs/archive/](docs/archive/) — historical reference
  only; superseded by [docs/sheba.md](docs/sheba.md).

[Unreleased]: https://example.invalid/sheba/compare/HEAD
