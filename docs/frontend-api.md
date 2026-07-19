# Sheba — Frontend API Reference

> **Audience:** Web (React/Next.js) and mobile (Flutter) developers building against the Sheba
> backend. This document is generated from the current implementation (`src/`), not from a target
> design. Where the code and the architecture docs (`docs/sheba.md`) diverge, **the code wins here**
> and the divergence is flagged.
>
> Companion docs: [frontend-auth.md](frontend-auth.md) (flows & sequence diagrams) ·
> [frontend-integration.md](frontend-integration.md) (how-to guide) ·
> [frontend-examples.md](frontend-examples.md) (React / Next.js / Flutter code).
>
> Machine-readable specs live in [`docs/api/`](api/): `openapi.yaml` (OpenAPI 3.1),
> `sheba.postman_collection.json`, `insomnia.json`, and `bruno/`.

---

## 0. Implementation status legend

Every endpoint is tagged so you never build against something that isn't there.

| Tag | Meaning |
|-----|---------|
| ✅ **IMPLEMENTED** | Route is mapped, handler exists, verified against source. |
| 🟡 **PARTIALLY IMPLEMENTED** | Some layers exist (e.g. command/handler) but the HTTP route is not wired, or behaviour is a mock. |
| 🟣 **PLANNED / NOT IMPLEMENTED** | Referenced in code comments/docs but no route today. Do not call it. |

**Known discrepancies vs. `docs/sheba.md` (the code is authoritative):**

1. **Password reset** — `RequestPasswordResetCommand` / `ConfirmPasswordResetCommand` and validators
   exist (`Sheba.Identity.Application/Commands/RequestPasswordReset`, `ConfirmPasswordReset`) but **no
   endpoint is mapped** in `IdentityModule.MapIdentityEndpoints`. The command docstrings reference
   `POST /api/identity/password-reset/request` and `/confirm`; those routes return **404** today. → 🟡
2. **Citizen profile API** (`/api/citizens`) — `CitizenModule.MapCitizenEndpoints` returns the app
   unchanged. `UpdateProfileCommand` exists but is unmapped. The profile is created by an event
   handler on approval, not via HTTP. → 🟣
3. **Notification API** (`/api/notifications/...`) — `NotificationModule.MapNotificationEndpoints`
   maps nothing; a `GET /api/notifications/{accountId}` is a code TODO. Notifications are delivered
   out-of-band (email/SMS), not fetched. → 🟣
4. **MFA challenge at login** is enforced inside the `urn:sheba:grant:admin_password` token grant,
   not as a separate endpoint.

---

## 1. Base URLs, hosts, and environments

| Concern | Value |
|---------|-------|
| Dev API base | `https://localhost:7001` (or the Kestrel port from `launchSettings.json`); HTTP allowed in dev (`DisableTransportSecurityRequirement`). |
| Swagger UI | `/swagger` (always enabled) |
| OpenAPI JSON | `/swagger/v1/swagger.json` |
| Hangfire dashboard | `/hangfire` (ops only, not a frontend surface) |
| Seeded citizen portal RP redirect | `https://localhost:4200/callback` |
| Seeded admin portal RP redirect | `https://localhost:4300/callback` |

There is **no global route prefix / API version segment** — routes are exactly as written
(`/api/identity/...`, `/connect/...`). CORS is an explicit allow-list (`Cors:AllowedOrigins`);
an unconfigured environment blocks **all** cross-origin browser calls, so your origin must be added
server-side before a browser SPA can talk to the API (native mobile is unaffected by CORS).

---

## 2. The JSend response envelope (read this first)

