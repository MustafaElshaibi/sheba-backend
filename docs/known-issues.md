# Known Issues, Open Risks & Deferred Decisions

> Companion to [sheba.md](sheba.md) (target design) and [TASKS.md](../TASKS.md) (actionable
> backlog). This file is the honest ledger of where the code diverges from the design and which
> decisions remain open. Update it whenever a gap closes or a new one is found.

## 1. Design-vs-code gaps (each has a TASKS.md item)

| ID | Gap | Impact | Where |
|----|-----|--------|-------|
| T-API-1 | No JSend envelopes — endpoints return raw DTOs; errors are RFC 7807 ProblemDetails | API consumers get inconsistent shapes vs. the contract in [api-contract.md](api-contract.md) | `Sheba.Api/Middleware/ExceptionHandlerMiddleware.cs`, all module endpoint groups |
| T-EVT-1 | Outbox is a dead entity: exists only in Identity, **no dispatcher drains it**; events dispatch in-process at SaveChanges with no durability | Crash between commit and publish silently loses events (e.g. approval email/VC issuance) | `Identity.Domain/Entities/OutboxMessage.cs`; no consumer inbox anywhere |
| T-DB-1 | Only Identity has EF migrations; 9 contexts rely on `EnsureCreated()` fallback | Schema drift; no upgrade path once real data exists | `Sheba.Api/Extensions/MigrationExtensions.cs` |
| T-SEC-1 | Admin TOTP modeled (`mfa_secret`) but not enforced at login | Admin accounts are password-only | Identity module |
| T-SEC-2 | No rate limiting anywhere (no `AddRateLimiter`) | OTP flooding, credential stuffing, enumeration pressure | `Sheba.Api/Program.cs` |
| T-SEC-3 | Secrets via config/env; AES vault key has a derived dev fallback; dev signing certs even on production branch | Key compromise + no rotation story in practice | `IdentityModule.cs`, `AesGcmCredentialEncryptor.cs` |
| T-SEC-4 | No signing-cert rotation procedure wired | JWKS rollover untested | Identity module |
| T-SEC-6/7 | No DB-volume/column encryption for `form_data_json`; MinIO SSE off (T-DOC-2) | PII at rest relies on host security only | Postgres/MinIO deployment |
| T-SRV-1 | Webhook verification lacks timestamp window + delivery-id dedup (HMAC only, partially) | Replay attacks possible on ministry callbacks | ServiceRequest webhook receiver |
| T-SRV-2 | `JsonSchema.Net` referenced in old doc but **not in `Directory.Packages.props`** — dynamic form submissions are not schema-validated server-side | Invalid/malicious form payloads reach handlers | ServiceRequest |
| T-PAY-1 | Payment Application layer is **empty** (entities + repo only; mock flow) | No real order/refund lifecycle; workflow coupled via direct command instead of `PaymentCompletedEvent` | `Modules/Payment` |
| T-AUD-1..3 | Audit is a plain table: no INSERT-only grant, no hash chain, no partitioning | "Tamper-evident" is aspirational today | `Modules/Audit` |
| T-AUTH-1 | Ministry-Admin per-ministry scoping only partially enforced | A MinistryManager could touch another ministry's data via some endpoints | Ministry/ServiceRequest admin endpoints |
| T-STD-1 | No `Result<T>`; expected failures flow as exceptions | Works, but exception-as-control-flow on hot validation paths | Shared.Kernel |
| T-NOT-1 | `Notification`/`NotificationTemplate` entities are TODO stubs; sends are hardcoded strings | No bilingual templating | `Modules/Notification` |
| T-ADM-1 | No projection rebuild for the BI read model | Lost/buggy projection = manual SQL repair | `Modules/Admin` |
| T-TST-1..4 | Only ~2 real test classes (Identity); Ministry/ServiceRequest/Integration projects are placeholders | Regression safety near zero outside Identity | `tests/` |
| T-INT-1/2 | No OpenCRVS adapter; no OTP provider failover | Single-provider risk | Identity adapters |
| — | Leftover `Class1.cs` stubs in Citizen module | Cosmetic | `Modules/Citizen` |
| — | `TransactionBehavior` warns and proceeds without `IUnitOfWork` (none registered); only 2 commands marked `ITransactionalCommand` | Multi-write handlers aren't atomic with their outbox rows — folded into T-EVT-1 | `Sheba.Api/Behaviors` |

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
5. **Refresh-token custom family table vs OpenIddict-native tracking** — `RefreshTokenFamily`
   duplicates part of what OpenIddict stores; decide single source when implementing T-SEC-4.
6. **Push notifications** — brief lists email/SMS/push; push deferred until a mobile app exists.

## 4. Documentation notes

- Spec/RFC citations in this doc set (OAuth 2.1 draft, RFC 9700, OIDC Core, W3C VC 2.0, JSend)
  are from the authors' knowledge of the stable published specs; they were not re-fetched at
  writing time. Verify versions when implementing security-critical details.
- The archived [SHEBA_ARCHITECTURE.md](archive/SHEBA_ARCHITECTURE.md) remains for history; where
  it conflicts with [sheba.md](sheba.md), sheba.md wins (notably: JSend, AES-GCM, single-track
  monolith design).
