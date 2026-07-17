---
name: sheba-migration
description: Create, apply, and verify EF Core migrations for Sheba's schema-per-module PostgreSQL database. Use this whenever an entity or EF configuration changes, when giving a module its first migration (T-DB-1), when adding a new table/column/index, or when diagnosing "relation does not exist" / migration-history errors. Never rely on EnsureCreated and never edit an applied migration — this skill exists because both mistakes are live risks in this repo.
---

# Sheba EF Core Migrations

One PostgreSQL database (`sheba`), one schema per module, one `DbContext` per module, each with
its own `__ef_migrations` history table **inside its own schema** (already configured via
`MigrationsHistoryTable("__ef_migrations", "<schema>")` in each `<Name>Module.cs`).

## Module → context → schema map

| Module | DbContext | Schema |
|--------|-----------|--------|
| Identity | `IdentityDbContext` | `identity` (also owns the OpenIddict tables via `UseOpenIddict()`) |
| Citizen | `CitizenDbContext` | `citizen` |
| Ministry | `MinistryDbContext` | `ministry` |
| ServiceRequest | `ServiceRequestDbContext` | `service_req` |
| Document | `DocumentDbContext` | `document` |
| Wallet | `WalletDbContext` | `wallet` |
| Payment | `PaymentDbContext` | `payment` |
| Notification | `NotificationDbContext` | `notification` |
| Audit | `AuditDbContext` | `audit` |
| Admin | `AdminDbContext` | `admin_data` |

## The command (run from the repo root)

```
dotnet ef migrations add <PascalCaseName> `
  --project src/Modules/<Module>/Sheba.<Module>.Infrastructure `
  --startup-project src/Sheba.Api `
  --context <Module>DbContext
```

`--context` is **required**: the startup project registers all ten DbContexts, so `dotnet ef`
cannot pick one by itself. Migrations land in the module's `Persistence/Migrations/` folder
(only Identity has one today — creating the folder via a first `InitialCreate` migration per
module is exactly what T-DB-1 asks for).

Apply / verify locally (requires the compose Postgres up):

```
dotnet ef database update --project src/Modules/<Module>/Sheba.<Module>.Infrastructure `
  --startup-project src/Sheba.Api --context <Module>DbContext
```

Startup also applies pending migrations automatically (`MigrateAllModulesAsync`).

## Rules

1. **Migrations are the only schema-change mechanism.** Never call `EnsureCreated`, never hand-run
   DDL. The `EnsureCreated()` fallback in `MigrationExtensions.cs` exists only for modules that
   don't have migrations yet; it cannot evolve a schema and is being deleted under T-DB-1.
2. **Never edit a migration that may have been applied anywhere** (including a teammate's dev DB).
   Fix forward with a new migration.
3. **First migration for a module that ran under `EnsureCreated`:** the tables already exist but
   the history table doesn't, so `MigrateAsync` would fail on CREATE TABLE. Either verify against
   a clean volume (`docker compose down -v && docker compose up`), or baseline explicitly —
   decide per T-DB-1's plan, don't improvise silently.
4. **Naming:** snake_case tables/columns via the explicit `HasColumnName`/`ToTable` calls in
   `Persistence/Configurations/` — new properties must set them; don't rely on default naming.
5. **Review the generated migration before committing.** Check: correct schema on every table,
   no accidental drops, indexes for new FK/lookup columns, and that a column type change has a
   safe data conversion.
6. Cross-schema FKs must never appear in a migration — if one shows up, a navigation property is
   crossing a module boundary; fix the model, not the migration.
7. After generating: `dotnet build Sheba.sln`, then boot against a **clean volume** and confirm
   startup logs show `Migrating <Context>...` (not `ensuring schema via EnsureCreated`), then run
   `dotnet test Sheba.sln`.
