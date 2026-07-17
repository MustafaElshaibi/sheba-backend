---
name: sheba-testing
description: How to write and structure tests for the Sheba backend — naming conventions, the mock civil-registry fixture citizens, OTP/console providers, what each test project must cover, and the security behaviors that always need regression tests. Use this whenever writing or modifying tests, adding coverage for a change, working in the tests/ directory, or implementing any T-TST task. Also use it when a change touches onboarding, login, OTP, webhooks, or workflow logic — those areas have mandatory test scenarios listed here.
---

# Sheba Testing Conventions

Full policy: [docs/testing.md](../../../docs/testing.md). Stack: xUnit + FluentAssertions +
NSubstitute (+ Testcontainers for integration). Test projects: `tests/Sheba.Identity.Tests`,
`Sheba.Ministry.Tests`, `Sheba.ServiceRequest.Tests`, `Sheba.Integration.Tests`. Real examples to
mirror: `IdentityRequestDecisionHandlerTests.cs`, `VerifyOtpHandlerTests.cs`.

## Ground rules

- Naming: `MethodUnderTest_Scenario_ExpectedOutcome`
  (e.g. `Handle_NidAlreadyRegistered_ReturnsGenericError`).
- **Never call live services.** National-ID checks use `MockNationalIdProvider` (or an NSubstitute
  `INationalIdProvider`), OTP uses the console provider or a substitute. Tests that would hit a
  network are a defect.
- Unit tests substitute repositories/ports (NSubstitute); integration tests use Testcontainers
  (Postgres/Redis/MinIO), never a developer's local services.
- Time-dependent logic (OTP TTL, lockout windows, token lifetimes): inject `TimeProvider` rather
  than sleeping or comparing against `DateTime.UtcNow` slack (T-TST-1 introduces this — extend it,
  don't add new untestable `DateTime.UtcNow` reads).
- Delete `PlaceholderTest.cs` / `UnitTest1.cs` from a project when adding its first real tests.

## Mock civil-registry fixtures (seeded in `MockNationalIdProvider`)

8 citizens covering every rejection branch — use these NIDs, don't invent new magic values:

| NID | Registered phone | Registry state | Expected onboarding outcome |
|-----|------------------|---------------|------------------------------|
| `1000000001`–`1000000004` | `0777000001`–`0777000004` (matching suffix) | Valid | proceeds to OTP |
| `1000000099` | `0777000099` | Deceased | generic failure |
| `1000000098` | `0777000098` | Suspended | generic failure |
| `1000000097` | `0777000097` | NID expired | generic failure |
| `1000000096` | `0777000096` | Valid, but test with a *different* phone to hit the mismatch branch | generic failure |

## Behaviors that always need regression tests (security-critical)

When your change touches one of these areas, these scenarios are mandatory, not optional:

- **Anti-enumeration (BR-ON-3):** every registration failure branch (not found, deceased,
  suspended, expired, phone mismatch, *already registered*) returns the **byte-identical** generic
  message. Assert message equality across branches, not just "a failure happened". Same idea for
  login: account-not-found and wrong-password must be indistinguishable, and status must not leak
  before credential verification.
- **OTP policy:** 5-min TTL expiry rejected, 4th attempt rejected, used code rejected, re-issue
  invalidates prior codes, code never appears in logs/responses.
- **Lockout:** 5th failed password locks; success resets counters.
- **Login status gate:** every non-`Approved` status fails to log in.
- **Webhooks:** wrong signature → rejected before any state change; stale timestamp → rejected;
  duplicate delivery-id → processed once (T-SRV-1 scenarios).
- **AuthZ:** each protected route group rejects anonymous (401) and wrong-role (403) callers —
  contract tests per the §10 permission matrix rows the change touches.
- **Crypto round-trips:** `AesGcmCredentialEncryptor` encrypt→decrypt equality + tampered
  ciphertext throws (T-TST-2).

## Verification

`dotnet test Sheba.sln` must be green before a change set is declared done. For behavior with a
runtime surface, also drive the flow end-to-end (compose up + hit the endpoint) rather than
trusting unit tests alone.
