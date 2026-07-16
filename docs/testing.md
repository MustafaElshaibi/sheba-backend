# Testing Strategy

> Extract of [sheba.md](sheba.md) conventions; sheba.md wins conflicts.
> **Honest baseline:** today only `Sheba.Identity.Tests` has real coverage
> (`IdentityRequestDecisionHandlerTests`, `VerifyOtpHandlerTests`); the Ministry, ServiceRequest,
> and Integration test projects contain placeholders. Closing this is **T-TST-1..4**.

## 1. Stack

xUnit + FluentAssertions + NSubstitute; EF InMemory for pure handler units; **Testcontainers
(PostgreSQL, Redis, MinIO)** for integration tests; `WebApplicationFactory` for API-level tests.
Never live registries, SMS, or ministries in any test tier.

## 2. Test doubles are production code paths

The pluggability requirement pays off in tests — the same seams used in dev are used in CI:

- `MockNationalIdProvider` — 8 deterministic citizens covering every onboarding branch
  (4 valid; deceased `1000000099`; suspended `1000000098`; expired `1000000097`; phone-mismatch
  `1000000096`).
- `ConsoleOtpProvider` — OTP delivery no-op; tests read the `OtpRecord` hash path, not the console.
- Ministry side: WireMock-style stub server (or Testcontainers httpbin) plays the external
  ministry for adapter and workflow tests.

## 3. Tiers & layout

```
tests/
├── Sheba.Identity.Tests/         # unit: domain + handlers (exists, expand)
├── Sheba.Ministry.Tests/         # unit: adapters (auth header shapes, encryption round-trip)
├── Sheba.ServiceRequest.Tests/   # unit: step engine branching, lifecycle guards
└── Sheba.Integration.Tests/      # API-level: WebApplicationFactory + Testcontainers
```

| Tier | Scope | Rules |
|------|-------|-------|
| Unit — domain | Aggregate state machines (`Account`, `IdentityRequest`, `ServiceRequestEntity`, `OtpRecord`) | No mocks needed; assert transitions + thrown `DomainException`s |
| Unit — handlers | Command/query handlers with substituted ports | NSubstitute for repos/providers; EF InMemory acceptable |
| Integration | Module slice against real Postgres/Redis/MinIO containers | Migrations must apply cleanly (guards T-DB-1); outbox rows written + dispatched |
| Contract/API | JSend envelope shape on every route group; authZ matrix; OIDC flows via `/connect/token` | One test per matrix row of [sheba.md §10.2](sheba.md#102-permission-matrix) |
| E2E happy paths | Full onboarding→approval→login; catalog→submit→pay→ministry-call→webhook→complete | Compose-based; run nightly, not per-commit |

## 4. Priority test scenarios (backlog T-TST-1..4)

1. **Onboarding matrix (T-TST-1):** each mock-citizen fixture → expected outcome; generic error
   indistinguishability (same message/shape for all failure reasons); status-gate: login refused
   in every non-`Approved` status.
2. **OTP policy (T-TST-1):** TTL expiry, 3-attempt cap, single-use, re-issue invalidation,
   issuance throttle.
3. **Ministry adapters (T-TST-2):** per-auth-type header/token shapes; AES-GCM encrypt/decrypt
   round-trip; tampered ciphertext fails; OAuth token cache expiry refresh.
4. **Workflow engine (T-TST-3):** on_success/on_failure branching; payment gate blocks advance;
   webhook signature invalid → not processed + receipt stored; step failure → correct status.
5. **JSend contract (T-TST-4, after T-API-1):** golden tests for success/fail/error envelopes and
   the HTTP mapping table; `/connect/*` exemption still speaks OAuth shapes.
6. **Refresh-token family:** reuse of rotated token revokes family.

## 5. Conventions

`MethodUnderTest_Scenario_ExpectedOutcome` naming; one behavior per test; builders for aggregates
(no shared mutable fixtures); deterministic time via an injectable clock where TTLs are asserted
(introduce `TimeProvider` — small refactor noted in T-TST-1); CI gate: unit + integration green,
coverage floor on Domain + Application projects 80 % (report-only for Infrastructure).
