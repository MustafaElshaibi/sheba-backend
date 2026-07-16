# Business Rules

> Extract of [sheba.md](sheba.md) §6, §5.4, §10. sheba.md wins conflicts. These rules are
> enforced in Domain entities and Application validators — if a rule here has no test, that is a
> [testing.md](testing.md) gap.

## 1. Citizen onboarding (digital-identity opening)

State machine: [sheba.md §6.2](sheba.md#62-citizen-onboarding-state-machine)
(source: [diagrams/citizen-onboarding-state.mmd](diagrams/citizen-onboarding-state.mmd)).

**BR-ON-1** A digital identity can only be opened for a person who already exists in the
civil/national registry. Sheba never creates civil-registry records.

**BR-ON-2** Registration requires national ID + phone; the pair must match the registry record
(`INationalIdProvider.VerifyCitizenAsync`). Checks: exists, phone matches, not deceased, not
suspended, NID not expired, not already registered in Sheba.

**BR-ON-3** Any registration check failure returns one **generic** error message. Specific failure
reasons are logged/audited internally only (anti-enumeration).

**BR-ON-4** A registry data snapshot is frozen into the identity request at submission
(`citizen_snapshot_json`) and is immutable thereafter.

**BR-ON-5** The registration OTP goes to the **registry-registered** phone number, never a
citizen-supplied one. 6 digits, 5-minute TTL, max 3 verification attempts, single-use, previous
codes invalidated on re-issue.

**BR-ON-6** After OTP verification the citizen supplies username (unique), email (unique, will be
verified) and password (Argon2id-hashed; strength-checked). Account moves to
`PendingEmailVerification`.

**BR-ON-7** Email verification link: single-use, 15-minute expiry. On confirmation the account
moves to `PendingAdminApproval` and `IdentityRequestSubmittedEvent` fires.

**BR-ON-8** All admins with the `IdentityReviewer` (or SuperAdmin) role are notified **by email**
of each new request.

**BR-ON-9** Admin decision:
- Approve → account `Approved`; citizen notified by email; Citizen profile created; identity VC
  issued by Wallet.
- Reject → rejection **reason required**; account `Rejected`; citizen notified by email with the
  shareable reason.
- A decided request cannot be re-decided; a rejected citizen may re-apply (new request).

**BR-ON-10** **No login is possible in any account status except `Approved`.** Suspended,
deactivated, rejected, and all pending states are refused with an appropriate (non-enumerating)
message.

## 2. Login & sessions

Sequence: [sheba.md §6.3](sheba.md#63-login-flow-password--sms-otp)
(source: [diagrams/login-otp-sequence.mmd](diagrams/login-otp-sequence.mmd)).

**BR-LG-1** Login identifier is national ID **or** username, plus password.
**BR-LG-2** Every successful password check is followed by a mandatory SMS OTP to the registered
phone (no remember-me bypass; Absher-style OTP-on-every-login).
**BR-LG-3** 5 consecutive failed passwords lock the account for `2^(n-4)` minutes (exponential).
Successful login resets counters.
**BR-LG-4** OTP verification failures count toward the 3-attempt cap; exceeding it invalidates the
code and requires re-issuance (rate-limited: max 3 issuances per 15 minutes per account).
**BR-LG-5** Tokens: access 15 min; refresh 30 days with rotation; refresh-token reuse revokes the
whole family (assumed theft).
**BR-LG-6** Admin users are separate principals from citizen accounts. Admin login requires TOTP
(target: T-SEC-1). A System Admin acting as a citizen uses their citizen account and citizen
tokens.

## 3. Levels of assurance

**BR-LOA-1** LoA1 = password + OTP (default on approval). LoA2 = LoA1 + reviewed KYC documents.
LoA3 = LoA2 + biometric/in-person (deferred — open question in [known-issues.md](known-issues.md)).
**BR-LOA-2** LoA upgrades are identity requests (`UpgradeLoa2`/`UpgradeLoa3`) through the same
admin-review pipeline. Upgrades only ever increase, only on `Approved` accounts.
**BR-LOA-3** Services declare `required_loa`; submission is refused below it. Scopes may declare
`requires_loa` (e.g. `civil_data` ≥ 2).

## 4. Relying parties & consent

**BR-RP-1** Only System Admin registers RPs. Each RP gets an OpenIddict client + scope allowlist +
redirect-URI allowlist (exact match).
**BR-RP-2** Public clients (SPA/mobile) use PKCE; no client secrets. Confidential clients rotate
secrets on demand.
**BR-RP-3** `civil_data`-class scopes require explicit citizen consent at the authorize prompt,
per RP, revocable.
**BR-RP-4** Sheba's own portal (`sheba-portal`) and admin dashboard (`sheba-admin`) are ordinary
registered RPs.

## 5. Ministries & integrations

**BR-MI-1** A ministry must have: name (ar/en), description, base URL, an auth config with
credentials, and at least one endpoint before any service can bind to it.
**BR-MI-2** Sub-ministries nest recursively (`parent_ministry_id`); deactivating a parent
deactivates its subtree for new calls.
**BR-MI-3** Ministry credentials are write-only through the API: create/update accepted, plaintext
never returned. Decryption happens only inside auth adapters at call time.
**BR-MI-4** Only System Admin or the owning Ministry Admin can modify a ministry's data,
endpoints, credentials, or webhooks.
**BR-MI-5** Inbound webhooks must pass HMAC signature + timestamp window + delivery-id dedup
([sheba.md §7.4](sheba.md#74-inbound-webhooks--verification-contract)) before any processing.
Invalid-signature receipts are stored and alerted, never processed.

## 6. Service catalog & requests

Lifecycle: [sheba.md §5.4.1](sheba.md#541-request-lifecycle)
(source: [diagrams/service-request-lifecycle.mmd](diagrams/service-request-lifecycle.mmd)).

**BR-SR-1** A service is publishable only with a form schema **or** at least one workflow step
(`ServiceDefinition.Publish()` enforces). Unpublished services are invisible to citizens.
**BR-SR-2** Submission validates: account `Approved`, LoA ≥ `required_loa`, eligibility rules
(JSON Logic) pass, all mandatory documents attached (type/size/MIME limits enforced).
**BR-SR-3** Every request gets a unique human-readable reference `SHB-<year>-<6 hex>`.
**BR-SR-4** Workflow executes strictly by `step_order` with explicit `on_success_step` /
`on_failure_step` branching. Every execution is recorded (`request_step_executions`) with actor,
timestamps, result JSON — this is the request's audit trail.
**BR-SR-5** A `Payment` step creates a `PaymentOrder` for the mandatory fees valid on submission
date; the workflow does not advance until payment completes.
**BR-SR-6** `due_date` = submission + service `average_days`; breach flags SLA in BI and (target)
sets status `Expired` for abandoned `AwaitingMinistry` requests.
**BR-SR-7** Citizens may cancel only before completion/rejection. Completed/rejected requests are
immutable.
**BR-SR-8** Rejection at any review step requires a recorded reason; citizen is notified.

## 7. Payments

**BR-PA-1** Fees come only from the service's fee schedule (validity-dated); no ad-hoc amounts.
**BR-PA-2** Payment orders are idempotent per (request, fee set) — re-initiating returns the open
order. Currency default YER.
**BR-PA-3** Refunds (target, T-PAY-1) only against completed payments, only by System Admin, fully
audited.

## 8. Documents & wallet

**BR-DO-1** Documents are soft-deleted only; owners see only their own documents; non-owners need
a time-boxed access grant.
**BR-DO-2** Downloads go through short-TTL presigned URLs — MinIO is never exposed directly.
**BR-WA-1** The identity VC is issued automatically on approval; revoking an account (suspension/
deactivation) revokes its VCs.
**BR-WA-2** VCs are signed JWT-VCs verifiable against Sheba's published keys; revocation status is
checkable via the wallet API.

## 9. Audit & notifications

**BR-AU-1** Every state-changing command produces an audit event (actor, action, entity,
timestamp, IP, before/after snapshots, outcome). Audit rows are never updated or deleted.
**BR-NO-1** Notification sends are logged append-only with outcome; delivery failure never fails
the originating business operation (fire-and-record).
