# CLAUDE.md — Working in the Sheba repository

Guidance for Claude and other AI agents. Human-facing intro: [README.md](README.md).
Agent roles and hard boundaries: [AGENTS.md](AGENTS.md).

## What this is

Yemen's e-Government backend: a .NET 9 **modular monolith** that is simultaneously the national
OIDC/OAuth 2.1 identity provider (OpenIddict) and the government service gateway. Ten modules
under `src/Modules/<Name>/`, one host (`src/Sheba.Api`), one PostgreSQL DB with a schema per
module. **The architectural source of truth is [docs/sheba.md](docs/sheba.md)** — read the
relevant section before changing anything structural; sibling docs extract it and must not
contradict it.

## Non-negotiable boundary rules

1. A module project references **only** `Sheba.Shared.Kernel` — never another module's
   Domain/Application/Infrastructure. (Documented exception: ServiceRequest → Ministry ports;
   do not add more.)
2. **No cross-schema DB access.** Cross-context references are bare `Guid`s with a comment naming
   the logical target. No FKs, joins, or queries across schemas.
3. Cross-module communication is **only**: (a) MediatR integration events (`IDomainEvent`
   records raised on aggregates), or (b) read-only query ports declared in
   `Sheba.Shared.Kernel.Interfaces` and implemented by the owning module.
4. Business rules live on **aggregates** (static factories + intention-revealing methods that
   throw `DomainException`), not in handlers. See [docs/coding-standards.md](docs/coding-standards.md).

## Conventions that will bite you if ignored

- **Layering:** Domain ← Application ← Infrastructure; `<Name>Module.cs` (in Infrastructure) does
  DI registration + minimal-API endpoint mapping. No controllers anywhere.
- **API envelopes:** the target standard is **JSend** via a shared wrapping filter + exception
  middleware ([docs/api-contract.md](docs/api-contract.md)). Never hand-roll response envelopes
  in endpoints. `/connect/*` and `/.well-known/*` are exempt (OIDC wire formats).
- **Errors:** throw `ValidationException` / `NotFoundException` / `DomainException` — the
  middleware maps them (400/404/422). Every command gets a FluentValidation validator; message
  keys become JSend `fail` field keys.
- **Persistence:** EF migrations only (`--project src/Modules/<M>/Sheba.<M>.Infrastructure
  --startup-project src/Sheba.Api`). Do not rely on the `EnsureCreated()` fallback — it is being
  removed (T-DB-1). snake_case tables, configurations under `Persistence/Configurations/`.
- **No-PII logging:** national IDs, phones, OTP codes, passwords, tokens, credential material
  never go to logs. Log entity IDs.
- **Secrets:** never in `appsettings*.json` or code. Ministry credentials are AES-256-GCM
  encrypted via `ICredentialEncryptor`; plaintext exists only inside auth adapters at call time.
- **Pluggability:** national-ID and OTP integrations go behind `INationalIdProvider` /
  `IOtpProvider`, selected by `NationalId:ActiveProvider` / `Otp:ActiveProvider` config. New
  providers = new adapter + config entry, never `#if`/env checks.
- **Bilingual fields:** anything citizen-facing needs `NameAr`/`NameEn` pairs.

## Where things live

| Need | Location |
|------|----------|
| Host pipeline, MediatR behaviors, exception middleware | `src/Sheba.Api/` |
| OpenIddict config, custom grant `urn:sheba:grant:national_id_otp` | `src/Modules/Identity/Sheba.Identity.Infrastructure/IdentityModule.cs`, `Oidc/` |
| Onboarding/login commands | `src/Modules/Identity/Sheba.Identity.Application/Commands/` |
| Mock civil registry (8 fixture citizens) | `Identity.Infrastructure` `MockNationalIdProvider` |
| Ministry auth adapters (5) + AES-GCM encryptor | `src/Modules/Ministry/Sheba.Ministry.Infrastructure/` |
| Workflow step engine | `src/Modules/ServiceRequest/.../StepHandlers/` |
| BI read model + reports + Hangfire | `src/Modules/Admin/` |
| Dev credentials & fixtures | [README.md](README.md) quickstart table |

## Workflow expectations

- Check [TASKS.md](TASKS.md) before starting — most known gaps already have a task ID (T-XXX-n);
  reference it in commits/PRs.
- If you change behavior described in the docs, **update the doc in the same change** —
  [docs/sheba.md](docs/sheba.md) first, then any affected sibling. Diagrams: edit the `.mmd`
  source in `docs/diagrams/` *and* the inline copy in the doc that embeds it.
- Closing a gap = also remove/annotate its row in [docs/known-issues.md](docs/known-issues.md)
  and tick [TASKS.md](TASKS.md).
- Tests per [docs/testing.md](docs/testing.md): mock NID + console OTP in tests, never live
  services; `MethodUnderTest_Scenario_ExpectedOutcome` naming.
- Run `dotnet build Sheba.sln` and `dotnet test Sheba.sln` before declaring work done.
- Keep [CHANGELOG.md](CHANGELOG.md) (`[Unreleased]`) updated for user-visible changes.

## Things agents must NOT do

See [AGENTS.md](AGENTS.md) §3 — in short: never weaken auth/authz/rate limits or OTP policy,
never log or echo secrets/PII, never add cross-module references or cross-schema SQL, never edit
applied migrations, never touch seeded production-like credentials, never "simplify" the generic
registration error into specific messages (it is an anti-enumeration control, BR-ON-3).
