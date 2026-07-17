# Known Issues, Open Risks & Deferred Decisions

> Companion to [sheba.md](sheba.md) (target design) and [TASKS.md](../TASKS.md) (actionable
> backlog). This file is the honest ledger of where the code diverges from the design and which
> decisions remain open. Update it whenever a gap closes or a new one is found.

## 1. Design-vs-code gaps (each has a TASKS.md item)

Severity (2026-07 code audit onward): **Critical** (exploitable/blocking now) · **High** ·
**Medium** · **Low**. Unmarked rows predate the classification.

| ID | Gap | Impact | Where |
|----|-----|--------|-------|
| T-SEC-3 | Secrets via config/env; AES vault key has a derived dev fallback; dev signing certs even on production branch | Key compromise + no rotation story in practice | `IdentityModule.cs`, `AesGcmCredentialEncryptor.cs` |
| T-SEC-6/7 | No DB-volume/column encryption for `form_data_json`; MinIO SSE off (T-DOC-2) | PII at rest relies on host security only | Postgres/MinIO deployment |
| T-PAY-1 | Payment Application layer is **empty** (entities + repo only; mock flow) | No real order/refund lifecycle; workflow coupled via direct command instead of `PaymentCompletedEvent` | `Modules/Payment` |
| T-AUD-1..3 | Audit is a plain table: no INSERT-only grant, no hash chain, no partitioning | "Tamper-evident" is aspirational today | `Modules/Audit` |
| T-AUTH-1 | Ministry-Admin per-ministry scoping not enforced (role policies exist on every group as of T-AUTH-2; the `ministry_id` ownership dimension does not) | A MinistryManager could touch another ministry's data via some endpoints | Ministry/ServiceRequest admin endpoints |
| T-NOT-1 | `Notification`/`NotificationTemplate` entities are TODO stubs; sends are hardcoded strings | No bilingual templating | `Modules/Notification` |
| T-ADM-1 | No projection rebuild for the BI read model | Lost/buggy projection = manual SQL repair | `Modules/Admin` |
| T-TST-1..4 | Only ~2 real test classes (Identity); Ministry/ServiceRequest/Integration projects are placeholders | Regression safety near zero outside Identity | `tests/` |
| T-INT-1/2 | No OpenCRVS adapter; no OTP provider failover | Single-provider risk | Identity adapters |
| — | `EfUnitOfWork<T>` is now registered per module (T-EVT-1), but zero commands implement `ITransactionalCommand` yet | `TransactionBehavior` has a real `IUnitOfWork` to wrap with, but nothing opts in, so no handler runs inside an explicit transaction today | `Sheba.Api/Behaviors/TransactionBehavior.cs` |
| T-CIT-1 | **High** — Citizen module never materializes: no consumer handles `IdentityRequestDecidedEvent(approved)` to create the `CitizenProfile` (§5.2), and `MapCitizenEndpoints` maps nothing (`/api/citizens` absent) | Approved citizens never get a profile; documented API missing | `Modules/Citizen` |
| T-ID-1 | **High** — Account lifecycle incomplete: no `Suspend`/`Reinstate`/`Deactivate` on the aggregate (§6.2 transitions and permission-matrix rows unimplementable); `Account.Reject` accepts a reason but never stores it; a Rejected or OTP-expired PendingVerification account permanently blocks its NID — re-registration hits the already-registered guard and no purge job or re-application path exists | Documented admin actions impossible; a citizen can be locked out of onboarding forever | `Account.cs`, `RegisterCitizenHandler`, missing purge job |
| T-SRV-3 | **Medium** — Submission gates missing: `RequiredLoa`, eligibility, and required documents are never checked at submit (§5.4.1; form-schema validation T-SRV-2 is done, these are not); `ServiceRequestEntity` lifecycle methods enforce no transition guards; no citizen cancel endpoint; no SLA/`Expired` handling | Ineligible or under-assured requests enter workflows; state machine corruptible | `SubmitServiceRequestHandler`, `ServiceRequestEntity` |
| T-SRV-4 | **Medium** — Step-engine gaps: `on_failure_step` is persisted but never consulted (§11.3 compensation unimplemented); unhandled step types fall through to an auto-complete handler and silently succeed; step executions are pre-created for every step at submit *and* created on demand in `ExecuteNextStep` (ambiguous "active step" resolution) | Misconfigured workflows complete without doing work; failure routing absent | `ExecuteNextStepHandler`, step handlers |
| T-NOT-2 | **Medium** — Notification module owns no event consumers: identity emails are sent by handlers inside Identity.Application (§5.8 assigns them to Notification); no `ServiceRequestSubmitted`/`Completed` notifications exist at all; `NotificationRecord` is never written; `IEmailService`/`ISmsService` are duplicated in Notification.Domain | Citizens get no service-request notifications; no delivery log | `Modules/Notification`, `Identity.Application/EventHandlers` |
| T-SEC-8 | **Medium** — OTP codes are generated inside `IOtpProvider.SendAsync` (which returns the raw code to the caller), contradicting §6.6's rule that generation/policy live in the application layer and providers only deliver | A provider swap can weaken OTP policy | `IOtpProvider.cs` + OTP adapters |
| T-WAL-1 | **Medium** — VC signing key is ephemeral when `Wallet:IssuerPrivateKeyPem` is unset: a fresh RSA key is generated per process, so previously issued credentials become unverifiable after a restart; no production fail-fast | Issued VCs silently invalidated | `RsaCredentialSigner.cs` |
| T-MIN-1 | **Low** — No ministry seed data: the demo catalog references five hardcoded ministry GUIDs that exist in no table; `MinistryEndpoint.RateLimitPerMinute` is stored but never enforced on outbound calls | Seeded ministry-call workflows cannot resolve a ministry; per-endpoint limits inert | `ServiceRequestModule.SeedServiceCatalogAsync`, `MinistryCallStepHandler` |
| T-OIDC-2 | **Low** — Refresh tokens issued via the browser `/connect/authorize` + PKCE flow (T-OIDC-1) don't get T-SEC-9 family-reuse tracking — only the two custom grants (`IssueCitizenTokenAsync`/`IssueAdminTokenAsync`) attach `family_id`/`family_generation` claims. An authorize-flow refresh token still rotates correctly (OpenIddict default) but a stolen one wouldn't trigger family-wide revocation | Reuse of an authorize-flow refresh token is only individually rejected, not cascaded | `AuthorizeEndpoints.IssueAuthorizationCode` |

