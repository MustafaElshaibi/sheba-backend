# Sheba — Frontend Integration Guide

> A practical how-to for wiring a web or mobile client to Sheba: token handling, retries, uploads,
> downloads, list querying, and turning JSend responses into UI. Concrete code is in
> [frontend-examples.md](frontend-examples.md); endpoint contracts in
> [frontend-api.md](frontend-api.md).

---

## 1. The mental model

- **Two auth surfaces.** Pre-auth JSON steps live under `/api/identity/*` (register/login/OTP).
  Actual **tokens** come only from `/connect/token`. Everything else is a resource API behind a
  bearer token.
- **One envelope.** Every non-OIDC response is a JSend envelope: `{ status, data, message?, code? }`.
  Write **one** response parser and route everything through it.
- **Identity is server-derived.** Never send `accountId`/`adminId`/`ministryId` for an authenticated
  action — the server takes it from your token. Sending it does nothing (it's ignored or rejected).
- **OIDC endpoints are different.** `/connect/*` speaks OAuth error JSON (`{error, error_description}`),
  not JSend. Handle those two shapes separately.

---

## 2. How to log in

**Citizen** (see [frontend-auth.md §5](frontend-auth.md#5-citizen-login--otp--tokens)):

1. `POST /api/identity/login` `{ usernameOrNid, password }` → `{ accountId, maskedPhone }`.
2. Collect the SMS OTP.
3. `POST /connect/token` (form-urlencoded): `grant_type=urn:sheba:grant:national_id_otp`,
   `account_id`, `otp`, `client_id=sheba-portal`, `scope=openid profile email offline_access`.
4. Store the returned `access_token` + `refresh_token`.

**Admin:** `POST /connect/token` with `grant_type=urn:sheba:grant:admin_password`,
`employee_id_or_email`, `password`, `mfa_code?`, `client_id=sheba-admin`, `client_secret`, `scope=openid profile admin_api`.

Always request `offline_access` if you want a refresh token.

---

## 3. How to log out

1. Delete the access + refresh tokens from storage.
2. (Recommended) `POST /connect/revoke` with the refresh token so it can't be replayed.
3. (Browser SSO only) redirect to `/connect/logout`.

Clearing local tokens is the authoritative client-side logout for first-party apps.

---

## 4. How to refresh tokens

- `POST /connect/token` with `grant_type=refresh_token`, `refresh_token=<current>`, `client_id`
  (+ `client_secret` for the confidential admin client).
- **Replace both tokens** with the returned pair — refresh tokens rotate; the old one is dead.
- Refresh **proactively** ~60 s before `expires_in` (900 s), and **reactively** on a `401`.
- On `invalid_grant` during refresh → the family was revoked (or token expired): clear tokens, force
  login.
- Use **single-flight**: if 5 requests 401 at once, refresh **once** and replay all 5. (Code in
  examples doc.)

---

## 5. How to store tokens securely

| Platform | Access token | Refresh token |
|----------|-------------|---------------|
| React/Next SPA | in-memory (JS variable / state) | prefer a **httpOnly cookie set by your own BFF**; if pure SPA, `localStorage` is the pragmatic default but XSS-exposed — minimize blast radius with a strict CSP |
| Next.js (with server) | server session / httpOnly cookie | httpOnly cookie via route handler (never expose to client JS) |
| Flutter (mobile) | in-memory | `flutter_secure_storage` (Keychain/Keystore) |
| Flutter (web) | in-memory | secure storage falls back to IndexedDB — same XSS caveat as SPA |

Rules of thumb: **access token in memory** (short-lived, re-derivable via refresh); **refresh token in
the most protected store available**. Never log tokens. Never put tokens in URLs/query strings.
Never store the raw National ID or OTP.

---

## 6. How to detect expired tokens

Two signals, use both:
1. **Proactive:** decode the JWT `exp` (or track `expires_in` from the token response) and refresh
   ~60 s early.
2. **Reactive:** any protected call returning **401** (`fail`/`token`) means the token is missing/
   expired/invalid → attempt one refresh, then replay; if refresh fails, log out.

Do **not** try to parse the access token for authorization decisions beyond display hints — treat it
as opaque (it may be an encrypted JWE in production). Use `role`/`loa` from the `id_token` for UI
gating only; the server is the real gate.

---

## 7. How to retry requests

- **401** → refresh once (single-flight) → replay the original request once. Still 401 → log out.
- **429** → read `Retry-After` (seconds), wait, then retry with backoff. Show a "slow down" message.
- **5xx / network** → exponential backoff with jitter, **max 2–3 retries**, only for **idempotent**
  calls (GET). Do **not** auto-retry non-idempotent POSTs (register, submit request, payment) — you
  risk duplicates; surface the error and let the user retry deliberately.
- Always attach a client `X-Correlation-Id` (a UUID) so retries share a trace.

---

## 8. How to upload files

- Endpoint: `POST /api/documents` — `multipart/form-data`.
- Fields: `file` (binary, required), `documentType` (string, optional; e.g. `"PHOTO"`, `"PDF"`,
  default `"GENERAL"`).
- Constraints: **JPEG/PNG/WebP/PDF only, max 10 MB.** Validate client-side before sending.
- Do **not** set `Content-Type` manually — let the HTTP client set the multipart boundary.
- Success → `201` `{ documentId, fileName, sizeBytes, message }`. Store `documentId` to reference the
  file in a service request.

---

## 9. How to download documents

Two-step (never a direct authenticated stream):
1. `GET /api/documents/{id}/download-url` → `{ downloadUrl, expiresAt, fileName, contentType }`.
2. `GET downloadUrl` directly (it's a **presigned MinIO URL**, no bearer needed, valid **15 min**).
   Trigger a browser download (anchor with `download`) or fetch the bytes.

Request a **fresh** URL per download; never cache past `expiresAt`. Non-owners get **404** (existence
is not leaked).

**Reports** (`/api/admin/reports/*`, `/api/admin/audit/export`) are a **direct** authenticated file
stream (PDF/Excel/CSV) with `Content-Disposition: attachment` — fetch with your bearer token and save
the blob.

---

## 10. How pagination works

- Query params `?page=1&pageSize=20` (audit defaults to `pageSize=25`).
- Paginated responses: `data.items[]`, `data.totalCount`, `data.page`, `data.pageSize`, and usually
  `data.totalPages` (**absent** on `GET /api/admin/audit`).
- Compute "has next page" as `page * pageSize < totalCount` (robust even when `totalPages` is absent).
- Bare-array list endpoints (catalog, `mine`, ministries, credentials, documents) are **not**
  paginated — they return the whole list.

```jsonc
// GET /api/admin/identity-requests?status=Pending&page=1&pageSize=20
{ "status": "success", "data": { "items": [ /* ... */ ], "totalCount": 42, "page": 1, "pageSize": 20, "totalPages": 3 } }
```

---

## 11. How filtering works

Typed query params, per endpoint. Examples:
- Identity queue: `?status=Pending`
- Admin requests: `?status=Processing&serviceId=<guid>&ministryId=<guid>&fromDate=2026-07-01T00:00:00Z&toDate=2026-07-31T00:00:00Z`
- Audit: `?actorId=<guid>&entityType=IdentityRequest&action=ApproveIdentityRequest&from=2026-07-01&to=2026-07-31`

Notes: `status` values are enum **names** (e.g. `Pending`, `Processing`). For a `MinistryManager`, a
`ministryId` filter is **overridden** by their own claim (they can't widen beyond their ministry).
There is **no** generic filter language and **no** `OR` combinations — filters AND together.

---

## 12. How sorting works

There is **no client-controllable sort** on any endpoint. Ordering is fixed server-side (generally
newest-first). If you need a different order, sort client-side after fetching (only feasible for the
non-paginated bare-array lists). Do not send `sort`/`orderBy` params — they're ignored.

---

## 13. How searching works

There is **no free-text search endpoint.** Use the typed filters above. For "find a request by
reference number", fetch the citizen's `mine` list (or the admin list with filters) and match
client-side. Treat richer search as a future capability.

---

## 14. How validation errors are returned

FluentValidation runs before handlers. A failure produces **400** with `status: "fail"` and
`data` = **{ field → message }**. One entry per invalid field; multiple messages per field are joined
with a space.

```jsonc
{ "status": "fail", "data": {
  "password": "Password must be at least 8 characters. Password must contain a digit.",
  "email": "A valid email address is required."
} }
```

Nested form-schema violations (service-request submission) use dotted keys: `formData.fullNameEn`.
**Map keys directly to your form fields.** Keys mirror the command property names in camelCase.

---

## 15. How to parse JSend responses

One function, three branches:

```
parse(httpStatus, body):
  if body.status == "success":  return { ok: true, data: body.data }
  if body.status == "fail":     return { ok: false, kind: "fail",  fields: body.data }   // dict
  if body.status == "error":    return { ok: false, kind: "error", message: body.message, code: body.code, correlationId: body.data?.correlation_id }
  // OIDC route (no `status` field):
  if body.error:                return { ok: false, kind: "oauth", error: body.error, description: body.error_description }
```

- **`fail`** → `data` is a **dictionary**; iterate keys to place field errors or show the first as a
  toast.
- **`error`** → show `message`, log `code` + `correlation_id`.
- Use the **HTTP status** to decide global handling (401 → refresh/login, 403 → permission screen,
  429 → backoff), and the **envelope** to decide UI copy.

---

## 16. How authorization (401) errors should be handled

`401` (`fail`/`token`, "Authentication is required…") means no/expired/invalid token:
1. Try a single-flight refresh.
2. Replay the request once.
3. If still 401 → clear tokens, route to login, preserve the intended destination for post-login
   return.

Never show raw "401" to users; show "Please sign in again."

---

## 17. How permission (403) errors should be handled

`403` (`fail`/`permissions`, "You do not have permission…") means authenticated but not allowed
(wrong role, or ministry-ownership violation). This is **not** fixable by refreshing:
- Show a "you don't have access to this" state; **don't** retry, **don't** log out.
- Hide/disable the action in the UI based on the token's `role`/`ministry_id` to avoid dead-ends, but
  always treat the server 403 as the final word.

---

## 18. How optimistic UI should behave

Safe to be optimistic on **idempotent, low-risk** actions (e.g. toggling a local filter, marking a
notification read once that exists). For **state-changing government actions** (submit request,
approve/reject, payment, upload), prefer **pessimistic** UI: show a spinner, wait for the `success`
envelope, then update. If you must be optimistic:
- Apply the change locally, keep the previous state.
- On non-`success`, **roll back** and show the `fail`/`error` message.
- Never optimistically assume a payment or submission succeeded — these are non-idempotent and
  irreversible from the citizen's view.

---

## 19. How polling / webhooks should be handled

- **Webhooks are inbound to Sheba** (ministry → `POST /api/webhooks/ministry/{id}`), **not** outbound
  to your frontend. There is no webhook you can subscribe to as a client, and **no WebSocket/SSE**.
- To reflect progress (service-request status, approval), **poll**:
  - `GET /api/requests/{id}` or `GET /api/requests/mine` for request/workflow status.
  - Poll with backoff (e.g. every 5–10 s while a screen is open, slower in background). Stop on
    terminal states (`Completed`, `Rejected`, `Cancelled`, `Expired`).
- **Approval** has no citizen-pollable status endpoint — the citizen is notified by email. In the
  admin app, refetch the queue after acting.

---

## 20. How long-running requests should be displayed

- **Reports** (PDF/Excel/CSV) generate synchronously and stream back a file — show a spinner/progress
  and a "preparing your report…" message; the response is the download.
- **Service-request workflow** advances asynchronously (ministry calls, webhooks). After submit,
  show a **timeline/stepper** built from `RequestDetailDto.steps[]` (each has `stepOrder`, `status`,
  `startedAt`, `completedAt`, `errorMessage`) and poll for updates.
- **Ministry test-connection** returns latency; show it inline.

---

## 21. How to handle loading states

- Every network call has three states: **idle → loading → (success | fail | error)**. Model them
  explicitly (don't infer loading from "data is null").
- Distinguish **fail** (user-fixable, show inline field/message) from **error** (server fault, show a
  retry + correlation id).
- For lists, show skeletons; for mutations, disable the submit button and show a spinner to prevent
  double-submits of non-idempotent actions.
- Debounce filter inputs before firing list queries.

---

## 22. How to display server validation messages

- The messages in `fail.data` are **already human-readable and localized to English** (the backend
  ships English copy). Show them verbatim next to the matching field (key = field name).
- For a form, build a `Record<fieldName, message>` from `fail.data` and bind each to its input's
  error slot. Clear on the next successful submit.
- For citizen-facing bilingual UIs, you may map known keys to Arabic copy client-side; the server
  currently returns English strings.

---

## 23. How to display business-rule failures

- Business-rule violations come back as **422** with `fail`/`domain` (from `DomainException`) or a
  specific `Result` error key (e.g. `account_status`). These are **not** field errors — they're
  "this action isn't allowed in the current state".
- Show them as a **prominent banner/toast**, not an inline field error. Example: submitting a service
  request below the required LoA → 422 "This service requires a higher level of assurance." → link the
  citizen to the LoA-upgrade flow.
- Common 422 cases to handle explicitly: account not `Approved` at login, LoA too low, duplicate/
  invalid state transitions, ownership violations.

---

## 24. Correlation IDs & debugging

- Send `X-Correlation-Id: <uuid>` on every request; it's echoed back and stamped on every server log
  line for that request. Reuse the same id across a retry chain.
- On an `error` (5xx), the id is also in `data.correlation_id`. Show it to users as a support
  reference and include it in bug reports.

---

## 25. CORS (browser only)

The API's CORS allow-list is `Cors:AllowedOrigins` (server config) — **no wildcard**. Your SPA origin
must be added there or browsers block responses. Native mobile is unaffected. If cross-origin calls
silently fail in a browser with no response, it's almost always a missing origin in the allow-list.

---

## 26. Quick checklist for a new client

- [ ] One JSend parser + one OAuth-error parser.
- [ ] Auth interceptor: attach `Authorization` + `X-Correlation-Id`.
- [ ] Refresh interceptor: single-flight on 401, rotate stored refresh token.
- [ ] Never send `sub`/`accountId`/`ministryId` for authenticated actions.
- [ ] File upload: validate type/size (≤10 MB) client-side; multipart.
- [ ] File download: get presigned URL → fetch directly.
- [ ] Pagination via `page`/`pageSize`; no sort/search params.
- [ ] 401→refresh, 403→permission screen, 422→business banner, 429→backoff, 5xx→error + correlation id.
- [ ] Poll for request/workflow status; no WebSockets.

---

*Generated from source on 2026-07-18.*
