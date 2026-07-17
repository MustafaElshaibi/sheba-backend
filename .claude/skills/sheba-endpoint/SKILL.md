---
name: sheba-endpoint
description: Conventions for adding, modifying, or securing HTTP endpoints in the Sheba backend — minimal-API route groups in <Name>Module.cs, authorization policies, actor identity from JWT claims, JSend envelopes, FluentValidation, and exception mapping. Use this whenever touching any endpoint mapping, adding a route, wiring authorization/RequireAuthorization, changing a response shape, or reviewing an endpoint for security. Endpoints written without it tend to repeat the repo's two historic defects: missing authorization and trusting caller-supplied identity.
---

# Sheba Endpoint Conventions

## Where endpoints live

Minimal APIs only, mapped in the module's `<Name>Module.cs` (Infrastructure project) inside
`Map<Name>Endpoints(this WebApplication app)`. One `MapGroup` per route prefix from the module map
in `docs/sheba.md` §3.3. Endpoints are thin: bind → `mediator.Send` → map result. Any logic beyond
that belongs in a command/query handler.

## Authorization — the two rules that were historically broken

This codebase shipped with **zero** authorization; every new or touched endpoint must fix that
locally, never extend it.

1. **Every route group gets an explicit authorization posture.** Either
   `.RequireAuthorization("<policy>")` on the group, or — for the few genuinely public routes
   (registration/login steps, public service catalog, `/connect/*`) — `.AllowAnonymous()` **with a
   one-line comment saying why it is public**. An endpoint with neither is a review-blocking
   defect. Policy names follow the §10 roles: prefer group-level policies like `AdminOnly`,
   `IdentityReviewer`, `CitizenOnly`, `MinistryApiScope` (check `Program.cs`/shared auth setup for
   the ones already defined before inventing a new name).
2. **Actor identity comes from the token, never the payload.** The acting admin/citizen is
   `ClaimsPrincipal` `sub` (`User.FindFirst("sub")` / `GetClaim(Claims.Subject)`), not a
   `ReviewedByAdminId` or `citizenId` in the body/route. Route-owned resources need an ownership
   check in the handler: citizen resources compare `citizen_id == sub`; ministry-admin resources
   compare the `ministry_id` claim (T-AUTH-1). If a handler needs the actor, pass it into the
   command from the endpoint — commands never carry "who I claim to be" fields bindable by the
   caller.

## Request/response contract

- **Validation:** every command has a FluentValidation validator (auto-discovered). Don't validate
  in the endpoint lambda. Message keys become the field keys in the JSend `fail` body (T-API-1).
- **Errors:** never `Results.BadRequest(new {...})` hand-rolled shapes. Throw
  `ValidationException` / `NotFoundException` / `DomainException` from handlers and let
  `ExceptionHandlerMiddleware` map them (400/404/422).
- **Envelopes:** target standard is JSend via a shared result filter (`docs/api-contract.md`).
  Until T-API-1 lands, return the DTO directly (`Results.Ok(dto)` / `Results.Created(uri, dto)`)
  and never build a `{ status: ... }` wrapper by hand — a hand-rolled envelope will be
  double-wrapped when the filter arrives.
- **Exemptions:** `/connect/*` and `/.well-known/*` speak RFC-mandated OAuth/OIDC shapes — no
  JSend, no custom wrapper, errors via the OpenIddict `Forbid` + error properties pattern.
- 204 is avoided: prefer 200 with `data: null` so the envelope survives (§9.2).

## Security checklist for any endpoint you touch

- [ ] Group has `RequireAuthorization(...)` or a justified `AllowAnonymous()`.
- [ ] No caller-supplied actor IDs; ownership enforced for citizen/ministry resources.
- [ ] Failure messages don't reveal account existence, account status, or which identity
      check failed (BR-ON-3 / §6.3 — one generic message per public flow).
- [ ] No PII (NID, phone, OTP, password, token) in log statements added here.
- [ ] Anti-abuse: identity/OTP/token endpoints need the strict rate-limit policy (T-SEC-2)
      once available; don't add new unthrottled OTP-sending paths.
- [ ] Webhook receivers verify HMAC signature (constant-time) + timestamp window + delivery-id
      dedup **before** any processing (§7.4) — never process first, verify later.
- [ ] Swagger metadata: `.WithName(...)`, `.WithSummary(...)`, `.WithTags(...)` — match the
      existing style in the module.
