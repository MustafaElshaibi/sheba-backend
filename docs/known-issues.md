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
| T-NOT-1 | `Notification`/`NotificationTemplate` entities are TODO stubs; sends are hardcoded strings | No bilingual templating | `Modules/Notification` |
| T-ADM-1 | No projection rebuild for the BI read model | Lost/buggy projection = manual SQL repair | `Modules/Admin` |
| T-TST-1..4 | Only ~2 real test classes (Identity); Ministry/ServiceRequest/Integration projects are placeholders | Regression safety near zero outside Identity | `tests/` |
| T-INT-1/2 | No OpenCRVS adapter; no OTP provider failover | Single-provider risk | Identity adapters |
| — | `EfUnitOfWork<T>` is now registered per module (T-EVT-1), but zero commands implement `ITransactionalCommand` yet | `TransactionBehavior` has a real `IUnitOfWork` to wrap with, but nothing opts in, so no handler runs inside an explicit transaction today | `Sheba.Api/Behaviors/TransactionBehavior.cs` |
| — | **Low** — `Sheba.Citizen.Infrastructure.Adapters.CitizenQueryAdapter` implements `ICitizenAccountQueryService` by reading `CitizenProfiles` but is never registered in DI; `Identity.Infrastructure.CitizenAccountQueryAdapter` (reading `IdentityDbContext` directly) is the one actually wired and in use. Now that T-CIT-1 populates `CitizenProfiles`, the Citizen-owned adapter could take over per the architecture's stated intent ("Citizen module provides the implementation") — not done here to avoid an untested swap of a working cross-module dependency | Dead code; two implementations of the same port, only one live | `Citizen.Infrastructure/Adapters/CitizenQueryAdapter.cs` |
| T-SRV-3 (residual) | **Low** — JSON-Logic **eligibility-rules** evaluation is still not wired: `ServiceDefinition.EligibilityRulesJson` is stored but not evaluated at submit. The rest of T-SRV-3 is done (LoA gate, required-documents gate, transition guards, cancel endpoint, SLA sweep). Needs a JSON-Logic evaluator dependency — deferred as its own follow-up rather than pulling a library in under this task | Services relying on declarative eligibility rules are not gated on them (LoA + required-docs still enforced) | `SubmitServiceRequestHandler`, `ServiceDefinition.EligibilityRulesJson` |
| T-NOT-2 | **Medium** — Notification module owns no event consumers: identity emails are sent by handlers inside Identity.Application (§5.8 assigns them to Notification); no `ServiceRequestSubmitted`/`Completed` notifications exist at all; `NotificationRecord` is never written; `IEmailService`/`ISmsService` are duplicated in Notification.Domain | Citizens get no service-request notifications; no delivery log | `Modules/Notification`, `Identity.Application/EventHandlers` |
| T-WAL-1 | **Medium** — VC signing key is ephemeral when `Wallet:IssuerPrivateKeyPem` is unset: a fresh RSA key is generated per process, so previously issued credentials become unverifiable after a restart; no production fail-fast | Issued VCs silently invalidated | `RsaCredentialSigner.cs` |
| T-OIDC-3 | **Medium** — `DELETE /api/admin/relying-parties/{clientId}` throws 500: `OpenIddict.EntityFrameworkCore` 5.7.0's `ApplicationStore.DeleteAsync` calls `RelationalQueryableExtensions.ExecuteDeleteAsync`, which EF Core 9 moved to `EntityFrameworkQueryableExtensions` — a binary-compatibility gap between the pinned OpenIddict (compiled against EF Core 8's API surface) and EF Core 9.0.6 at runtime (`MissingMethodException`). Found live while testing the new rotate-secret endpoint (T-OIDC-1); GET/POST/rotate-secret on the same `IOpenIddictApplicationManager` are unaffected. **Researched, not executed**: OpenIddict 6.x is the fix — it targets net9.0/EF Core 9 natively, and its 5→6 migration guide has no breaking changes that touch anything this repo uses (no cryptography/device endpoint URIs, no `Prompts` usage, no custom store overrides). Deliberately did **not** jump to 7.x: that migration adds mandatory `RegisterAudiences()`/`RegisterResources()` calls (token exchange fails without them), removes DbContext auto-registration, and requires a DB migration for a widened `Type` column — real risk for a fix this narrow. Deliberately did **not** bump to 6.x either in this pass: it's a `Directory.Packages.props` pin change affecting every OIDC/OAuth flow in the app (the entire auth system), too broad a blast radius to execute without explicit sign-off for what only blocks one admin route | An admin cannot remove a registered relying party via the API | `RelyingPartyEndpoints.cs`, `Directory.Packages.props` (bump `OpenIddict.AspNetCore`/`OpenIddict.EntityFrameworkCore` to `6.4.0`) |
| — | **Low** — No global `JsonStringEnumConverter`: minimal-API JSON request bodies with an enum property (e.g. `POST /api/admin/admin-users`'s `role`) only accept the numeric value, not the string name, even though responses often serialize enums as strings (`.ToString()` calls in handlers). Found live while creating a test MinistryManager for T-AUTH-1 verification. Fixing it globally risks changing every module's existing JSON shape without a full audit, so deliberately not fixed here — no task ID assigned pending a decision on scope (global vs. per-endpoint) | Admin UIs must send `"role": 3`, not `"role": "MinistryManager"`, until decided | `Program.cs` (no `AddJsonOptions`/`Configure<JsonOptions>` exists yet) |

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
   whole family the moment a stale generation is presented. **Extended to the browser flow
   (T-OIDC-2):** `AuthorizeEndpoints.IssueAuthorizationCode` now attaches the same claims, so
   `/connect/authorize`-issued refresh tokens get identical treatment to the two custom grants.
   Confirmed live: a *sequential* replay of the exact same already-rotated refresh token never
   reaches `RotateRefreshTokenFamilyHandler` at all — OpenIddict's own token store rejects it
   first (`ID2012`, "already been redeemed"), before our code runs. The family-generation check
   exists for the case that short-circuit doesn't cover: a genuine race where two requests both
   pass OpenIddict's redemption check for the same predecessor token before either commits (the
   RFC 9700 threat model), which sequential single-request testing can't reproduce.
6. **Push notifications** — brief lists email/SMS/push; push deferred until a mobile app exists.

## 4. Documentation notes

- Spec/RFC citations in this doc set (OAuth 2.1 draft, RFC 9700, OIDC Core, W3C VC 2.0, JSend)
  are from the authors' knowledge of the stable published specs; they were not re-fetched at
  writing time. Verify versions when implementing security-critical details.
- The archived [SHEBA_ARCHITECTURE.md](archive/SHEBA_ARCHITECTURE.md) remains for history; where
  it conflicts with [sheba.md](sheba.md), sheba.md wins (notably: JSend, AES-GCM, single-track
  monolith design).