## 2. Superseded decisions (do not resurrect)

| Old decision | Status | Replacement |
|--------------|--------|-------------|
| ADR-011: RSA-256/OAEP credential field encryption | **Superseded — design mistake** (key-wrap primitive misused for bulk field crypto) | AES-256-GCM ([security.md §3](security.md)) |
| RabbitMQ + MassTransit now | Deferred by design | In-process MediatR + outbox; broker arrives with first extraction ([sheba.md §11.4](sheba.md#114-transport-evolution)) |
| Saga framework for workflows | Rejected | Data-driven step engine (admin-authored workflows) |
| Materialized views for BI | Rejected | Event-fed read model ([sheba.md §12](sheba.md#12-dashboard--bi--reporting-backend)) |
| SAML relying-party support | Deferred | Enum reserved; OIDC covers planned integrations (assumption A8) |
| Per-module microservices at pilot scale | Rejected | Modular monolith with extraction seams |

## 3. Open decisions / questions

1. **Registry outage policy for onboarding** — fail-closed is designed; is a queued
   "retry my registration" UX acceptable to product?
2. **National SMS gateway** — no contract yet; Twilio adapter is the placeholder. Failover order
   and spend caps TBD (T-INT-2).
3. **LoA3 (biometric / in-person)** — in first production release or later? Affects
   `UpgradeLoa3` flow and appointment infrastructure.
4. **Retention numbers** — the 10-year post-closure snapshot retention is an assumption (A3), not
   a legal requirement; confirm when a data-protection regime lands.
5. **Refresh-token custom family table vs OpenIddict-native tracking** — **Decided (T-SEC-4):
   keep `RefreshTokenFamily`.** OpenIddict's built-in rotation rejects a *replayed* (already-
   redeemed) refresh token on its own, but that only blocks reuse of that one token — it doesn't
   cascade-revoke every token an attacker may have already minted further down the same chain
   before the legitimate reuse was detected. `RefreshTokenFamily` gives that cascade (§6.4's
   "revoke the entire family on replay" claim), which is the RFC 9700 guidance the design
   deliberately targets. **Implemented (T-SEC-9):** `OidcEndpoints` attaches an internal
   `family_generation` claim on every token response that grants `offline_access`;
   `RotateRefreshTokenFamilyHandler` checks it on every `refresh_token` grant and revokes the
   whole family the moment a stale generation is presented.
6. **Push notifications** — brief lists email/SMS/push; push deferred until a mobile app exists.

## 4. Documentation notes

- Spec/RFC citations in this doc set (OAuth 2.1 draft, RFC 9700, OIDC Core, W3C VC 2.0, JSend)
  are from the authors' knowledge of the stable published specs; they were not re-fetched at
  writing time. Verify versions when implementing security-critical details.
- The archived [SHEBA_ARCHITECTURE.md](archive/SHEBA_ARCHITECTURE.md) remains for history; where
  it conflicts with [sheba.md](sheba.md), sheba.md wins (notably: JSend, AES-GCM, single-track
  monolith design).
