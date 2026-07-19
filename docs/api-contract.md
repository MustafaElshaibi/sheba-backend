# API Contract — REST Conventions, JSend Envelopes, Endpoint Catalog

> Extract of [sheba.md](sheba.md) §9; sheba.md wins conflicts.
> **Status:** implemented (T-API-1). The wrapper filter (`JSendWrappingFilter`) is registered on
> every module route group; the exception middleware emits JSend `fail`/`error`; bare 401/403
> challenges get JSend bodies; Swagger documents the envelope via an operation filter.

## 1. General conventions

- JSON only, `camelCase` properties, UTC ISO-8601 timestamps, UUID ids.
- Kebab-case plural routes under `/api/...`; admin surfaces under `/api/admin/...`.
- AuthN: Bearer JWT from Sheba's own OIDC server. Machine clients use `client_credentials`.
- Pagination: `?page=1&pageSize=20` (max 100); responses carry `data.items`, `data.total`,
  `data.page`, `data.pageSize`.
- Correlation: every response echoes `X-Correlation-Id`.

## 2. JSend — the three envelopes

All REST responses use [JSend](https://github.com/omniti-labs/jsend). JSend carries the
**application-level** outcome; the HTTP status carries the **transport-level** outcome. Always
both, never one instead of the other.

### `success` — it worked

```json
{ "status": "success", "data": { "...": "..." } }
```
`data` is required; `null` when there is nothing to return (e.g. DELETE).

### `fail` — the client's request was rejected

```json
{ "status": "fail", "data": { "fieldName": "why it failed" } }
```
`data` keys mirror the offending input fields where applicable; non-field preconditions use a
descriptive key (e.g. `"account": "Account is not approved."`).

### `error` — the server failed while processing

```json
{ "status": "error", "message": "human-readable message", "code": 5001,
  "data": { "correlation_id": "0HN4V2F3K8Q0J" } }
```
`message` required; `code` (numeric app error code) and `data` optional.

## 3. JSend → HTTP status mapping (normative)

| JSend | HTTP | When |
|-------|------|------|
| `success` | 200 OK | reads, updates, actions |
| `success` | 201 Created | resource created; `Location` header set |
| `success` | 200 + `data: null` | deletions (prefer over 204 so the envelope survives) |
| `fail` | 400 Bad Request | malformed/invalid input (FluentValidation) |
| `fail` | 401 Unauthorized | missing/invalid token — **the challenge still returns a JSend `fail` body** |
| `fail` | 403 Forbidden | authenticated, not allowed — JSend `fail` body |
| `fail` | 404 Not Found | resource absent or not visible to caller |
| `fail` | 409 Conflict | uniqueness/state conflict (duplicate username, already decided) |
| `fail` | 422 Unprocessable | domain rule violated (`DomainException`) |
| `fail` | 429 Too Many Requests | rate limit hit (limiter emits JSend body) |
| `error` | 500 Internal Server Error | unhandled exception |
| `error` | 502 Bad Gateway | upstream ministry/registry returned garbage |
| `error` | 503 Service Unavailable | upstream down / circuit open |

**Exempt routes:** `/connect/*` and `/.well-known/*` speak the OAuth 2.0 / OIDC wire formats
required by their specs (e.g. `{"error":"invalid_grant"}`), not JSend. Hangfire dashboard and
Swagger UI are also exempt.

## 4. Implementation design (one wrapper, no hand-rolled envelopes)

In `Sheba.Shared.Kernel/Responses/`:

```csharp
public sealed record JSendResponse<T>(string Status, T? Data, string? Message = null, int? Code = null);

public static class JSend
{
    public static JSendResponse<T> Success<T>(T? data) => new("success", data);
    public static JSendResponse<IDictionary<string, string>> Fail(IDictionary<string, string> data)
        => new("fail", data);
    public static JSendResponse<object> Error(string message, int? code = null, object? data = null)
        => new("error", data, message, code);
}
```

Wiring:

1. **Endpoint result filter** registered on every module route group
   (`group.AddEndpointFilter<JSendWrappingFilter>()`): wraps any raw DTO / `IResult` value in
   `success`; passes through responses that are already `JSendResponse<T>`.
2. **Global exception middleware** (replaces the current ProblemDetails mapper) maps
   `ValidationException` → 400 `fail` (field dictionary), `NotFoundException` → 404 `fail`,
   `DomainException` → 422 `fail`, `UnauthorizedAccessException` → 403 `fail`, anything else →
   500 `error` with correlation id and **no stack trace**.
3. **Challenge bodies**: the same exception middleware rewrites bare 401/403 challenge responses
   (emitted by the authN/authZ middleware) into JSend `fail` bodies — scheme-agnostic, so it
   covers OpenIddict validation and policy failures alike.
4. Swagger documents the envelope via a schema filter so RP developers see real shapes.

Error `code` ranges (app-level, optional): `1xxx` identity, `2xxx` ministry integration,
`3xxx` service requests, `4xxx` payment, `5xxx` infrastructure.

## 5. Worked examples

**Success (200)** — `GET /api/requests/6f1c...`

```json
{ "status": "success",
  "data": { "request": {
      "id": "6f1c2a9e-...", "referenceNumber": "SHB-2026-4A9F21",
      "status": "AwaitingMinistry", "serviceCode": "PASSPORT_RENEW",
      "submittedAt": "2026-07-16T09:12:03Z", "dueDate": "2026-07-30T00:00:00Z" } } }
```

**Validation fail (400)** — `POST /api/identity/register`

```json
{ "status": "fail",
  "data": { "nationalId": "National ID must be exactly 10 digits.",
            "phoneNumber": "Phone number must be in +967 format." } }
```

**Server error (500)** — unhandled exception during a workflow step

```json
{ "status": "error",
  "message": "An unexpected error occurred while processing the request.",
  "code": 5001,
  "data": { "correlation_id": "0HN4V2F3K8Q0J" } }
```

## 6. Endpoint catalog (by module route group)

Method-level shapes are visible in Swagger (`/swagger`); this catalog is the stable surface map.

### Identity — `/api/identity`, `/connect/*`
| Method & path | Auth | Purpose |
|---|---|---|
| POST `/api/identity/register` | anonymous | Step 1: NID + phone registry check → registration OTP |
| POST `/api/identity/verify-otp` | anonymous | Step 2: verify registration OTP |
| POST `/api/identity/complete-registration` | anonymous | Step 3: username/email/password |
| GET `/api/identity/verify-email` | anonymous | Step 4: email link confirmation |
| POST `/api/identity/login` | anonymous | Password check → login OTP dispatch |
| POST `/api/identity/loa-upgrade` | citizen | Request LoA2/LoA3 upgrade |
| POST `/connect/token` | per grant | Tokens: auth code+PKCE, client_credentials, refresh, `urn:sheba:grant:national_id_otp` |
| GET/POST `/connect/authorize`, `/connect/userinfo`, `/connect/logout`, `/connect/revoke`, `/connect/introspect` | OIDC | Standard OIDC endpoints (JSend-exempt) |

### Identity admin — `/api/admin/identity-requests`, `/api/admin/relying-parties`
| Method & path | Auth | Purpose |
|---|---|---|
| GET `/api/admin/identity-requests` | IdentityReviewer+ | Approval queue (filter by status) |
| GET `/api/admin/identity-requests/{id}` | IdentityReviewer+ | Snapshot + documents + details |
| POST `/api/admin/identity-requests/{id}/approve` | IdentityReviewer+ | Approve |
| POST `/api/admin/identity-requests/{id}/reject` | IdentityReviewer+ | Reject (reason required) |
| GET/POST/PUT `/api/admin/relying-parties[...]` | SystemAdmin | RP registry incl. OpenIddict client provisioning |

### Ministry — `/api/ministry`
CRUD ministries + sub-ministries, `/{id}/auth-configs` (+ credentials, write-only),
`/{id}/endpoints`, `/{id}/webhooks`, `/{id}/test-connection`. SystemAdmin or owning MinistryAdmin.

### ServiceRequest — `/api/services`, `/api/requests` (+ `/api/admin/...` mirrors)
Citizen: browse catalog, submit request (validates LoA/eligibility/documents), my-requests,
request detail, cancel. Payment confirmation is a Payment-module endpoint, not a ServiceRequest
one — see `/api/payments` below (T-PAY-1). Admin: category/service CRUD, publish, fees, workflow
steps, all-requests, review actions, webhook receiver path.

### Document — `/api/documents`
Upload (multipart), my-documents, presigned download URL, delete (soft). Citizen-owned; grants for
reviewers.

### Wallet — `/api/wallet`
`GET /credentials` (my credentials), `GET /credentials/{id}` (detail, JWT + claims) — both
`CitizenOnly`, owner-checked. Public (`AllowAnonymous`) verification/presentation surface
(T-WAL-2): `POST /verify` (signature + expiry + revocation), `GET
/credentials/{id}/revocation-status` (revocation only, no claims), `GET /did/{did}` (issuer/citizen
DID resolution).

### Payment — `/api/payments`
`GET /{id}` order detail (owner or admin), `POST /{id}/confirm` (owner; mock gateway),
`POST /{id}/refund` (SuperAdminOnly; mock gateway, BR-PA-3). Order creation is internal — driven
by the ServiceRequest workflow's Payment step via `IPaymentOrderPort`, not a public endpoint
(T-PAY-1).

### Audit / Admin — `/api/admin/audit`, `/api/admin`
Audit search (Auditor+); KPI summary, time-series, report jobs CRUD + download (SystemAdmin;
Auditor read-only).

## 7. Versioning & compatibility

Additive changes (new optional fields/endpoints) are unversioned. Breaking changes require a new
route version segment (`/api/v2/...`) — expected rarely; the JSend envelope itself is
version-stable by design.
