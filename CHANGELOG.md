# Changelog

All notable changes to Sheba are documented here. Format:
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning:
[SemVer](https://semver.org/) once releases begin.

## [Unreleased]

### Fixed
- **`DELETE /api/admin/relying-parties/{clientId}` 500 (T-OIDC-3)**: bumped
  `OpenIddict.AspNetCore`/`OpenIddict.EntityFrameworkCore` from `5.7.0` to `6.4.0` — the first line
  built against EF Core 9 natively; 5.7.0's `ApplicationStore.DeleteAsync` bound an EF Core 8
  `ExecuteDeleteAsync` overload absent at runtime under EF Core 9.0.6 (`MissingMethodException`).
  The 6.x line also renamed several OpenIddict APIs used in this repo:
  `SetUserinfoEndpointUris`/`EnableUserinfoEndpointPassthrough` → `SetUserInfoEndpointUris`/
  `EnableUserInfoEndpointPassthrough`, and the `Logout` endpoint permission/URIs →
  `EndSession` (`SetLogoutEndpointUris` → `SetEndSessionEndpointUris`,
  `EnableLogoutEndpointPassthrough` → `EnableEndSessionEndpointPassthrough`,
  `Permissions.Endpoints.Logout` → `Permissions.Endpoints.EndSession`) — updated at all call sites
  in `IdentityModule.cs` and `RelyingPartyEndpoints.cs`. Verified end-to-end against a live
  Postgres instance: register → delete → delete returns success and a follow-up GET 404s.
  Full suite: `dotnet build` clean, `dotnet test` 207/207 passing.

### Added
- **OpenCRVS `INationalIdProvider` adapter (T-INT-1)**: `OpenCrvsNationalIdProvider` — the second
  concrete civil-registry integration shape, deliberately different from `HttpNationalIdProvider`'s
  plain REST: OAuth2 client_credentials auth with a cached bearer token (`IMemoryCache`, 60s
  refresh margin) against a GraphQL endpoint. `NationalId:ActiveProvider = "OpenCrvs"` selects it;
  config lives under `NationalId:OpenCrvs` (`GraphQlEndpoint`, `TokenEndpoint`, `ClientId`,
  `ClientSecret`, `TimeoutSeconds`). Registry outages (HTTP failures, non-2xx, GraphQL `errors`)
  throw rather than returning `NotFound`, preserving the fail-closed-for-onboarding behavior
  documented in sheba.md §6.5. 7 new contract tests against a hand-rolled `HttpMessageHandler`
  stub (no HTTP-mocking package existed in the repo).

- **Ministry health dashboard**: a new Hangfire recurring job, `MinistryHealthSweepJob`
  (`ministry-health-sweep`, every 15 minutes), exercises `TestConnectionAsync` for every active
  `MinistryAuthConfig` by driving the existing `TestMinistryConnectionCommand` handler — the same
  code path the manual "test connection" endpoint uses, so there is one adapter-selection and
  health-recording path for both triggers. Results persist on the auth config itself
  (`LastHealthCheckAt`/`LastHealthSuccess`/`LastHealthLatencyMs`/`LastHealthError` — new nullable
  columns, migration `MinistryHealthColumns`) and are exposed to the Admin module via a new
  cross-module port, `IMinistryHealthProvider` (mirrors `IIdentityStatsProvider`), surfaced at
  `GET /api/admin/analytics/ministry-health` and sliced by the caller's `ministry_id` claim
  (T-AUTH-3 pattern — SuperAdmin/Auditor see every ministry). 10 new tests (domain entity, the
  sweep job's fan-out/error-isolation, the cross-module adapter's ministry filtering).

- **OTP provider failover + spend alarm hooks (T-INT-2)**: `ConsoleOtpProvider`/`TwilioOtpProvider`
  are now keyed DI registrations, and setting `Otp:ActiveProvider = "Failover"` selects a new
  `FailoverOtpProvider` composite that tries providers in `Otp:FailoverOrder` (default
  `["Twilio","Console"]`) until one delivers — a provider that returns failure *or throws* is
  treated as a failed attempt and the chain continues, so one flaky SMS gateway never blocks login.
  Every attempt and any full-chain exhaustion is reported to a new `IOtpSpendAlarm` hook
  (`LoggingOtpSpendAlarm` default — structured, PII-free logs an operator can alert on for
  cost/volume spikes). Existing `"Console"`/`"Twilio"` `ActiveProvider` values are unchanged.

  4 new tests (first-succeeds-short-circuits, failover-on-failure, all-fail-raises-exhausted-alarm,
  throwing-provider-continues). Full suite: `dotnet build` clean, `dotnet test` 207/207 passing.

- **Step-engine failure routing (T-SRV-4)**: `ExecuteNextStepHandler` now honors a step's
  `on_failure_step` — a failing step with a failure route jumps there (re-executing if that step is
  automated) instead of dead-ending; a failing step with no route sets the request to
  `ActionRequired` (new `ServiceRequestEntity.MarkActionRequired`). Unhandled step types now fail
  **loudly** — the step is marked `Failed` and the request `ActionRequired` — instead of silently
  falling through to the Notification handler and auto-completing work that never ran. Step
  executions are now created by a single mechanism: `SubmitServiceRequest` no longer pre-creates a
  Running row for every step (which made "the active step" ambiguous), and `ExecuteNextStep` creates
  exactly one execution per step as it runs, looked up by `(request, step order)`.

  3 new tests (unhandled-type → ActionRequired, failure routing, failure-without-route). Full suite:
  `dotnet build` clean, `dotnet test` 203/203 passing.

- **Service-request submission gates + lifecycle integrity (T-SRV-3)**: `SubmitServiceRequestHandler`
  now enforces `RequiredLoa` (from the token's `loa` claim via a new `ClaimsPrincipal.GetLoa()`
  helper — never a caller-supplied value) and mandatory-document presence (via a new cross-module
  `IDocumentPort`/`DocumentPortAdapter`) before a request is created; both fail as 422
  `DomainException`s. `ServiceRequestEntity` gained transition guards — every `Mark*`/`Advance`/
  `Complete`/`Reject`/`Cancel` now throws from a terminal status (`Completed`/`Rejected`/
  `Cancelled`/`Expired`), so a decided request is immutable (BR-SR-7). New citizen cancel endpoint
  `POST /api/requests/{id}/cancel` (`CitizenOnly`, 404-not-403 on cross-owner attempt). New hourly
  `SlaSweepJob` expires overdue `AwaitingMinistry` requests past their `DueDate` (BR-SR-6) via a new
  `Account`-less `Expire()` transition (legal only from `AwaitingMinistry`).

  Not included (carved out as a Low residual in known-issues.md): JSON-Logic **eligibility-rules**
  evaluation — `EligibilityRulesJson` is stored but not yet evaluated at submit; needs a JSON-Logic
  evaluator dependency, deferred rather than pulled in under this task. LoA and required-documents
  gating are fully in place.

  8 new tests (transition guards + LoA gate). Full suite: `dotnet build` clean, `dotnet test`
  200/200 passing.

- **Ministry seed + per-endpoint outbound rate limit (T-MIN-1)**: `MinistryModule.SeedMinistriesAsync`
  seeds the five demo ministries at the same fixed GUIDs `ServiceRequestModule.SeedServiceCatalogAsync`
  already hardcoded (previously referencing ministry ids that existed in no table); runs before the
  catalog seeder in `Program.cs`, idempotent. `Ministry.Create` gained an optional `id` parameter
  for this (every other caller keeps the default fresh-`Guid` behavior). `MinistryCallPortAdapter`
  now enforces `MinistryEndpoint.RateLimitPerMinute` via a Redis fixed-window counter
  (`ministry:ratelimit:{endpointId}:{yyyyMMddHHmm}`) before sending the outbound call; a limited
  call returns `MinistryCallResult.RateLimited = true` without ever hitting the network, and
  `MinistryCallStepHandler` defers the workflow step (`AdvanceWorkflow = false`) instead of failing
  the request outright for what's a transient throttle, not a real failure.

  Full suite: `dotnet build` clean, `dotnet test` 192/192 passing (no behavior change to any
  existing endpoint limit value — `RateLimitPerMinute` defaults to `null`/unlimited).

- **Citizen module completion (T-CIT-1)**: `CreateCitizenProfileOnApprovalHandler` consumes
  `IdentityRequestDecidedEvent(approved)` (inbox-guarded, idempotent) to materialize the
  `CitizenProfile` row other modules read via `ICitizenAccountQueryService`. `/api/citizens/me`
  (GET/PATCH, `CitizenOnly` policy) lets a citizen read/update only their own profile — ownership
  is implicit since `AccountId` always comes from the caller's own token, never a route/body
  parameter.

  Found and fixed a live bug in the same area: `Sheba.Citizen.Application` was never in
  `Program.cs`'s MediatR or FluentValidation assembly-scan lists — the existing
  `UpdateProfileCommand`/`Handler` (written before this change, never wired to an endpoint) would
  have thrown "no handler registered" the first time anything actually called it. Added the
  missing assembly registrations and a validator that didn't exist yet.

  **Verified live**: approved a pending identity request and confirmed a `citizen.citizen_profiles`
  row was created automatically with the correct NID/name/email. Full suite: `dotnet build`
  clean, `dotnet test` 192/192 passing (no new tests added for this pass — the endpoint plumbing
  mirrors Wallet's already-tested `/api/wallet/credentials` pattern exactly).

- **Account lifecycle completion (T-ID-1)**: `Account.Suspend`/`Reinstate`/`Deactivate` (§6.2:
  `Approved ⇄ Suspended`, `Approved → Deactivated` terminal) behind new admin endpoints
  `POST /api/admin/accounts/{id}/{suspend,reinstate,deactivate}` (`IdentityReviewer` policy).
  Suspending or deactivating raises a new Shared.Kernel event consumed by two independent
  handlers: Identity emails the citizen, and Wallet revokes all of the account's Verifiable
  Credentials (BR-WA-1, previously undone — VCs stayed valid through a suspension). Fixed a
  real bug in the same area: `Account.Reject(reason)` accepted a rejection reason but silently
  discarded it — there was no `RejectionReason` field on `Account` at all (the `IdentityRequest`
  side already stored it correctly). A `Rejected` account can now re-apply by registering again
  with the same NID: `RegisterCitizenHandler` reuses the existing row instead of hitting the
  already-registered guard, resets it to `PendingVerification`, and returns a response
  indistinguishable from a brand-new registration (BR-ON-3). New hourly Hangfire job
  (`AccountPurgeJob`) hard-deletes abandoned `PendingVerification` accounts (default 24h,
  `Identity:PendingVerificationPurgeHours`) — freeing their NID — plus spent/expired OTP records
  everywhere, closing the gap OtpRecord's doc comment always described but nothing implemented.

  Found and fixed a subtler bug while building the purge cutoff: filtering on `CreatedAt` would
  make a `Rejected` account that just re-applied (which reuses its original row and `CreatedAt`)
  immediately eligible for purge if the original registration was old. Filtered on `UpdatedAt`
  instead, which `Touch()` bumps on every transition including `ReApply`.

  21 new tests (`Account` lifecycle transitions + domain events, three lifecycle handlers,
  re-application). **Verified live**: suspend → login blocked → reinstate → login restored →
  deactivate → login blocked → reinstate-attempt correctly rejected (terminal); suspend/
  reinstate/deactivate emails all arrived in MailHog; `wallet.verifiable_credentials.is_revoked`
  flips to `true` on deactivation; reject → `accounts.rejection_reason` persists → re-register
  with the same NID reuses the account row, resets to `PendingVerification`, clears the reason,
  and opens a fresh `IdentityRequest` alongside the old rejected one. Full suite: `dotnet build`
  clean, `dotnet test` 192/192 passing.

- **Password reset flow (BR-LG-7)**: `POST /api/identity/password-reset/request` +
  `POST /api/identity/password-reset/confirm`, gated on an OTP sent to the account's
  **registry-registered** phone (never citizen-supplied, mirroring BR-ON-5) and only available to
  `Approved` accounts (no other status can log in at all, per BR-ON-10, so there is nothing to
  recover into). Both endpoints follow the BR-ON-3 anti-enumeration pattern: `request` always
  returns the same generic message regardless of whether the identifier matches an account, and
  `confirm` returns one identical generic error for every failure path (unknown identifier,
  non-Approved account, no active/expired OTP, exhausted attempts, wrong code). A successful reset
  also clears `FailedLoginCount`/`LockedUntil` — the same recovery effect a successful login
  already gives under BR-LG-3. New `Account.ResetPassword` domain method, `RequestPasswordReset`/
  `ConfirmPasswordReset` commands (Identity.Application), rate-limited with the existing
  `IdentityRegister`/`IdentityOtp` policies respectively.

  Found and fixed a pre-existing bug while live-verifying this flow (needed a real `Approved`
  account to test against): `GET /api/admin/identity-requests` declared `int page, int pageSize`
  with no defaults in its minimal-API lambda, so ASP.NET Core treated them as **required** query
  parameters and threw `BadHttpRequestException` (500) on any call that omitted them — despite the
  handler's own `page <= 0 ? 1 : page` fallback logic clearly intending them to be optional. Fixed
  by giving both parameters `= 0` defaults so the existing fallback logic is reachable.

  14 new tests (`Account.ResetPassword`, `RequestPasswordResetHandler`,
  `ConfirmPasswordResetHandler`). **Verified live**: registered and approved a fresh citizen →
  `password-reset/request` → OTP read from console log → `password-reset/confirm` → confirmed
  `identity.otp_records.code_hash` for the `PasswordReset` purpose is a real Argon2id hash (not
  plaintext) → logged in successfully with the new password. Full suite: `dotnet build` clean,
  `dotnet test` 171/171 passing.

- **OTP generation moved into the Application layer (T-SEC-8)**: new `IOtpCodeGenerator`
  (Shared.Kernel, implemented by `CryptoOtpCodeGenerator` in Identity.Infrastructure) generates
  numeric codes via `RandomNumberGenerator.GetInt32` (unbiased CSPRNG — replaces `ConsoleOtpProvider`'s
  previous `System.Random`, which is neither cryptographically secure nor was it the app layer's
  call to make). `IOtpProvider.SendAsync` now takes the ready code as a parameter instead of
  generating one and handing it back — a provider is purely a delivery mechanism now, exactly as
  §6.6 always documented but didn't enforce. `LoginCitizenHandler`, `RegisterCitizenHandler`, and
  `CompleteRegistrationHandler` generate the code, hash it (`IOtpHasher`), then call `SendAsync`.
  `TwilioOtpProvider` uses Verify's `CustomCode` option so Twilio delivers *our* code instead of
  minting its own (requires "Custom Code" enabled on the Verify Service in the Twilio console) —
  this also drops the unused `"TWILIO_MANAGED"` sentinel that no application-layer code ever
  actually checked.

  Fixed a real bug found while making this change: `CompleteRegistrationHandler` was storing the
  **raw** email-verification token directly in `OtpRecord.CodeHash` (never hashed), and
  `VerifyEmailHandler` compared it with a plain `!=` string check — contradicting the documented
  "raw code is NEVER stored" invariant and using a non-constant-time comparison. Both handlers now
  go through `IOtpHasher.Hash`/`Verify` like every other OTP purpose already did.

  10 new tests (`CryptoOtpCodeGenerator`, `CompleteRegistrationHandler`, `VerifyEmailHandler` —
  the latter previously had zero coverage). **Verified live**: register → the app-generated code
  appears in the console log and verifies correctly; complete-registration → confirmed
  `otp_records.code_hash` for `EmailVerify` is now a real Argon2id hash, not the plaintext
  token → `verify-email` with the real token succeeds. Full suite: `dotnet build` clean,
  `dotnet test` 156/156 passing.

- **Refresh-token family tracking for the browser PKCE flow (T-OIDC-2)**:
  `AuthorizeEndpoints.IssueAuthorizationCode` now calls the same `AttachRefreshFamilyClaimsAsync`
  helper the two custom grants (`IssueCitizenTokenAsync`/`IssueAdminTokenAsync`) already used, so
  a refresh token minted via `/connect/authorize` + PKCE gets `family_id`/`family_generation`
  claims and participates in T-SEC-9's cascade-revocation-on-reuse the same way. Also made
  `OidcEndpoints.SetDestinationsForAll` skip those two claim types by name (rather than relying on
  call ordering) — needed because `IssueFromAuthorizationCodeAsync` re-runs it over a principal
  OpenIddict restores from the stored code, and an ordering-only rule would have silently leaked
  the internal claims into the JWT at redemption time.

  **Verified live** end-to-end (admin login → session cookie → PKCE authorize → code exchange with
  a *different* client than the one that issued the original bearer token → refresh rotation ×2),
  confirming the family row is created and its generation advances correctly. This surfaced and
  fixed two pre-existing bugs along the way, both unrelated to T-OIDC-2's stated scope but found
  while exercising the same code path:
  - `POST /api/identity/session/establish` copied the caller's bearer-token claims verbatim into
    the session cookie, including OpenIddict's own internal claims (`oi_prst`, `oi_au_id`,
    `oi_tkn_id`, `client_id`, `scope`, `aud`/`exp`/`iat`/`iss`/`jti`/`nbf`). The stale `oi_prst`
    (presenters) claim survived into every new authorization code issued from that cookie, so
    OpenIddict rejected the token exchange with "issued to a different client application"
    whenever the RP calling `/connect/authorize` differed from whatever client originally issued
    the bearer token — i.e. for every third-party RP, since the citizen's own login session and a
    third party's `client_id` are never the same client. This was a live bug in the core SSO path
    since T-OIDC-1, just never exercised by a request where the two clients differed. Fixed by
    filtering the claim copy to identity claims only.
  - `RotateRefreshTokenFamilyHandler`'s reuse-detection revocation reason (57 chars) exceeded the
    `revocation_reason` column's `varchar(50)`, so a real replay attempt threw a
    `DbUpdateException` (500) instead of revoking the family — the exact code path meant to defend
    against token theft failed in the direction that leaves the family *not* revoked. Fixed by
    shortening the message and adding defensive truncation in `RefreshTokenFamily.Revoke()`.

  Also confirmed live and documented in known-issues.md: a *sequential* replay of an
  already-rotated refresh token never reaches `RotateRefreshTokenFamilyHandler` at all — OpenIddict's
  own token store rejects it first (`ID2012`). The family-generation cascade exists for the race
  condition that short-circuit doesn't cover, which sequential single-request testing can't
  reproduce; this is expected, not a gap.

  1 new domain test (`RefreshTokenFamily.Revoke` truncation). Full suite: `dotnet build` clean,
  `dotnet test` 147/147 passing.

- **Ministry-Admin per-ministry scoping (T-AUTH-1)**: `AdminUser.MinistryId` (required for
  `MinistryManager`, forbidden for every other role — enforced in `AdminUser.Create`) is embedded
  as a `ministry_id` claim on admin tokens and enforced two ways: `MinistryOwnershipFilter`
  (new, Ministry.Infrastructure) on every `/api/ministry/{id}/...` route compares the route id
  against the claim; ServiceRequest's admin routes carry an `ActorMinistryId` command parameter
  checked in the handler (`UpdateServiceDefinition`/`SetServiceFee` 404 — not 403 — on a
  cross-ministry attempt, matching the anti-enumeration shape BR-DO-1 already established;
  `CreateServiceDefinition`'s body `MinistryId` and `GetAllRequests`' `ministryId` filter are both
  force-overridden to the caller's own ministry when scoped). SuperAdmin's ministry_id-less token
  is unrestricted throughout — absence of the claim means "no restriction," never "restricted to
  nothing."

  New minimal `POST /api/admin/admin-users` (SuperAdmin-only) — a required prerequisite, since
  there was previously no way to create any admin account beyond the seeded SuperAdmin, which
  would have made the ministry_id claim impossible to attach to a real MinistryManager account.

  **Verified live**: created two ministries and a MinistryManager scoped to one; confirmed 200 on
  their own ministry, 403 (JSend `permissions`) on the other, and SuperAdmin unaffected on both.
  25 new unit tests (domain invariant, CreateAdminUser handler, ownership filter, both
  ServiceRequest ownership handlers). Found and reported two issues while testing live, not fixed
  here: `POST /api/admin/admin-users`'s `role` field only accepts numeric `AdminRole` values, not
  the string name, since no `JsonStringEnumConverter` is configured anywhere in the app (fixing it
  globally risks changing every module's existing JSON shape without a full audit); Admin/KPI
  reports have no ministry-slice filtering despite sheba.md §10.2 documenting one (T-AUTH-3, was
  never in T-AUTH-1's literal TASKS.md scope).

- **Admin/KPI ministry-slice filtering (T-AUTH-3)**: `GetKpiSummaryQuery` and
  `GetServiceRequestTrendsQuery` take an optional `MinistryId`; `AdminEndpoints` passes
  `user.GetMinistryId()` (the same claim T-AUTH-1 introduced) so a MinistryManager's dashboard
  numbers narrow to their own ministry while a null claim (SuperAdmin/Auditor) keeps the global
  view. `IAdminAnalyticsRepository.GetTodayCompletionsAsync`/`GetSlaBreachCountLast30DaysAsync`/
  `GetServiceRequestSnapshotsAsync` and the Excel/CSV service-request report generators gained the
  matching `ministryId` filter parameter (`DailyServiceRequestSnapshot` already carried
  `MinistryId`, so no migration was needed).

  Deliberately left global: `TotalAccounts`, `PendingIdentityRequests`,
  `AvgApprovalHoursLast30Days`, registration trend charts, and the identity-requests PDF/CSV
  export — these read `DailyRegistrationSnapshot`/`IIdentityStatsProvider`, and identity requests
  are not ministry-owned entities, so there is no ministry slice to apply to them.

  First test project for the Admin module (`tests/Sheba.Admin.Tests`, added to `Sheba.sln`): 8
  new tests — two handler-level (NSubstitute, verifying the claim is forwarded or left null) and
  one repository-level using EF Core InMemory to exercise the actual filter predicate rather than
  a mock. Full suite: `dotnet build` clean, `dotnet test` 146/146 passing.
- **Access-token encryption for external RPs (T-SEC-5)**: access tokens are now encrypted (JWE,
  RSA-OAEP/A256CBC-HS512) whenever a real `Identity:EncryptionCertificates` cert is configured —
  reusing T-SEC-4's `SigningCertificateLoader` result to decide, rather than an environment-name
  check, so the same config that turns on real certs also hardens the tokens they sign. Unconfigured
  (every environment today) keeps the existing plain signed JWT for `jwt.io` inspectability — no
  behavior change in dev. Verified live with a throwaway RSA cert: token shape switches from
  3-segment JWT to 5-segment JWE (`alg: RSA-OAEP`, `enc: A256CBC-HS512`) when configured, and a
  protected endpoint (`GET /api/admin/identity-requests`) still authorizes correctly against the
  encrypted token — Sheba.Api's own resource-server validation decrypts it locally without any
  separate introspection step needed. ID tokens are untouched (still plain signed JWTs, as OIDC
  expects for client consumption).
- **RP secret rotation + bilingual consent screen copy**: `POST
  /api/admin/relying-parties/{clientId}/rotate-secret` (SuperAdmin-only) generates a new
  cryptographically random secret for a confidential client via
  `IOpenIddictApplicationManager.UpdateAsync`, preserving every other registered field; the
  previous secret is rejected immediately (verified live: old secret → `invalid_client`, new
  secret → 200). `/connect/consent`'s copy is now Arabic-first (RTL) with an English section below
  it, matching the repo's citizen-facing bilingual convention — the previous pass had shipped it
  English-only. Found and reported two pre-existing issues while testing this live (not fixed
  here, flagged separately): `DELETE /api/admin/relying-parties/{clientId}` 500s on a
  `MissingMethodException` (OpenIddict.EntityFrameworkCore 5.7.0 calling an EF Core
  `ExecuteDeleteAsync` overload absent from the pinned EF Core 9.0.6 — T-OIDC-3); sheba.md §6.7
  claimed RP registration writes a `RelyingParty` domain-entity row alongside the OpenIddict
  application, which the code has never done (`RelyingPartyEndpoints.cs`'s own docstring explains
  why) — corrected in place, no dedicated task since it's a doc fix, not a functional gap.
- **Browser authorization-code + PKCE flow with civil_data consent (T-OIDC-1)**: `/connect/authorize`
  now has a real handler (`AuthorizeEndpoints`) behind the passthrough that was enabled but unused
  since Phase 0. A new, non-default cookie scheme (`SheebaSessionScheme`) backs the browser-redirect
  session, bridged from an existing bearer token via `POST /api/identity/session/establish` — no
  new login UI duplicates the existing password+OTP flow. `civil_data` in the requested scopes
  gates on the cookie's `loa` claim ≥ 2 (OAuth-shaped `invalid_scope` redirect if not) and, unless
  already consented in this round-trip, redirects to a minimal server-rendered `/connect/consent`
  page — the one place this API-first backend renders HTML directly, since nothing else in the
  repo exists to redirect to for it. The Allow/Deny decision travels back via a 2-minute one-time
  Redis marker (mirroring `MinistryWebhookVerifier`'s dedup pattern) rather than a persisted
  authorization-record store — consent is not remembered across sessions in this pass (T-OIDC-2
  tracks bringing the authorize flow's refresh tokens under T-SEC-9's family-reuse tracking, which
  this change didn't extend to). `urn:sheba:grant:national_id_otp` no longer defaults `civil_data`
  into its granted scopes and now enforces the same LoA ≥ 2 gate without the consent step (a
  first-party grant isn't the third-party trust decision consent exists for).

  **Verified live end-to-end** against a running instance with real HTTP requests (register →
  admin-approve → login → PKCE authorize → consent → token exchange), not just unit tests — this
  caught a real gap during development: `/connect/token` had no `authorization_code` grant branch
  at all (only the two custom grants + `refresh_token`), so the consent/PKCE plumbing was correct
  but nothing could actually redeem the code it produced. Fixed by adding
  `IssueFromAuthorizationCodeAsync` to `OidcEndpoints.HandleTokenAsync`. Confirmed: cookie-less
  request redirects to the configured portal login URL; `openid profile` (no civil_data) issues a
  code directly and completes a full PKCE token exchange; `civil_data` at LoA 1 redirects to the
  RP with `error=invalid_scope`; `civil_data` at LoA 2 walks the full consent round-trip and the
  resulting token's `scope` correctly includes `civil_data`.
- **Refresh-token family reuse detection (T-SEC-9)**: `RefreshTokenFamily` is wired into the
  actual token-issuance path for the first time. `OidcEndpoints` attaches an internal
  `family_id`/`family_generation` claim pair (no token destination — never appears in the JWT
  text) whenever a token response grants `offline_access`; because OpenIddict restores the full
  original principal on every future `refresh_token` grant regardless of claim destinations, these
  two claims survive across redemptions even though this endpoint never sees the raw refresh
  token value OpenIddict mints after it returns. `RotateRefreshTokenFamilyHandler` compares the
  presented generation against the family's current one on every refresh: a match advances it
  (normal use); a mismatch means a superseded token was replayed, and the **whole family** is
  revoked — including the legitimate holder's current, individually-still-valid token — matching
  the RFC 9700 guidance §6.4 already described as the target design. A family with no matching
  record (pre-feature tokens, or any response that never granted `offline_access`) defers entirely
  to OpenIddict's own validation rather than blocking anything new.

  Schema: `RefreshTokenFamily.CurrentTokenHash` (never populated — dead since the entity's
  original commit) replaced with `Generation` (int); `AccountId` renamed to `SubjectId` since the
  entity now tracks both citizen and admin sessions. Migration `RefreshTokenFamilyGeneration`
  renames the column (no data loss) and drops the unused hash column. 11 new unit tests cover the
  domain state machine and both new handlers, including the "reuse revokes the family, and the
  family stays dead even for the legitimate next attempt" scenario the AC calls out; this is
  handler-level test coverage (repository mocked), not a live HTTP+Postgres integration test —
  that tier of test infrastructure doesn't exist in this repo yet (T-TST-4).
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