**Every** REST endpoint (everything except `/connect/*`, `/.well-known/*`, `/swagger`, `/hangfire`)
returns a [JSend](https://github.com/omniti-labs/jsend) envelope. The HTTP status code carries the
transport outcome; the `status` field carries the semantic outcome. Source:
`Sheba.Shared.Kernel/Responses/JSendResponse.cs`, `JSendWrappingFilter.cs`,
`Sheba.Api/Middleware/ExceptionHandlerMiddleware.cs`.

### 2.1 `success` — the request worked

```json
{
  "status": "success",
  "data": { "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "maskedPhone": "+967 777 ***001" }
}
```

- `data` is the endpoint's payload (object, array, or `null`). Field order in the wire JSON is
  `status`, then `message`, then `code`, then `data` (only `status` and `data` appear on success).
- A `204 No Content` from a handler is rewritten to `200` with `"data": null` so the envelope always
  survives. `Created` (201) responses keep their `Location` header and wrap the body as `success`.

### 2.2 `fail` — the request was rejected for a caller-fixable reason (4xx)

`data` is a **dictionary of field → message**. Keys are stable and meant to drive form-field error
placement. Validation failures return one entry per invalid field; other 4xx return a single
descriptive key.

```json
{
  "status": "fail",
  "data": {
    "nationalId": "National ID must be between 10 and 20 characters.",
    "phoneNumber": "Phone number must be a valid Yemeni number (+967xxxxxxxxx)."
  }
}
```

Generic single-key `fail` bodies produced by the framework (no handler payload):

| HTTP | Key | Message |
|------|-----|---------|
| 400 | `request` | "The request could not be processed." |
| 401 | `token` | "Authentication is required to access this resource." |
| 403 | `permissions` | "You do not have permission to perform this action." |
| 404 | `resource` | "The requested resource was not found." |
| 409 | `conflict` | "The request conflicts with the current state of the resource." |
| 422 | `domain` | *(the domain-rule message, e.g. "Account is not in an approvable state.")* |
| 429 | `rate_limit` | "Too many requests. Try again in N seconds." |

### 2.3 `error` — the server failed (5xx)

```json
{
  "status": "error",
  "message": "An unexpected error occurred while processing the request.",
  "code": 5001,
  "data": { "correlation_id": "0HN7…:00000003" }
}
```

- `message` is human-readable and safe to show. `code` is an internal error code (`5001` = unhandled).
- `data.correlation_id` echoes `X-Correlation-Id` — **surface it to the user** ("Reference: …") and
  log it; support uses it to find the server-side trace.

### 2.4 How `Result<T>` errors map to HTTP (Application-layer failures)

Handlers that return `Result<T>` (most Identity commands) map their `Error.Type` to status +
`fail` key = `Error.Code`. Source: `Results/ResultHttpExtensions.cs`, `Results/Error.cs`.

| `ErrorType` | HTTP | Example `fail` key |
|-------------|------|--------------------|
| `Validation` | 400 | `otp`, `credentials`, `registration` |
| `NotFound` | 404 | `resource` |
| `Conflict` (business rule) | **422** | `domain`, `account_status` |
| `Unauthorized` | 403 | `permissions` |
| `Failure` (uncategorised) | 400 | `request` |

Exceptions thrown by handlers map the same way via `ExceptionHandlerMiddleware`:
`ValidationException`→400, `NotFoundException`→404, `DomainException`→**422**,
`UnauthorizedAccessException`→403, anything else→500.

---

## 3. Authentication, roles, scopes, claims (summary — full flows in frontend-auth.md)

### 3.1 Bearer tokens

All protected endpoints expect `Authorization: Bearer <access_token>`. Access tokens are JWT
(RS256), **15-minute** TTL, issued only by `POST /connect/token`. Refresh tokens are 30 days,
rotating. In dev the access token is an inspectable signed JWT; with encryption certs configured it
becomes an opaque JWE (you never parse it client-side either way — treat it as opaque).

### 3.2 Roles (the `role` claim)

Citizen tokens always carry `role = "Citizen"`. Admin tokens carry one `AdminRole` name.
Source: `OidcEndpoints.cs`, `Program.cs` policies, `Enums/AdminRole.cs`.

| Role | Enum value | Can access |
|------|-----------|-----------|
| `Citizen` | — | Citizen self-service (`CitizenOnly` policy) |
| `SuperAdmin` | 1 | Everything (satisfies every admin policy) |
| `IdentityReviewer` | 2 | Identity-request review queue, account lookup, wallet force-issue |
| `MinistryManager` | 3 | Ministry registry, service catalog admin, request admin (scoped to own `ministry_id`) |
| `Auditor` | 4 | Audit log |
| `Support` | 5 | `AnyAdmin` dashboard reads only |

**Authorization policies** (`Program.cs`) — a policy is satisfied if the token's `role` claim is in
the allowed set:

| Policy | Allowed roles |
|--------|---------------|
| `CitizenOnly` | `Citizen` |
| `SuperAdminOnly` | `SuperAdmin` |
| `IdentityReviewer` | `SuperAdmin`, `IdentityReviewer` |
| `MinistryManager` | `SuperAdmin`, `MinistryManager` |
| `Auditor` | `SuperAdmin`, `Auditor` |
| `AnyAdmin` | `SuperAdmin`, `IdentityReviewer`, `MinistryManager`, `Auditor`, `Support` |

Ministry-scoped ownership (a `MinistryManager` only touching their own ministry's data) is enforced
**inside handlers / `MinistryOwnershipFilter`**, keyed off the `ministry_id` claim — not by the
policy. A `MinistryManager` acting on another ministry's row gets a 403/422 even though the policy
passed.

### 3.3 Scopes

`openid`, `profile`, `email`, `phone`, `offline_access` (→ refresh token), `civil_data` (registry
claims; requires LoA ≥ 2 and, in the browser flow, consent), `ministry_api` (M2M), `admin_api`
(admin portal). Registered in `IdentityModule.cs`.

### 3.4 Claims present in tokens

| Claim | Present on | Meaning |
|-------|-----------|---------|
| `sub` | all | Subject id — citizen `AccountId` or admin `AdminId`. **Never send this in a body; the server reads it from the token.** |
| `name` | all | Full name (En for citizen, admin full name) |
| `preferred_username` | citizen | Username |
| `email` | citizen/admin | Email |
| `national_id_hash` | citizen (with `civil_data`/profile scope) | SHA-256 hex of NID — raw NID never leaves the server |
| `loa` | citizen | Level of Assurance (`1`, `2`, `3`) as a string |
| `role` | all | See §3.2 |
| `ministry_id` | `MinistryManager` only | The admin's ministry scope |
| `family_id`, `family_generation` | internal only | Refresh-token family tracking — **never emitted into the JWT**, you will not see them |

### 3.5 Grant types at `/connect/token`

| `grant_type` | Who | Required params |
|-------------|-----|-----------------|
| `urn:sheba:grant:national_id_otp` | citizen | `account_id`, `otp`, `client_id`, `scope` |
| `urn:sheba:grant:admin_password` | admin | `employee_id_or_email`, `password`, `mfa_code`?, `client_id`, `client_secret`, `scope` |
| `authorization_code` | browser SSO (PKCE) | `code`, `code_verifier`, `redirect_uri`, `client_id` |
| `refresh_token` | any | `refresh_token`, `client_id` (+ `client_secret` if confidential) |
| `client_credentials` | ministries/machines | `client_id`, `client_secret`, `scope` |

---

## 4. Rate limits

Redis-backed sliding windows on auth-sensitive routes; a 300/min in-memory global limiter
everywhere else. Source: `Sheba.Api/RateLimiting/RateLimitingExtensions.cs`. Partition key = caller
IP. On rejection you get **429** with a `Retry-After` header (seconds) and a `fail`/`rate_limit`
body (OAuth `slow_down` JSON on `/connect/*`).

| Policy | Endpoints | Limit |
|--------|-----------|-------|
| `identity_register` | `POST /api/identity/register` | 5 / 5 min |
| `identity_login` | `POST /api/identity/login` | 10 / 5 min |
| `identity_otp` | `POST /api/identity/verify-otp`, `POST /api/identity/login/verify-otp` | 10 / 5 min |
| `connect_token` | `POST /connect/token` | 30 / 1 min |
| *(global default)* | everything else | 300 / 1 min |

---

## 5. Custom & standard headers

| Header | Direction | Notes |
|--------|-----------|-------|
| `Authorization: Bearer <jwt>` | request | Protected endpoints |
| `X-Correlation-Id` | request & response | Optional on request (reused if sent); **always echoed** on response. Send your own UUID to correlate client/server logs. |
| `Retry-After` | response | On 429, seconds to wait |
| `Location` | response | On 201 Created, the new resource URL |
| `Content-Type: multipart/form-data` | request | Only `POST /api/documents` |
| `X-Sheba-Event`, `X-Sheba-Signature`, `X-Sheba-Timestamp`, `X-Sheba-Delivery-Id` | request | **Inbound ministry webhooks only** — set by ministry systems, not by frontends |

---

## 6. Pagination, filtering, sorting, searching (conventions)

- **Pagination** is **query-string** based: `?page=1&pageSize=20`. Defaults vary by endpoint
  (identity requests & admin requests: `pageSize=20`; audit: `pageSize=25`). Paginated responses
  carry `items`, `totalCount`, `page`, `pageSize`, and usually `totalPages`.
- **Filtering** is per-endpoint typed query params (e.g. `status`, `serviceId`, `ministryId`,
  `from`/`to` dates). There is **no** generic filter DSL.
- **Sorting**: there is **no** client-controllable sort parameter on any endpoint today. Ordering is
  fixed server-side (typically newest-first). 🟣 for anything else.
- **Searching**: there is **no** free-text search endpoint. Lists are filtered, not searched.

Non-paginated list endpoints (service catalog, `mine` lists, ministries, credentials, documents)
return a **bare JSON array** wrapped in `success` — no pagination envelope.

---

## 7. Endpoints

Each entry lists: **Method · URL · Module · Auth · Role/Policy · Scopes · Rate limit**, then request
(headers/path/query/body + validation) and responses (success/fail/error + status codes) with full
JSON. Enums referenced are defined in [§8](#8-enum-reference). Field types: `Guid` = UUID string,
`decimal` = JSON number, dates = ISO-8601 strings.

---

### 7.1 Identity — Registration & Login (public)

All under `MapGroup("/api/identity").AllowAnonymous()` with the JSend filter. These are the
pre-authentication flows — no token is required or accepted for auth.

---

#### ✅ POST `/api/identity/register`

- **Module:** Identity · **Auth:** none · **Rate limit:** `identity_register` (5 / 5 min)
- **Purpose:** Step 1 of onboarding — verify NID + phone against the civil registry, create a
  `PendingVerification` account, send a registration OTP to the **registry-registered** phone.

**Request body**

```json
{ "nationalId": "1000000001", "phoneNumber": "0777000001" }
```

**Validation** (`RegisterCitizenValidator`)

| Field | Rules |
|-------|-------|
| `nationalId` | required; 10–20 chars; digits only (`^\d+$`) |
| `phoneNumber` | required; Yemeni format `^(\+967|967|0)\d{9}$` |

**Success — 200**

```json
{
  "status": "success",
  "data": {
    "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "maskedPhone": "+967 777 ***001"
  }
}
```

**Fail — 400** (validation) / **422** (any registry-check failure — deliberately generic, see BR-ON-3)

```json
{ "status": "fail", "data": { "registration": "We could not verify your details. Please check your national ID and registered phone number." } }
```

**Error — 500** — standard `error` envelope with `correlation_id`.

**Status codes:** 200, 400, 422, 429, 500.

**Business rules / notes for frontend:**
- The registry failure message is **intentionally identical** for not-found / deceased / suspended /
  expired / phone-mismatch / already-registered. Do **not** try to distinguish them; show the generic
  message and a "check your details" hint.
- Persist `accountId` — every subsequent step needs it.
- `maskedPhone` is safe to display ("we sent a code to +967 777 ***001").
- Mock registry (dev): NID `1000000001`–`1000000004` with matching phones `0777000001`–`0777000004`
  succeed. See §9 for the full fixture list.

---

#### ✅ POST `/api/identity/verify-otp`

- **Module:** Identity · **Auth:** none · **Rate limit:** `identity_otp` (10 / 5 min)
- **Purpose:** Step 2 — verify the registration OTP; marks the phone verified.

**Request body**

```json
{ "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "otp": "123456" }
```

**Validation** (`VerifyOtpValidator`): `accountId` required (non-empty Guid); `otp` required, `^\d{6}$`.

**Success — 200**

```json
{ "status": "success", "data": { "message": "Phone number verified. Continue to set your account details." } }
```

**Fail — 400** — `{ "status": "fail", "data": { "otp": "The code is invalid or has expired." } }`

**Status codes:** 200, 400, 429, 500.

**Notes:** OTP is 6 digits, 5-minute TTL, max 3 attempts, previous OTPs invalidated on re-issue.
There is **no resend endpoint** today — re-issue happens implicitly on re-`register`/`login`.

---

#### ✅ POST `/api/identity/complete-registration`

- **Module:** Identity · **Auth:** none
- **Purpose:** Step 3 — set username/email/password. Moves the account to `PendingAdminApproval` and
  submits the `IdentityRequest`; an email verification link is sent.

**Request body**

```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "ahmed.alyemeni",
  "email": "ahmed@example.com",
  "password": "Str0ng!Pass",
  "confirmPassword": "Str0ng!Pass"
}
```

**Validation** (`CompleteRegistrationValidator`)

| Field | Rules |
|-------|-------|
| `accountId` | required |
| `username` | required; 3–100 chars; `^[a-zA-Z0-9_.-]+$` |
| `email` | required; valid email; ≤254 chars |
| `password` | required; 8–256; ≥1 uppercase, ≥1 lowercase, ≥1 digit, ≥1 special |
| `confirmPassword` | must equal `password` |

**Success — 200**

```json
{
  "status": "success",
  "data": {
    "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "identityRequestId": "9c1b…",
    "message": "Account details saved. Check your email to verify your address."
  }
}
```

**Fail — 400** — per-field validation dictionary (see §2.2).

**Notes:** After this step, show a "check your email" screen. The verification link points at your
frontend, which then calls `verify-email` with the token.

---

#### ✅ POST `/api/identity/verify-email`

- **Module:** Identity · **Auth:** none
- **Purpose:** Step 4 — confirm the email link; moves the request into the admin approval queue.

**Request body**

```json
{ "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "token": "b3f1c9d2e8a7..." }
```

**Success — 200**

```json
{ "status": "success", "data": { "message": "Email verified. Your request is now pending review by an administrator." } }
```

**Fail — 400** — `{ "status": "fail", "data": { "token": "This verification link is invalid or has expired." } }`

**Notes:** Link is single-use, 15-minute TTL. On success, show the **waiting-for-approval** screen —
no login is possible until an admin approves (status becomes `Approved`).

---

#### ✅ POST `/api/identity/login`

- **Module:** Identity · **Auth:** none · **Rate limit:** `identity_login` (10 / 5 min)
- **Purpose:** Login step 1 — validate credentials, dispatch a login OTP by SMS. **Does not issue
  tokens.**

**Request body**

```json
{ "usernameOrNid": "ahmed.alyemeni", "password": "Str0ng!Pass" }
```

`usernameOrNid` accepts either the username or the national ID.

**Validation** (`LoginCitizenValidator`): `usernameOrNid` required, ≤100; `password` required, ≤256.

**Success — 200**

```json
{ "status": "success", "data": { "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "maskedPhone": "+967 777 ***001" } }
```

**Fail — 400** (bad credentials, generic) / **422** (account not `Approved`, or locked)

```json
{ "status": "fail", "data": { "credentials": "Invalid credentials." } }
```

**Business rules:** 5 failed passwords → exponential lockout `2^(n-4)` minutes. Only `Approved`
accounts can log in. The error is generic (no "user not found" vs "wrong password" distinction).

**Notes for frontend:** On success, go to the OTP screen and remember `accountId`. Token issuance
happens next at `/connect/token`, **not** here.

---

#### ✅ POST `/api/identity/login/verify-otp`

- **Module:** Identity · **Auth:** none · **Rate limit:** `identity_otp` (10 / 5 min)
- **Purpose:** Login step 2 — verify the login OTP (second factor). Returns the verified subject.
  **Still does not mint tokens** — call `/connect/token` with the custom grant next.

**Request body**

```json
{ "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "otp": "123456" }
```

**Validation:** `accountId` required; `otp` `^\d{6}$`.

**Success — 200**

```json
{
  "status": "success",
  "data": {
    "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nationalId": "1000000001",
    "username": "ahmed.alyemeni",
    "email": "ahmed@example.com",
    "fullNameEn": "Ahmed Al-Yemeni",
    "identityLevel": 1
  }
}
```

**Fail — 400** — `{ "status": "fail", "data": { "otp": "The code is invalid or has expired." } }`

**Notes for frontend:** **Recommended flow** is to skip reading this response's body and instead POST
the same `accountId` + `otp` directly to `/connect/token` (grant `urn:sheba:grant:national_id_otp`),
which re-runs this verification and returns tokens in one call. Calling this endpoint separately
consumes the OTP; the token grant re-validates it, so in practice front-ends go straight to
`/connect/token`. See [frontend-auth.md §Citizen Login](frontend-auth.md).

---

#### ✅ POST `/api/identity/loa/upgrade`

- **Module:** Identity · **Auth:** Bearer · **Policy:** `CitizenOnly`
- **Purpose:** Request a Level-of-Assurance upgrade (LoA 2 or 3) for **your own** account — enters
  the admin review queue.

**Request body** (`LoaUpgradeBody`)

```json
{ "targetLevel": 2 }
```

`AccountId` is taken from the token `sub`, **never** the body.

**Success — 200**

```json
{ "status": "success", "data": { "identityRequestId": "…", "targetLevel": 2, "message": "Your upgrade request has been submitted for review." } }
```

**Fail — 422** — domain rule (e.g. already at that level, or a pending upgrade exists).

**Status codes:** 200, 401, 403, 422, 500.

---

#### ✅ POST `/api/identity/session/establish`

- **Module:** Identity · **Auth:** Bearer (any authenticated principal)
- **Purpose:** Bridge an existing bearer token into a **browser session cookie** (`sheba_session`)
  so the browser `/connect/authorize` PKCE flow (SSO) can run. Used only by the web portal.

**Request:** no body. Send `Authorization: Bearer <access_token>`.

**Success — 200** (also sets `Set-Cookie: sheba_session=…`)

```json
{ "status": "success", "data": { "established": true } }
```

**Notes:** Mobile apps generally don't need this (they use the custom grant directly). Web SPAs call
it once after login before initiating a "Sign in with Sheba" authorize redirect to a third-party RP.

---

#### 🟡 POST `/api/identity/password-reset/request` · POST `/api/identity/password-reset/confirm`

- **Status:** PARTIALLY IMPLEMENTED — commands, responses, and the request-side validator exist, but
  **no route is mapped**. These paths return **404** today.
- **Planned request bodies** (for when they're wired):

```jsonc
// request
{ "usernameOrNid": "ahmed.alyemeni" }
// confirm
{ "usernameOrNid": "ahmed.alyemeni", "otp": "123456", "newPassword": "N3w!Pass", "confirmNewPassword": "N3w!Pass" }
```

- **Planned behaviour:** always returns a generic success message regardless of whether the account
  exists (anti-enumeration, BR-ON-3); the OTP goes only to the registry phone. Build the UI, but
  gate the calls behind a feature flag until the routes ship.

---

### 7.2 OIDC / OAuth 2.1 (`/connect/*`, `/.well-known/*`) — **not** JSend

These speak the OAuth/OIDC wire format (`{ "error": "...", "error_description": "..." }` on failure),
never JSend. Source: `Oidc/OidcEndpoints.cs`, `Oidc/AuthorizeEndpoints.cs`, `IdentityModule.cs`.

---

#### ✅ GET `/.well-known/openid-configuration` · GET `/.well-known/jwks`

- **Auth:** none. Standard OIDC discovery + JSON Web Key Set. Use a standard OIDC client library and
  point it at the issuer; it will fetch these automatically. Endpoints advertised:
  `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/introspect`,
  `/connect/revoke`, `/connect/logout`.

---

#### ✅ POST `/connect/token`

- **Auth:** none (client auth via body) · **Rate limit:** `connect_token` (30 / 1 min)
- **Content-Type:** `application/x-www-form-urlencoded`
- **Purpose:** Issue tokens. Supports 5 grants (§3.5).

**Citizen grant request** (`urn:sheba:grant:national_id_otp`)

```
grant_type=urn:sheba:grant:national_id_otp
&account_id=3fa85f64-5717-4562-b3fc-2c963f66afa6
&otp=123456
&client_id=sheba-portal
&scope=openid profile email offline_access
```

**Admin grant request** (`urn:sheba:grant:admin_password`)

```
grant_type=urn:sheba:grant:admin_password
&employee_id_or_email=admin@sheba.gov
&password=Admin@123
&mfa_code=123456
&client_id=sheba-admin
&client_secret=sheba-admin-dev-secret
&scope=openid profile admin_api
```

**Refresh request**

```
grant_type=refresh_token&refresh_token=<token>&client_id=sheba-portal
```

**Success — 200**

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiІ...",
  "token_type": "Bearer",
  "expires_in": 900,
  "id_token": "eyJhbGciOiJSUzI1Ni...",
  "refresh_token": "eyJhbGciOiJSUzI1Ni...",
  "scope": "openid profile email offline_access"
}
```

- `expires_in` = 900 seconds (15 min). `refresh_token` present only when `offline_access` was granted.

**Failure — 400** (OAuth format)

```json
{ "error": "invalid_grant", "error_description": "The code is invalid or has expired." }
```

Common `error` values: `invalid_request` (missing params), `invalid_grant` (bad OTP/password/MFA,
expired code/refresh), `invalid_scope` (`civil_data` below LoA 2), `unsupported_grant_type`,
`slow_down` (rate limited, with `Retry-After`).

**Notes for frontend:**
- MFA: if an enrolled admin omits `mfa_code`, you get `invalid_grant` with description
  `mfa_required`; wrong code → description `mfa`. Re-prompt and resubmit the **whole** grant.
- `civil_data` scope: only granted at LoA ≥ 2; requesting it below that → `invalid_scope`.
- Refresh rotation: every refresh returns a **new** refresh token; replaying an old one revokes the
  whole family (you'll be forced to log in again). Always store the newest refresh token.

---

#### ✅ GET/POST `/connect/userinfo`

- **Auth:** Bearer. Returns OIDC claims filtered by granted scope.

**Success — 200**

```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Ahmed Al-Yemeni",
  "preferred_username": "ahmed.alyemeni",
  "loa": "1",
  "email": "ahmed@example.com",
  "national_id_hash": "b1c2..."
}
```

`name`/`preferred_username`/`loa` require `profile`; `email` requires `email`; `national_id_hash`
requires `civil_data`. Unauthenticated → 401.

---

#### ✅ GET/POST `/connect/logout` · ✅ POST `/connect/revoke` · ✅ POST `/connect/introspect`

- `/connect/logout` — ends the OpenIddict session and redirects to `/` (or `post_logout_redirect_uri`).
- `/connect/revoke` — standard RFC 7009 token revocation (`token`, `token_type_hint`, client auth).
- `/connect/introspect` — standard RFC 7662 introspection (for RPs/resource servers, not SPAs).

Use your OIDC library's logout/revocation helpers; these are standard endpoints.

---

#### ✅ GET `/connect/authorize` · GET `/connect/consent` · POST `/connect/consent/decide`

- **Auth:** `sheba_session` cookie (established via `/api/identity/session/establish`).
- **Purpose:** Browser authorization-code + **PKCE** flow ("Sign in with Sheba" for third-party RPs).

`GET /connect/authorize?response_type=code&client_id=<rp>&redirect_uri=<uri>&scope=openid profile&code_challenge=<b64url>&code_challenge_method=S256&state=<state>`

Behaviour:
- No session cookie → 302 to `Identity:PortalLoginUrl` with `?return_url=…`.
- `civil_data` requested + LoA < 2 → 302 to `redirect_uri?error=invalid_scope`.
- `civil_data` requested + no consent marker → 302 to `/connect/consent` (server-rendered bilingual
  HTML with Allow/Deny). Allow → sets a 2-min Redis marker, re-enters authorize, issues the code.
  Deny → 302 to `redirect_uri?error=access_denied`.
- Otherwise → 302 to `redirect_uri?code=…&state=…`.

Then exchange `code` + `code_verifier` at `/connect/token`. **Use a standard PKCE OIDC library** —
you should never hand-build this. See [frontend-auth.md](frontend-auth.md).

---

### 7.3 Admin — Identity Requests, Accounts, Users, MFA

---

#### ✅ GET `/api/admin/identity-requests`

- **Module:** Identity · **Auth:** Bearer · **Policy:** `IdentityReviewer`
- **Purpose:** Paginated identity-request review queue.

**Query params:** `status` (enum `RequestStatus` name, optional), `page` (default 1), `pageSize`
(default 20). Invalid/≤0 page/pageSize are coerced to defaults.

**Success — 200**

```json
{
  "status": "success",
  "data": {
    "items": [
      {
        "requestId": "9c1b…", "accountId": "3fa8…",
        "fullNameAr": "أحمد اليمني", "fullNameEn": "Ahmed Al-Yemeni",
        "maskedNid": "1000****01", "status": "Pending", "requestType": "OpenAccount",
        "submittedAt": "2026-07-18T09:12:00Z", "reviewedAt": null
      }
    ],
    "totalCount": 1, "page": 1, "pageSize": 20, "totalPages": 1
  }
}
```

**Status codes:** 200, 401, 403, 500.

---

#### ✅ POST `/api/admin/identity-requests/{requestId}/approve`

- **Policy:** `IdentityReviewer` · **Path:** `requestId` (Guid)
- **Body** (`ApproveIdentityRequestBody`): `{ "notes": "Verified against registry snapshot." }` (`notes` optional).
- Reviewer id comes from the token `sub`.

**Success — 200**

```json
{ "status": "success", "data": { "requestId": "9c1b…", "accountId": "3fa8…", "message": "Identity request approved. The citizen account is now active." } }
```

**Fail — 404** (unknown request) / **422** (not in an approvable state).

**Notes:** Approval activates the account, emits `IdentityRequestDecidedEvent(approved)` → the
citizen is emailed and a Wallet VC is auto-issued. Refresh your queue after acting.

---

#### ✅ POST `/api/admin/identity-requests/{requestId}/reject`

- **Policy:** `IdentityReviewer` · **Path:** `requestId`
- **Body** (`RejectIdentityRequestBody`): `{ "rejectionReason": "NID photo unreadable", "notes": null }`
  — `rejectionReason` required, `notes` optional.

**Success — 200**

```json
{ "status": "success", "data": { "requestId": "9c1b…", "accountId": "3fa8…", "message": "Identity request rejected." } }
```

**Fail — 400** (missing reason) / **404** / **422**.

---

#### ✅ GET `/api/admin/accounts/{id}`

- **Policy:** `IdentityReviewer` · **Path:** `id` (Guid account id).

**Success — 200**

```json
{
  "status": "success",
  "data": {
    "id": "3fa8…", "maskedNid": "1000****01", "username": "ahmed.alyemeni",
    "email": "ahmed@example.com", "maskedPhone": "+967 777 ***001",
    "fullNameAr": "أحمد اليمني", "fullNameEn": "Ahmed Al-Yemeni",
    "status": "Approved", "identityLevel": 1,
    "createdAt": "2026-07-18T09:00:00Z", "lastLoginAt": "2026-07-18T10:30:00Z"
  }
}
```

**Fail — 404** — account not found.

---

#### ✅ POST `/api/admin/admin-users`

- **Policy:** `SuperAdminOnly`
- **Purpose:** Provision a new admin account.

**Body** (`CreateAdminUserCommand`)

```json
{
  "employeeId": "EMP1024",
  "email": "manager@moi.gov",
  "fullName": "Layla Al-Muhandis",
  "role": "MinistryManager",
  "password": "T3mp!Passw0rd",
  "department": "MOI Digital Services",
  "ministryId": "00000000-0000-0000-0001-000000000001"
}
```

**Validation:** `employeeId` required ≤50; `email` required valid ≤254; `fullName` required ≤200;
`password` required 8–256; `department` ≤100; `role` a valid `AdminRole` name. A `MinistryManager`
requires `ministryId`; other roles must omit it (enforced by `AdminUser.Create`).

**Success — 200**

```json
{ "status": "success", "data": { "adminId": "…", "employeeId": "EMP1024", "role": "MinistryManager" } }
```

**Fail — 400/422** — validation or role/ministry mismatch.

---

#### ✅ POST `/api/admin/mfa/enroll`

- **Policy:** `AnyAdmin` · **Body:** none. `AdminId` from token `sub`.
- **Purpose:** Step 1 of TOTP enrollment — generate a secret (unconfirmed).

**Success — 200**

```json
{
  "status": "success",
  "data": {
    "secret": "JBSWY3DPEHPK3PXP",
    "provisioningUri": "otpauth://totp/Sheba:admin@sheba.gov?secret=JBSWY3DPEHPK3PXP&issuer=Sheba&period=30&digits=6&algorithm=SHA1"
  }
}
```

**Notes:** Render `provisioningUri` as a QR code (or show `secret` for manual entry). MFA is **not**
enforced until `/verify` succeeds. Shown once.

---

#### ✅ POST `/api/admin/mfa/verify`

- **Policy:** `AnyAdmin` · **Body** (`ConfirmAdminMfaBody`): `{ "totpCode": "123456" }`
- **Validation:** `totpCode` required, `^[0-9]{6}$`.
- **Purpose:** Step 2 — confirm enrollment, enable MFA, return 10 single-use recovery codes.

**Success — 200**

```json
{ "status": "success", "data": { "recoveryCodes": ["a1b2-c3d4", "e5f6-g7h8", "...(10 total)"] } }
```

**Fail — 400/422** — invalid code.

**Notes:** Recovery codes are shown **once** — force the admin to save them. After this, every admin
login must include `mfa_code` (a live TOTP or an unused recovery code) at `/connect/token`.

---

### 7.4 Admin — Relying Parties (`SuperAdminOnly`)

Thin management surface over OpenIddict's application store. Source: `Oidc/RelyingPartyEndpoints.cs`.

#### ✅ GET `/api/admin/relying-parties`

List all registered OIDC clients.

**Success — 200**

```json
{
  "status": "success",
  "data": [
    { "clientId": "sheba-portal", "displayName": "Sheba Citizen Portal", "clientType": "public",
      "redirectUris": ["https://localhost:4200/callback"], "scopes": ["openid","profile","email","phone","civil_data","offline_access"] }
  ]
}
```

#### ✅ GET `/api/admin/relying-parties/{clientId}`

Single client (secret **never** returned). **404** if unknown.

#### ✅ POST `/api/admin/relying-parties`

**Body** (`RegisterRelyingPartyRequest`)

```json
{
  "clientId": "moi-portal",
  "displayName": "MOI Citizen Portal",
  "clientType": "confidential",
  "clientSecret": null,
  "redirectUris": ["https://moi.gov.ye/callback"],
  "postLogoutRedirectUris": ["https://moi.gov.ye"],
  "scopes": ["openid", "profile", "civil_data"]
}
```

- `clientId` required (409 if it exists). `clientType` `"public"` (default, PKCE, no secret) or
  `"confidential"` (secret generated if omitted). Scopes default to `["openid","profile"]`.

**Success — 201** (secret returned **once** for confidential)

```json
{ "status": "success", "data": { "clientId": "moi-portal", "clientType": "confidential", "clientSecret": "a1b2c3…(64 hex)", "message": "Store this client secret now — it will not be shown again." } }
```

**Fail — 400** (missing clientId) / **409** (duplicate).

> Note: this endpoint returns raw `Results.BadRequest`/`Conflict`/`Created` bodies which the JSend
> filter wraps; the `error` field inside a 400/409 becomes the `fail` data.

#### ✅ POST `/api/admin/relying-parties/{clientId}/rotate-secret`

Confidential clients only. Returns the new secret once; previous secret stops working immediately.
**400** if the client is public; **404** if unknown.

```json
{ "status": "success", "data": { "clientId": "moi-portal", "clientSecret": "…new…", "message": "Store this client secret now — it will not be shown again. The previous secret is now invalid." } }
```

#### ✅ DELETE `/api/admin/relying-parties/{clientId}`

Revoke/remove a client. **200** with `data: null` (204 rewritten) on success; **404** if unknown.

---

### 7.5 Ministry (`MinistryManager`)

Source: `MinistryModule.cs`. Per-ministry ownership enforced by `MinistryOwnershipFilter` on `{id}`
routes (SuperAdmin unrestricted). `GET /` and `POST /` have no `{id}` and are not ownership-filtered.

#### ✅ GET `/api/ministry`

**Query:** `includeInactive` (bool, default false).

**Success — 200** (array of `MinistrySummaryDto`)

```json
{
  "status": "success",
  "data": [
    { "id": "0000…0001", "code": "MOI", "nameAr": "وزارة الداخلية", "nameEn": "Ministry of Interior",
      "parentMinistryId": null, "depthLevel": 0, "isActive": true, "endpointCount": 3, "authConfigCount": 1,
      "createdAt": "2026-07-16T00:00:00Z" }
  ]
}
```

#### ✅ GET `/api/ministry/{id}`

Full detail (`MinistryDetailDto`) incl. `authConfigs[]`, `endpoints[]`, `webhooks[]`. **404** if unknown.

```json
{
  "status": "success",
  "data": {
    "id": "0000…0001", "code": "MOI", "nameAr": "…", "nameEn": "Ministry of Interior",
    "descriptionAr": null, "descriptionEn": null, "logoUrl": null, "websiteUrl": null,
    "contactEmail": "it@moi.gov", "contactPhone": "+967…", "parentMinistryId": null,
    "depthLevel": 0, "displayOrder": 0, "isActive": true,
    "createdAt": "2026-07-16T00:00:00Z", "updatedAt": "2026-07-16T00:00:00Z",
    "authConfigs": [
      { "id": "…", "name": "prod", "authType": "Oidc", "baseUrl": "https://api.moi.gov",
        "isActive": true, "isDefault": true, "healthCheckPath": "/health", "timeoutSeconds": 30,
        "retryCount": 3, "hasCredentials": true, "lastVerifiedAt": "2026-07-17T00:00:00Z" }
    ],
    "endpoints": [
      { "id": "…", "code": "LOOKUP", "nameAr": "…", "nameEn": "Citizen Lookup", "httpMethod": "GET",
        "pathTemplate": "/citizens/{nid}", "type": "DataQuery", "isActive": true, "authConfigId": "…" }
    ],
    "webhooks": [
      { "id": "…", "eventType": "passport.issued", "shebaWebhookPath": "/api/webhooks/ministry/0000…0001",
        "isActive": true, "lastReceivedAt": null }
    ]
  }
}
```

#### ✅ POST `/api/ministry`

**Body** (`CreateMinistryCommand`)

```json
{ "code": "MOI", "nameAr": "وزارة الداخلية", "nameEn": "Ministry of Interior",
  "parentMinistryId": null, "descriptionAr": null, "descriptionEn": null,
  "contactEmail": "it@moi.gov", "contactPhone": "+967771234567" }
```

**Success — 201** — `{ "status":"success", "data": { "ministryId":"…", "code":"MOI", "nameEn":"Ministry of Interior" } }`

#### ✅ PUT `/api/ministry/{id}`

**Body** (`UpdateMinistryCommand`, `MinistryId` overridden from route)

```json
{ "nameAr":"…", "nameEn":"Ministry of Interior", "descriptionAr":null, "descriptionEn":null,
  "logoUrl":null, "websiteUrl":null, "contactEmail":"it@moi.gov", "contactPhone":"+967…",
  "addressAr":null, "addressEn":null, "displayOrder":0, "isActive":true }
```

**Success — 200** — `{ "status":"success", "data": { "ministryId":"…", "message":"Ministry updated." } }`

#### ✅ POST `/api/ministry/{id}/auth-config`

**Body** (`SetMinistryAuthConfigCommand`; credential fields plaintext, encrypted server-side; include
only those relevant to `authType`). All optional fields shown:

```json
{
  "name": "prod", "authType": "Oidc", "baseUrl": "https://api.moi.gov",
  "isDefault": true, "healthCheckPath": "/health", "timeoutSeconds": 30, "retryCount": 3,
  "oidcTokenEndpoint": "https://idp.moi.gov/connect/token", "oidcClientId": "sheba",
  "oidcClientSecret": "•••", "oidcScope": "citizen.read",
  "apiKeyHeaderName": null, "apiKeyValue": null, "apiKeyPlacementType": null,
  "bearerToken": null, "basicUsername": null, "basicPassword": null, "adminId": null
}
```

`authType` is a `MinistryAuthType` name (§8). **Success — 201** — `{ "authConfigId":"…", "message":"…" }`.

#### ✅ POST `/api/ministry/{id}/test-connection`

**Body** (`TestConnectionBody`): `{ "authConfigId": "…" }`.

**Success — 200**

```json
{ "status": "success", "data": { "success": true, "statusCode": 200, "latencyMs": 143, "error": null } }
```

#### ✅ POST `/api/ministry/{id}/endpoints`

**Body** (`RegisterMinistryEndpointCommand`)

```json
{ "code":"LOOKUP", "nameAr":"استعلام مواطن", "nameEn":"Citizen Lookup", "httpMethod":"GET",
  "pathTemplate":"/citizens/{nid}", "type":"DataQuery", "authConfigId":null,
  "descriptionAr":null, "descriptionEn":null, "timeoutSeconds":30, "rateLimitPerMinute":null,
  "requiresCitizenConsent":false }
```

`type` is an `EndpointType` name (§8). **Success — 201** — `{ "endpointId":"…", "code":"LOOKUP", "message":"…" }`.

#### ✅ POST `/api/ministry/{id}/webhooks`

**Body** (`RegisterMinistryWebhookCommand`)

```json
{ "eventType":"passport.issued", "shebaWebhookPath":"/api/webhooks/ministry/0000…0001",
  "signingSecret":"•••plaintext•••", "endpointId":null }
```

**Success — 201** — `{ "webhookId":"…", "message":"…" }`. The `signingSecret` is AES-256-GCM encrypted
at rest and only used to verify inbound callbacks (§7.11).

---

### 7.6 Service Catalog (public)

Source: `ServiceRequestModule.cs`. Anonymous, read-only, published services. Returns **raw values**
wrapped by the JSend filter.

#### ✅ GET `/api/services`

**Query:** `includeInactive` (bool, default false).

**Success — 200** (`ServiceCatalogResponse`)

```json
{
  "status": "success",
  "data": {
    "categories": [
      {
        "id": "…", "nameAr": "وثائق هوية", "nameEn": "Identity Documents", "iconUrl": null,
        "displayOrder": 1, "isActive": true,
        "services": [
          {
            "id": "…", "code": "PASSPORT_NEW", "nameAr": "طلب جواز سفر جديد",
            "nameEn": "New Passport Application", "descriptionEn": "Apply for a new Yemeni passport",
            "requiredLoa": 2, "isOnline": true, "averageDays": 14, "isActive": true,
            "fees": [
              { "feeType": "BASE", "nameEn": "Issuance Fee", "amount": 25000, "currency": "YER", "isMandatory": true },
              { "feeType": "EXPEDITE", "nameEn": "Express Processing", "amount": 15000, "currency": "YER", "isMandatory": false }
            ]
          }
        ]
      }
    ]
  }
}
```

#### ✅ GET `/api/services/{id}`

Full service detail (`ServiceDetailDto`) with form schema, fees, required documents, workflow steps.
**404** if unknown.

```json
{
  "status": "success",
  "data": {
    "id": "…", "code": "PASSPORT_NEW", "nameAr": "…", "nameEn": "New Passport Application",
    "descriptionAr": "…", "descriptionEn": "…", "categoryId": "…", "ministryId": "0000…0001",
    "requiredLoa": 2, "requiresAppointment": false, "isOnline": true, "isActive": true,
    "averageDays": 14, "displayOrder": 0,
    "createdAt": "2026-07-16T00:00:00Z", "updatedAt": "2026-07-16T00:00:00Z",
    "formSchema": {
      "id": "…", "schemaVersion": "1.0",
      "formSchemaJson": "{\"type\":\"object\",\"required\":[\"fullNameEn\",\"fullNameAr\",\"dateOfBirth\",\"gender\",\"placeOfBirth\",\"address\",\"travelReason\"],\"properties\":{ ... }}",
      "uiSchemaJson": null
    },
    "fees": [ { "id": "…", "feeType": "BASE", "nameAr": "رسوم إصدار", "nameEn": "Issuance Fee", "amount": 25000, "currency": "YER", "isMandatory": true } ],
    "requiredDocuments": [ { "id": "…", "documentType": "PHOTO", "nameAr": "…", "nameEn": "Passport Photo", "isMandatory": true, "maxSizeMb": 5 } ],
    "workflowSteps": [ { "id": "…", "stepOrder": 1, "nameAr": "…", "nameEn": "Payment", "stepType": "Payment", "actor": "Citizen", "isAutomated": false, "timeoutHours": null } ]
  }
}
```

**Notes:** `formSchemaJson` is a **JSON-Schema string** — parse it and render a dynamic form (e.g.
with `@rjsf`). Submitted form data is validated against it server-side (see `POST /api/requests`).

---

### 7.7 Admin — Service Catalog (`MinistryManager`)

#### ✅ POST `/api/admin/services/categories`

**Body** (`CreateServiceCategoryCommand`): `{ "nameAr":"…","nameEn":"…","parentId":null,"iconUrl":null,"displayOrder":0 }`.
**201** — `{ "categoryId":"…", "nameEn":"…" }`.

#### ✅ POST `/api/admin/services`

**Body** (`CreateServiceDefinitionCommand`)

```json
{ "categoryId":"…", "ministryId":"0000…0001", "code":"PASSPORT_NEW", "nameAr":"…", "nameEn":"New Passport Application",
  "descriptionAr":null, "descriptionEn":null, "requiredLoa":2, "averageDays":14,
  "formSchemaJson":"{\"type\":\"object\", ...}", "uiSchemaJson":null }
```

For a `MinistryManager`, `ministryId` is **overridden** by their own `ministry_id` claim. Starts
unpublished. **201** — `{ "serviceId":"…", "code":"…", "nameEn":"…" }`.

#### ✅ PUT `/api/admin/services/{id}`

**Body** (`UpdateServiceDefinitionCommand`; `serviceId` from route, `actorMinistryId` from token)

```json
{ "nameAr":"…","nameEn":"…","descriptionAr":null,"descriptionEn":null,"requiredLoa":2,
  "requiresAppointment":false,"isOnline":true,"averageDays":14,"displayOrder":0,"publish":true }
```

`publish`: `true` publish, `false` depublish, `null`/omitted = no change. **200** — `{ "serviceId":"…","message":"…" }`.
**422** if a MinistryManager targets another ministry's service.

#### ✅ POST `/api/admin/services/{id}/fees`

**Body** (`SetServiceFeeCommand`; `serviceId` from route, `actorMinistryId` from token)

```json
{ "feeType":"BASE", "nameAr":"رسوم إصدار", "nameEn":"Issuance Fee", "amount":25000, "currency":"YER", "isMandatory":true }
```

`feeType` conventionally `BASE` / `EXPEDITE` / `DELIVERY`. **201** — `{ "feeId":"…","message":"…" }`.

---

### 7.8 Service Requests

Source: `ServiceRequestModule.cs`. Mixed auth per route (no group policy).

#### ✅ POST `/api/requests`

- **Policy:** `CitizenOnly`
- **Body** (`SubmitServiceRequestBody`): `CitizenId` is **not** in the body (from token `sub`).

```json
{ "serviceId": "…", "formDataJson": "{\"fullNameEn\":\"Ahmed\",\"fullNameAr\":\"أحمد\",\"dateOfBirth\":\"1990-03-15\",\"gender\":\"Male\",\"placeOfBirth\":\"Sana'a\",\"address\":\"...\",\"travelReason\":\"Study\"}", "priority": "NORMAL" }
```

**Validation** (`SubmitServiceRequestValidator`): `serviceId`, `citizenId` non-empty; `formDataJson`
required, must be valid JSON, and is validated **against the service's form schema** (JSON Schema).
Schema violations return per-field `fail` keys like `formData.fullNameEn`.

**Success — 201** — the first workflow step auto-executes.

```json
{ "status": "success", "data": { "requestId": "…", "referenceNumber": "SR-2026-000123", "status": "PaymentPending", "message": "Your request has been submitted." } }
```

**Fail — 400** (invalid form data — dictionary keyed by `formData.<field>`) / **422** (eligibility, LoA,
or missing required documents).

**Notes:** After 201, poll `GET /api/requests/{id}` (or `mine`) for status. If `status` is
`PaymentPending`, drive the payment step (§ MarkPaymentComplete).

#### ✅ GET `/api/requests/mine`

- **Policy:** `CitizenOnly`. Returns the caller's requests (`CitizenId` from token).

**Success — 200** (array of `CitizenRequestDto`)

```json
{ "status": "success", "data": [ { "id":"…","referenceNumber":"SR-2026-000123","serviceId":"…","status":"Processing","currentStep":2,"priority":"NORMAL","submittedAt":"2026-07-18T09:00:00Z","completedAt":null,"dueDate":"2026-08-01T00:00:00Z" } ] }
```

#### ✅ GET `/api/requests/{id}`

- **Auth:** any authenticated principal; **ownership enforced in handler** (citizen sees only own;
  admin sees any). Non-owner → **404** (existence not leaked).

**Success — 200** (`RequestDetailDto` with `steps[]`)

```json
{
  "status": "success",
  "data": {
    "id": "…", "referenceNumber": "SR-2026-000123", "serviceId": "…", "citizenId": "3fa8…",
    "status": "Processing", "currentStep": 2, "priority": "NORMAL",
    "submittedAt": "2026-07-18T09:00:00Z", "completedAt": null, "dueDate": "2026-08-01T00:00:00Z",
    "rejectionReason": null, "formDataJson": "{...}",
    "steps": [ { "id":"…","stepOrder":1,"status":"Completed","startedAt":"…","completedAt":"…","actorType":"System","errorMessage":null } ]
  }
}
```

#### ✅ POST `/api/requests/{id}/execute-next`

- **Policy:** `AnyAdmin`. Manually advance the workflow (operators). Returns `ExecuteNextStepResponse`.

```json
{ "status": "success", "data": { "completed": false, "currentStep": 3, "status": "AwaitingMinistry", "paymentUrl": null, "message": "Advanced to ministry API call." } }
```

#### ✅ Payment — `/api/payments` (T-PAY-1)

Payment confirmation moved off `/api/requests/*` and is now owned by the Payment module. All
three routes require any authenticated principal; ownership is enforced in the handler (a citizen
sees only their own order; admins see any).

- **GET `/{paymentOrderId}`** — order detail.
- **POST `/{paymentOrderId}/confirm`** — confirms the caller's own order via the **mock**
  gateway (`IPaymentGateway`; no real PSP integration this phase). Send an empty body. Idempotent:
  confirming an already-completed order returns its current state without calling the gateway
  again. On success, raises `PaymentCompletedEvent`, which resumes the paused ServiceRequest
  workflow — there is no more direct `requestId`/"advance workflow" response here; poll
  `GET /api/requests/{id}` for the resumed state.
- **POST `/{paymentOrderId}/refund`** — **`SuperAdminOnly`** (BR-PA-3). Refunds a completed order
  via the mock gateway.

```json
{ "status": "success", "data": { "id": "…", "serviceRequestId": "…", "citizenId": "…", "orderNumber": "PAY-20260718-ABCD1234", "totalAmount": 1500.00, "currency": "YER", "status": "Completed", "paymentUrl": "/api/payments/…/pay", "gatewayReference": "MOCK-…", "paidAt": "2026-07-18T10:00:00Z", "refundedAt": null, "refundReference": null } }
```

#### ✅ GET `/api/admin/requests`

- **Policy:** `MinistryManager`. Paginated admin list with filters.
- **Query:** `status` (`RequestLifecycleStatus` name), `serviceId`, `ministryId` (Guid;
  **overridden** by a MinistryManager's own claim), `fromDate`, `toDate` (ISO datetimes),
  `page` (1), `pageSize` (20).

**Success — 200** (`GetAllRequestsResponse`)

```json
{
  "status": "success",
  "data": {
    "items": [ { "id":"…","referenceNumber":"SR-2026-000123","serviceId":"…","citizenId":"3fa8…","status":"Processing","currentStep":2,"priority":"NORMAL","submittedAt":"…","completedAt":null } ],
    "totalCount": 1, "page": 1, "pageSize": 20, "totalPages": 1
  }
}
```

---

### 7.9 Documents (Bearer; ownership enforced in handlers)

Source: `DocumentModule.cs`. Group requires an authenticated principal (`RequireAuthorization()` —
citizen or admin token). Owner id from token `sub`.

#### ✅ POST `/api/documents` — file upload

- **Content-Type:** `multipart/form-data` · antiforgery disabled.
- **Form fields:** `file` (the binary, **required**), `documentType` (string, optional, default
  `"GENERAL"`).
- **Constraints:** JPEG/PNG/WebP/PDF, **max 10 MB** (enforced in the handler).

**Success — 201**

```json
{ "status": "success", "data": { "documentId": "…", "fileName": "passport-photo.jpg", "sizeBytes": 245760, "message": "Document uploaded." } }
```

**Fail — 400/422** — wrong type, too large, or missing file.

#### ✅ GET `/api/documents/mine`

Array of `DocumentSummaryDto`.

```json
{ "status": "success", "data": [ { "id":"…","fileName":"passport-photo.jpg","contentType":"image/jpeg","sizeBytes":245760,"documentType":"PHOTO","createdAt":"2026-07-18T09:00:00Z" } ] }
```

#### ✅ GET `/api/documents/{id}/download-url`

Presigned MinIO URL, **valid 15 minutes**. Non-owner (non-admin) → **404**.

```json
{ "status": "success", "data": { "documentId":"…","fileName":"passport-photo.jpg","contentType":"image/jpeg","downloadUrl":"https://minio.local/sheba/…?X-Amz-Signature=…","expiresAt":"2026-07-18T09:15:00Z" } }
```

**Notes:** Do **not** cache the URL beyond `expiresAt`; request a fresh one per download. The URL is
a direct object-store link (no bearer token needed on the GET to that URL).

#### ✅ DELETE `/api/documents/{id}`

Owner or admin only. Soft-deletes metadata + removes from MinIO.

```json
{ "status": "success", "data": { "deleted": true, "message": "Document deleted." } }
```

---

### 7.10 Wallet (Verifiable Credentials)

Source: `WalletModule.cs`.

#### ✅ GET `/api/wallet/credentials`

- **Policy:** `CitizenOnly`. The caller's VCs with decoded claims (`CredentialDto[]`).

```json
{
  "status": "success",
  "data": [
    {
      "id": "…", "credentialType": "DigitalIdentityCredential",
      "issuerDid": "did:sheba:issuer", "subjectDid": "did:sheba:citizen:3fa8…",
      "jwt": "eyJhbGciOiJSUzI1Ni...", 
      "claims": { "name": "Ahmed Al-Yemeni", "loa": 1, "nationalIdHash": "b1c2…" },
      "issuedAt": "2026-07-18T09:05:00Z", "expiresAt": "2027-07-18T09:05:00Z", "isRevoked": false
    }
  ]
}
```

**Notes:** `jwt` is a signed JWT-VC (W3C VC Data Model). `claims` is the decoded credentialSubject
for convenience. Present the `jwt` to verifiers; render `claims` in the UI.

#### ✅ GET `/api/wallet/credentials/{id}`

- **Policy:** `CitizenOnly`, ownership-checked (own credential only, 404 otherwise). Same shape as
  one item of the list above.

#### ✅ POST `/api/wallet/verify` — public (T-WAL-2)

- **Auth:** none (`AllowAnonymous`) — a relying party verifying a citizen's presented VC has no
  Sheba account.
- **Body:** `{ "jwt": "eyJhbGciOiJSUzI1Ni..." }`.

```json
{
  "status": "success",
  "data": {
    "isValid": true, "reason": null, "credentialId": "…",
    "credentialType": "DigitalIdentityCredential", "issuerDid": "did:sheba:issuer",
    "subjectDid": "did:sheba:citizen:3fa8…",
    "claims": { "name": "Ahmed Al-Yemeni", "loa": 1 },
    "issuedAt": "2026-07-18T09:05:00Z", "expiresAt": "2027-07-18T09:05:00Z"
  }
}
```

**Notes:** always **200** — `isValid: false` with a `reason` ("Malformed credential.", "Signature
verification failed.", "Credential not recognized.", "Credential has been revoked.", "Credential
has expired.") covers every failure mode; there's nothing to 4xx/5xx over on a public verifier
endpoint. `claims`/dates are `null` when `isValid` is `false`.

#### ✅ GET `/api/wallet/credentials/{id}/revocation-status` — public

- **Auth:** none. Lightweight check by credential ID alone — no claims, no JWT.

```json
{ "status": "success", "data": { "credentialId": "…", "isRevoked": false, "revokedAt": null } }
```

#### ✅ GET `/api/wallet/did/{did}` — public

- **Auth:** none. Resolves `did:sheba:issuer` or `did:sheba:citizen:{id}` to its public key, so a
  verifier can independently check a VC-JWT's RS256 signature (BR-WA-2).

```json
{ "status": "success", "data": { "did": "did:sheba:issuer", "publicKeyPem": "-----BEGIN PUBLIC KEY-----…", "isActive": true } }
```

#### ✅ POST `/api/admin/wallet/credentials/issue`

- **Policy:** `IdentityReviewer`. Force-issue a VC for an approved account (testing/manual re-issue).
- **Body** (`IssueIdentityCredentialCommand`): `{ "accountId": "3fa8…" }`.

**Success — 201**

```json
{ "status": "success", "data": { "credentialId":"…","credentialType":"DigitalIdentityCredential","subjectDid":"did:sheba:citizen:3fa8…","jwt":"eyJ…","message":"Credential issued." } }
```

**Notes:** Normal issuance is automatic on approval (`IdentityRequestDecidedEvent`). This is an admin
override, not a citizen action.

---

### 7.11 Webhooks — inbound ministry callbacks (public, HMAC-verified)

#### ✅ POST `/api/webhooks/ministry/{ministryId}`

- **Auth:** none (HMAC signature, **not** bearer). This is a **server-to-server** endpoint for
  ministry systems — **frontends never call it**. Documented for completeness.
- **Path:** `ministryId` (Guid). **Headers:** `X-Sheba-Event`, `X-Sheba-Signature` (hex HMAC-SHA256
  of `"{timestamp}.{rawBody}"`), `X-Sheba-Timestamp` (unix seconds, ±300 s window), `X-Sheba-Delivery-Id`
  (unique; deduped in Redis).
- **Body:** raw JSON (the exact bytes the signature was computed over).

**Accepted — 200** — `{ "status": "success", "data": { "accepted": true, "message": "Callback accepted." } }`

**Rejected — 400** — `{ "status": "fail", "data": { "accepted": false, "message": "invalid signature" } }`
(reasons: malformed, invalid signature, stale timestamp, duplicate delivery, no webhook configured).

---

### 7.12 Admin — Analytics, Reports, Audit

Source: `AdminEndpoints.cs`, `AuditModule.cs`.

#### ✅ GET `/api/admin/analytics/kpis`

- **Policy:** `AnyAdmin`. `MinistryId` scope from token (MinistryManager sees own; others global).

```json
{ "status": "success", "data": { "totalAccounts": 1042, "pendingIdentityRequests": 7, "todayCompletions": 12, "avgApprovalHoursLast30Days": 5.4, "slaBreachCount": 2 } }
```

#### ✅ GET `/api/admin/analytics/trends/registrations`

- **Policy:** `AnyAdmin`. **Query:** `days` (int, default 30). Array of `TrendPointDto`.

```json
{ "status": "success", "data": [ { "date":"2026-07-18","totalRegistrations":14,"approved":10,"rejected":1 } ] }
```

#### ✅ GET `/api/admin/analytics/trends/service-requests`

- **Policy:** `AnyAdmin`. **Query:** `days` (default 30); ministry scope from token. Array of `ServiceTrendPointDto`.

```json
{ "status": "success", "data": [ { "date":"2026-07-18","submitted":22,"completed":15 } ] }
```

#### ✅ GET `/api/admin/reports/identity-requests` — **file download**

- **Policy:** `AnyAdmin`. **Query:** `from` (date, required), `to` (date, required), `format`
  (`pdf` default, or `excel`).
- **Response:** binary file (`application/pdf` or `.xlsx`), `Content-Disposition: attachment`. **Not**
  a JSend envelope (file results pass through the filter untouched).

#### ✅ GET `/api/admin/reports/service-requests` — **file download** (Excel)

- **Policy:** `AnyAdmin`. **Query:** `from`, `to` (dates, required). Returns `.xlsx`.

#### ✅ GET `/api/admin/audit/export` — **file download** (CSV)

- **Policy:** `AnyAdmin`. **Query:** `from`, `to` (required), `type` (`service-requests`, else
  registrations). Returns `text/csv`.

#### ✅ GET `/api/admin/audit`

- **Policy:** `Auditor`. Paginated audit log.
- **Query:** `actorId` (Guid), `entityType` (string), `action` (string), `from`/`to` (dates),
  `page` (1), `pageSize` (25).

```json
{
  "status": "success",
  "data": {
    "items": [ { "id":"…","actorId":"…","action":"ApproveIdentityRequest","entityType":"IdentityRequest","entityId":"…","timestamp":"2026-07-18T10:00:00Z","ipAddress":"10.0.0.5","succeeded":true,"errorMessage":null } ],
    "totalCount": 1, "page": 1, "pageSize": 25
  }
}
```

**Note:** the audit response has **no** `totalPages` field (unlike the request lists).

---

### 7.13 Not implemented / planned surfaces

| Route | Status | Notes |
|-------|--------|-------|
| `/api/citizens/*` (profile read/update) | 🟣 | `UpdateProfileCommand` exists; no route. Profile auto-created on approval. |
| `/api/notifications/{accountId}` | 🟣 | Planned citizen notification history; not mapped. Notifications are email/SMS only. |
| `/api/identity/password-reset/*` | 🟡 | Commands exist; routes not mapped (404). |
| Client-controlled sorting / free-text search | 🟣 | Not supported on any endpoint. |

---

## 8. Enum reference

All enums are serialized **by name** (string) in JSON unless noted. Source paths in parentheses.

**`AccountStatus`** (`Identity/…/Enums/AccountStatus.cs`): `PendingVerification` (1),
`PendingEmailVerification` (2), `PendingAdminApproval` (3), `Approved` (4), `Rejected` (5),
`Suspended` (6), `Deactivated` (7). *Login is possible only when `Approved`.*

**`RequestStatus`** (identity request queue): `Pending` (1), `UnderReview` (2), `Approved` (3),
`Rejected` (4), `Cancelled` (5).

**`RequestType`**: `OpenAccount` (1), `UpgradeLoa2` (2), `UpgradeLoa3` (3), `ReopenAccount` (4).

**`AdminRole`**: `SuperAdmin` (1), `IdentityReviewer` (2), `MinistryManager` (3), `Auditor` (4),
`Support` (5).

**`OtpChannel`**: `Sms` (1), `Email` (2), `Totp` (3). **`OtpPurpose`**: `Login` (1),
`Registration` (2), `PasswordReset` (3), `EmailVerify` (4), `PhoneVerify` (5), `SensitiveAction` (6),
`TransactionConfirm` (7).

**`RpClientType`**: `Confidential`, `Public`. **`RpPartyType`**: `Ministry`, `Organization`,
`Internal`, `Developer`. *(Note: RP endpoints use OpenIddict's own `"public"`/`"confidential"`
strings, not these enum names.)*

**`MinistryAuthType`**: `Oidc` (1), `OAuth2` (2), `ApiKey` (3), `BearerToken` (4), `BasicAuth` (5),
`Saml` (6, planned/no impl), `Custom` (7), `None` (8).

**`EndpointType`**: `DataQuery` (1), `ServiceAction` (2), `WebhookRegister` (3), `StatusCheck` (4),
`DocumentFetch` (5), `Health` (6).

**`ApiKeyPlacement`**: `Header` (1), `Query` (2), `Cookie` (3).

**`WorkflowStepType`**: `CitizenSubmit` (1), `Payment` (2), `MinistryApiCall` (3), `MinistryReview`
(4), `AdminReview` (5), `DocumentIssue` (6), `Notification` (7), `WebhookWait` (8), `Appointment` (9).

**`WorkflowActor`**: `Citizen` (1), `System` (2), `Ministry` (3), `ShebaAdmin` (4).

**`RequestLifecycleStatus`** (service requests): `Draft` (1), `Submitted` (2), `PaymentPending` (3),
`UnderReview` (4), `Processing` (5), `AwaitingMinistry` (6), `ActionRequired` (7), `Completed` (8),
`Rejected` (9), `Cancelled` (10), `Expired` (11).

**`StepExecutionStatus`**: `Pending` (1), `Running` (2), `Completed` (3), `Failed` (4), `Skipped` (5).

**`PaymentStatus`**: `Pending` (1), `Completed` (2), `Failed` (3), `Refunded` (4), `Cancelled` (5).

**`ReportType`**: `IdentityRequests` (1), `ServiceRequests` (2), `AuditLog` (3).
**`ReportFormat`**: `Pdf` (1), `Excel` (2), `Csv` (3). **`ReportJobStatus`**: `Queued` (1),
`Running` (2), `Done` (3), `Failed` (4).

**Free-text status strings** you'll see in service-request responses (`status` field is a string):
the `RequestLifecycleStatus` names above. `priority` is a free string, conventionally `"LOW"`,
`"NORMAL"`, `"HIGH"`.

---

## 9. Dev fixtures & seeded credentials

From `MockNationalIdProvider` and `IdentityModule.SeedIdentityAsync` — dev only.

**Mock civil registry (NID → phone → outcome):**

| National ID | Registered phone | Outcome |
|-------------|-----------------|---------|
| `1000000001` | `0777000001` | ✅ valid (Ahmed Al-Yemeni) |
| `1000000002` | `0777000002` | ✅ valid (Fatima Al-Sana'a) |
| `1000000003` | `0777000003` | ✅ valid (Omar Al-Hadhrami) |
| `1000000004` | `0777000004` | ✅ valid (Sara Al-Aden) |
| `1000000099` | `0777000099` | ❌ deceased → generic failure |
| `1000000098` | `0777000098` | ❌ suspended → generic failure |
| `1000000097` | `0777000097` | ❌ expired NID → generic failure |
| `1000000096` | `0777000096` | ❌ use any other phone to trigger phone-mismatch |

To exercise the OTP, watch the API console/logs — `ConsoleOtpProvider` (dev default) prints the code.

**Seeded OIDC clients:**

| client_id | Type | Secret (dev) | Grants |
|-----------|------|--------------|--------|
| `sheba-portal` | public | — (PKCE) | authorization_code, refresh_token, `urn:sheba:grant:national_id_otp` |
| `sheba-admin` | confidential | `sheba-admin-dev-secret` | authorization_code, refresh_token, `urn:sheba:grant:admin_password` |
| `sheba-api-internal` | confidential (machine) | `sheba-api-internal-dev-secret` | client_credentials (`ministry_api`) |

**Seeded admin:** `ADMIN001` / `admin@sheba.gov` / `Admin@123` (SuperAdmin). Log in via
`urn:sheba:grant:admin_password` on `sheba-admin`.

---

*Generated from source on 2026-07-18. If an endpoint's behaviour differs from this doc, the code in
`src/` is authoritative — please file an issue referencing the file + line.*
