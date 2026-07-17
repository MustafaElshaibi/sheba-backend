# Coding Standards & Conventions

> Extract of [sheba.md](sheba.md) §15 plus repo-specific conventions. sheba.md wins conflicts.

## 1. Layering (per module)

| Layer | Contains | May reference |
|-------|----------|---------------|
| Domain | Entities (aggregates), enums, domain events, ports (interfaces), value objects | Shared.Kernel only |
| Application | Commands/queries (records), handlers, FluentValidation validators, DTOs | Domain, Shared.Kernel |
| Infrastructure | DbContext + configurations, repositories, external adapters, `<Name>Module.cs` | Application, Domain, Shared.Kernel |

Never: Domain → Application, any layer → another module, Application → EF Core types.

## 2. Domain conventions

- Entities inherit `BaseEntity` (Guid id, CreatedAt/UpdatedAt, domain-event list). `sealed` classes,
  `private` parameterless ctor for EF, **static factory methods** (`Account.CreateFromNidCheck`,
  `Ministry.Create`) that enforce invariants and throw `DomainException` on violation.
- State changes go through intention-revealing methods (`Approve()`, `MarkPaid()`, `Expire()`),
  never public setters. Call `Touch()` on mutation.
- Raise domain events inside aggregate methods (`RaiseDomainEvent(new …Event(...))`), at the state
  transition that makes the fact true — not in handlers.
- Value objects (`Email`, `NationalId`, `PhoneNumber`) validate on construction.

## 3. Application conventions (CQRS + MediatR)

- Commands: `VerbNounCommand` records; queries: `GetXQuery` records; one handler per file,
  `<CommandName>Handler`.
- Handlers orchestrate: load aggregate via repository → call domain method → persist → return DTO.
  Business rules live on aggregates, not in handlers.
- Every command has a FluentValidation validator (picked up by `ValidationBehavior`). Validator
  messages are user-facing — write them accordingly; keys mirror input field names (they become
  JSend `fail` data keys).
- Commands that mutate state implement `ITransactionalCommand`.
- Two sanctioned styles for expected failures — pick one per module, never mix within a module:
  (a) throw `NotFoundException` / `DomainException` / `ValidationException` (mapped centrally to
  JSend by the exception middleware) — the default for modules not yet converted; or (b) return
  `Result<T>` from `Sheba.Shared.Kernel.Results` and let the endpoint call
  `result.ToHttpResult()`, which renders the *same* JSend shape/status as the exception it
  replaces. Identity uses (b) end-to-end (T-STD-1); every other module still uses (a).

## 4. API conventions

- Minimal APIs only, mapped in `<Name>Module.cs` route groups (see route table in
  [sheba.md §3.3](sheba.md#33-module-map)). No controllers.
- Kebab-case routes, plural resources (`/api/admin/identity-requests`). `camelCase` JSON.
- **Never hand-roll a response envelope.** Return DTOs (or `Results.NotFound()` etc.); the JSend
  result filter and exception middleware produce the envelopes
  ([api-contract.md](api-contract.md)). `/connect/*` and `/.well-known/*` are exempt (OIDC
  protocol shapes).
- Every endpoint declares `.RequireAuthorization(<policy>)` explicitly, or `.AllowAnonymous()`
  with a comment saying why.

## 5. Persistence conventions

- One DbContext per module, `HasDefaultSchema("<schema>")`, snake_case table/column names via
  configuration classes under `Persistence/Configurations/` (one per entity).
- All schema changes via EF migrations (`dotnet ef migrations add <Name> --project
  src/Modules/<M>/Sheba.<M>.Infrastructure --startup-project src/Sheba.Api`). `EnsureCreated()` is
  a temporary fallback being removed (T-DB-1) — never rely on it for new work.
- No lazy loading. Explicit `Include` for aggregate children only. Cross-context IDs are plain
  `Guid` properties with a comment naming the logical target.
- Store enums as ints (EF default here), JSON payloads in `jsonb` columns with a `*_json` suffix
  on the property.

## 6. DI & configuration

- Each module self-registers in `Add<Name>Module`. Provider selection (NID, OTP) via the Options
  pattern reading `NationalId:ActiveProvider` / `Otp:ActiveProvider` — new providers register
  under a code and are chosen by config, never by `#if` or environment checks in code.
- Secrets never in appsettings.json: user-secrets in dev, environment/secret store in prod
  ([security.md §2](security.md)).
- HTTP calls to ministries/registry go through named clients (`MinistryClient`, `CivilRegistry`)
  with the standard resilience handler — never `new HttpClient()`.

## 7. Logging & errors

- Serilog structured logging; message templates with named properties, no string interpolation.
- **No-PII rule:** national IDs, phone numbers, OTP codes, passwords, tokens, and credential
  material never appear in logs at any level. Log entity IDs instead.
- Exceptions bubble to the global handler; catch only where you add value (adapters wrapping
  external failures into typed results).

## 8. Naming & style

- C# 13, nullable reference types on, implicit usings on (see `Directory.Build.props`).
- `sealed` by default; records for messages/DTOs; file-scoped namespaces.
- Bilingual fields are paired `NameAr`/`NameEn` (`name_ar`/`name_en` in DB) — every citizen-facing
  label needs both.
- Async everywhere with `CancellationToken ct` as the last parameter, honored in queries.

## 9. Tests

Conventions and layout in [testing.md](testing.md): xUnit + FluentAssertions + NSubstitute;
`MethodUnderTest_Scenario_ExpectedOutcome` naming; mock NID provider + console OTP in tests, never
live services.
