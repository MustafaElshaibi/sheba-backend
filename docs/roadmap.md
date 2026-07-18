# Roadmap — Phased Milestones

> Extract of [sheba.md §16](sheba.md#16-implementation-roadmap); sheba.md wins conflicts.
> Task-level checklist: [TASKS.md](../TASKS.md). The build order respects module dependencies:
> Identity underpins everything; Ministry precedes ServiceRequest depth; durable events precede
> Notification/Admin/Wallet guarantees.

## Phase 0 — Harden the base *(complete)*

Everything here is prerequisite plumbing; no new features.

- ~~**T-API-1** JSend envelope: `JSendResponse<T>` + wrapping filter + exception-middleware swap +
  Swagger schema filter ([api-contract.md](api-contract.md)).~~ Done.
- ~~**T-DB-1** EF migrations for the 9 module contexts still on `EnsureCreated()`; ban the
  fallback.~~ Done.
- ~~**T-EVT-1** Outbox to Shared.Kernel + Hangfire dispatcher + per-module outbox tables + consumer
  inbox dedup ([sheba.md §11.1](sheba.md#111-transactional-outbox-the-reliability-backbone)).~~ Done.
- ~~**T-SEC-2** ASP.NET `RateLimiter` policies (auth endpoints strictest).~~ Done.
- ~~**T-STD-1** `Result<T>` in Shared.Kernel; adopt per module in single passes.~~ Done — adopted
  in Identity; remaining modules stay exception-based until their own pass.
- ~~Housekeeping: remove `Class1.cs` stubs (Citizen module)~~ Done; align README/quickstart still open.

2026-07 code-audit additions, also closed:

- ~~**T-STD-2** Housekeeping: stray `AdminUser` entity, Audit `Class1.cs` stubs, sync-over-async
  refresh-token grant.~~ Done.
- ~~**T-GW-1** CORS policy + correlation-ID middleware per §3.5.~~ Done.
- ~~**T-AUD-4** Activate `AuditLoggingBehavior` in the MediatR pipeline with a redaction
  allowlist.~~ Done.
- ~~**T-AUTH-2** Authorization coverage on every route group (Wallet/Admin/Audit, plus a
  previously-unguarded Document module gap found during the sweep); ownership from the JWT
  `sub`, never a caller-supplied id.~~ Done.
- ~~**T-ARC-1** Restore module boundaries: integration events relocated to Shared.Kernel;
  `IPaymentOrderPort`/`IMinistryCallPort`/`IMinistryWebhookVerifier` ports replace direct
  ServiceRequest→Payment/Ministry references; Admin/Wallet's illegal Domain refs removed.~~ Done
  — zero cross-module `ProjectReference`s remain anywhere in the solution.

**Exit:** all endpoints emit JSend; `docker compose up` from a clean volume migrates and seeds;
kill -9 during a command loses no events; auth endpoints rate-limited; every route group is
authorization-guarded; audit trail active with PII redaction; module boundaries compiler-enforced.

## Phase 1 — Identity completion

- ~~**T-SEC-1** Enforce admin TOTP at login (secret already modeled).~~ Done — self-service
  enrollment/confirmation endpoints, recovery codes, and MFA-gated login; admins who haven't
  enrolled keep the password-only baseline.
- ~~**T-SEC-4** Signing-cert rotation-by-overlap procedure + docs + drill.~~ Done — config-driven
  multi-cert loading (`SigningCertificateLoader`) + rotation runbook (security.md §4.1); decided
  to keep `RefreshTokenFamily` over OpenIddict-native tracking (known-issues §3.5), implementation
  is T-SEC-9. The runbook's live staging drill is still a pending pre-production checklist item.
- ~~Password reset flow (OTP-gated) & account recovery rules.~~ Done — BR-LG-7:
  `POST /api/identity/password-reset/request` + `/confirm`, registered-phone-only OTP, generic
  anti-enumeration responses on both steps, successful reset clears any active lockout.
- ~~RP management polish: secret rotation endpoint, per-RP consent screen copy.~~ Done —
  `POST /{clientId}/rotate-secret`; bilingual (Arabic/English) `/connect/consent` copy.
- ~~**T-AUTH-1** Ministry-Admin scoping claim + ownership policies end-to-end.~~ Done for
  `/api/ministry` + ServiceRequest admin routes; Admin/KPI ministry-slice filtering split out as
  T-AUTH-3 (was never in T-AUTH-1's literal TASKS.md scope).
- ~~**T-AUTH-3** Admin/KPI ministry-slice filtering.~~ Done — `GetKpiSummary`,
  `GetServiceRequestTrends`, and the service-request-based report generators take the caller's
  `ministry_id` claim; registration-based figures (not ministry-owned) stay global.

**Exit:** STRIDE rows in [sheba.md §13.5](sheba.md#135-stride-summary-auth-flows) all mitigated in
code, verified by contract tests.

## Phase 2 — Integration depth

- ~~**T-SRV-1** Webhook timestamp window + delivery-id dedup completing the HMAC check.~~ Done.
- ~~**T-SRV-2** Server-side JSON-Schema validation of service form submissions
  (JsonSchema.Net).~~ Done.
- **T-INT-1** OpenCRVS `INationalIdProvider` adapter (proves the second registry shape).
- **T-INT-2** OTP provider failover ordering.
- Ministry health dashboard wiring (`TestConnectionAsync` → Admin KPIs).

**Exit:** a stub external ministry round-trips a service request including an async webhook, with
replay attempts rejected.

## Phase 3 — Money & credentials

- **T-PAY-1** Payment application layer: CreateOrder / ConfirmPayment / Refund commands,
  `PaymentCompletedEvent` consumed by the workflow (replaces direct `MarkPaymentComplete`
  coupling), `IPaymentGateway` seam with mock gateway.
- Wallet: VC verification + presentation endpoints; revocation-status API.
- **T-NOT-1** Bilingual notification templates + template-keyed sends.

**Exit:** paid service completes end-to-end on events alone; a third party can verify a Sheba VC.

## Phase 4 — Audit, tests & scale-readiness

- **T-AUD-1..3** INSERT-only grant, hash-chain + verification job, monthly partitions.
- **T-ADM-1** BI projection rebuild (replay) command.
- **T-TST-1..4** Test debt to the bar in [testing.md](testing.md) (coverage floor 80 % on
  Domain/Application).
- **T-SEC-6/7** At-rest encryption: DB volume, `form_data_json` column crypto, MinIO SSE (T-DOC-2).
- Load test (k6/NBomber) against [performance.md §1](performance.md) targets.

**Exit:** tamper-evidence demonstrable to an auditor; perf targets met; CI green with coverage
gate.

## Phase 5 — Production migration *(post-pilot)*

- Real civil-registry adapter + contract tests against the live sandbox.
- Real SMS provider(s); MailHog → SMTP relay.
- TLS reverse proxy, Docker secrets/vault (T-SEC-3), backup + restore drill.
- Extraction dry run: Notification module to its own container + RabbitMQ
  ([sheba.md §3.4](sheba.md#34-migration-path-to-services)).

**Exit:** go-live checklist green; one module successfully running out-of-process proves the
seams.
