# Security

> Extract of [sheba.md](sheba.md) §6, §13; sheba.md wins conflicts. Gap items (T-SEC-*) are
> tracked in [TASKS.md](../TASKS.md) / [known-issues.md](known-issues.md).

## 1. Authentication flows

- **Citizen:** password (Argon2id) + mandatory SMS OTP via the custom OpenIddict grant
  `urn:sheba:grant:national_id_otp`; browser SSO via `/connect/authorize` + PKCE
  ([sheba.md §6.3](sheba.md#63-login-flow-password--sms-otp)). OAuth 2.1 alignment: no implicit,
  no password grant, PKCE required for public clients.
- **Admin:** separate `AdminUser` principal, password + TOTP (secret stored encrypted;
  enforcement gap **T-SEC-1**), `admin_api` scope/audience.
- **Machine (ministries/RPs):** `client_credentials` with scope allowlists; static API key /
  basic auth accepted only for legacy inbound webhook callers.

## 2. Secrets management

| Secret | Dev | Production target |
|--------|-----|-------------------|
| DB/Redis/MinIO creds | compose env | Docker secrets / vault-injected env (T-SEC-3) |
| Ministry credential-vault AES key (`Ministry:EncryptionKey`) | derived dev key | Secrets store; key-id prefix on ciphertexts for rotation |
| OpenIddict signing/encryption certs | dev certs | Dedicated certs, rotation §4; never in images |
| SMS/SMTP provider keys | none (console/MailHog) | Secrets store |
| Webhook signing secrets | per-ministry, AES-encrypted in DB | same + rotation procedure with dual-accept window |

Rules: nothing secret in `appsettings*.json` or git; `.env` files gitignored; secrets never
logged; startup fails fast on missing production secrets.

## 3. Credential vault encryption

AES-256-GCM, payload `base64(nonce[12] ‖ ciphertext ‖ tag[16])`; decryption only inside ministry
auth adapters at call time; application layer never sees plaintext. **Supersedes old ADR-011
(RSA-OAEP)** — RSA-OAEP is a key-wrap primitive, wrong for bulk field encryption; GCM adds
authenticated integrity for free. Rotation (T-SEC-3): new key id → new writes use it → background
re-encrypt → retire old key.

## 4. Token & key lifecycle

- Access JWT 15 min, RS256. External RPs validate via JWKS; introspection available for opaque
  needs.
- Refresh tokens: 30 days, **rotate on every use**; `RefreshTokenFamily` reuse-detection revokes
  the entire family on replay (OAuth Security BCP / RFC 9700 guidance for public clients).
- Revocation: `/connect/revoke`, `/connect/logout`; account suspension force-revokes all families.
- **Signing-key rotation by overlap** (T-SEC-4): register new cert alongside old → JWKS publishes
  both → new tokens signed with new key → old cert removed after max token lifetime + skew. RPs
  that cache JWKS honor `kid`. Emergency rotation = same procedure compressed; expect a wave of
  401s tolerated by 15-min token TTL.

## 5. Brute-force, OTP-abuse & rate limiting

- Passwords: 5 failures → exponential account lock (`2^(n-4)` min); counters reset on success.
- OTP: CSPRNG 6-digit; Argon2id hash at rest; 5-min TTL; 3 verify attempts per code; re-issue
  invalidates prior codes; issuance ≤ 3 per 15 min per account + per-IP cap (Redis counters).
- ASP.NET Core `RateLimiter` (**T-SEC-2**, implemented): Redis-backed sliding-window-log limiters
  on `/api/identity/register|login|verify-otp` and `/connect/token`, partitioned by caller IP; an
  in-memory global default elsewhere. 429 responses render as JSend `fail` (except `/connect/*`,
  which stays OAuth-shaped).
- Registration returns one generic failure for all NID-check outcomes (anti-enumeration oracle).

## 6. Webhook security (inbound)

`X-Sheba-Signature: HMAC-SHA256(secret, timestamp + "." + raw_body)` (constant-time compare) +
`X-Sheba-Timestamp` within ±5 min + `X-Sheba-Delivery-Id` dedup store (Redis `SET NX`, fail-open
on dedup only if Redis is unreachable — signature + timestamp still gate the callback). Order:
signature → timestamp → dedup → process. Invalid receipts are never persisted with the raw body or
secret; each rejection is logged as a structured warning (ministry id, status, reason) for
alerting. Implemented in `MinistryWebhookVerifier` (**T-SRV-1**).

## 7. Input validation

FluentValidation on every command (pipeline-enforced); JSON Schema validation of dynamic service
forms server-side (**T-SRV-2**, implemented via `JsonSchema.Net` in `SubmitServiceRequestValidator`
— services without a registered form schema pass through unvalidated); ministry call URLs
constructed only from admin-registered base URL + path template with variable whitelisting (no
SSRF via citizen input); upload limits + MIME allowlists per required-document definition.

## 8. Audit logging (tamper-evident)

Behavior-produced audit rows for every state-changing command: actor, action, entity,
timestamp, IP, request/response snapshots, outcome. Target hardening: INSERT-only DB grant for the
app role (**T-AUD-1**), SHA-256 hash chain `entry_hash = H(row ‖ prev_hash)` with periodic anchor
export (**T-AUD-2** — Estonia-style tamper evidence without a blockchain), monthly partitions
(**T-AUD-3**). Verification job re-walks the chain and alerts on first break.

## 9. PII handling

See the [PII & encryption map](database-design.md#5-pii--encryption-map). Headlines: raw NID never
in tokens (SHA-256 `national_id_hash` claim), no-PII logging rule, OTP/password hashes only,
registry snapshots immutable and retention-bounded, `civil_data` scope consent-gated + LoA≥2.

## 10. STRIDE threat model (auth flows)

The full table lives in [sheba.md §13.5](sheba.md#135-stride-summary-auth-flows). Residual risks
accepted at pilot scale: single-server availability (mitigated by backups + runbook), SMS
interception (SIM swap) — mitigated by password+OTP being *two* factors and OTP being bound to the
registry-registered number; TOTP for citizens is a future LoA3 option.

## 11. Transport & headers

TLS 1.2+ terminated at the reverse proxy (prod); HSTS; `X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`, restrictive CORS (explicit RP origins only); cookies (if any UI session)
`Secure; HttpOnly; SameSite=Lax`.
