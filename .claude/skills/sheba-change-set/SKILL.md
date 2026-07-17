---
name: sheba-change-set
description: End-to-end workflow for implementing any change in the Sheba backend — a TASKS.md item, a gap fix, a refactor, or a new feature. Use this whenever starting ANY implementation work in this repository, even if the user doesn't name a task ID — it enforces the module-boundary rules, the doc-sync obligations (sheba.md → sibling docs → known-issues.md → TASKS.md → CHANGELOG.md), and the build/test definition of done. Skipping it tends to produce PRs that break architecture rules or leave the docs contradicting the code.
---

# Sheba Change-Set Workflow

Every change in this repo is a "change set": one task ID, code + docs + ledger updates together,
verified by build and tests. The docs are contractual here — [docs/sheba.md](../../../docs/sheba.md)
is the source of truth and reviewers treat doc drift as a defect, so the doc updates are part of
the change, not follow-up work.

## 1. Before writing code

1. Find the task ID in [TASKS.md](../../../TASKS.md) and its context row in
   [docs/known-issues.md](../../../docs/known-issues.md). If the work has no task ID, add one
   (pattern `T-<AREA>-<n>`) in the same change set — untracked work is how ledger drift started.
2. Read the relevant section of `docs/sheba.md` **before** touching anything structural. Sibling
   docs (architecture.md, security.md, api-contract.md, …) are extracts; sheba.md wins conflicts.
3. Check which of these hard rules the change touches, and plan around them:
   - A module project references **only** `Sheba.Shared.Kernel`. Sole documented exception:
     ServiceRequest → Ministry query ports. Do not add project references between modules —
     if you need another module's event type, that's the signal the contract belongs in the
     shared kernel, not a reason to reference the producer's Domain assembly.
   - No cross-schema DB access. Cross-context references are bare `Guid`s + a comment naming the
     logical target. Never joins/FKs across schemas, never another module's repository.
   - Cross-module communication: MediatR integration events, or read-only query ports declared in
     `Sheba.Shared.Kernel.Interfaces` and implemented by the owning module.
   - Business rules live on aggregates (static factories + methods throwing `DomainException`),
     not in handlers. If a handler is making a state decision, move it to the entity.

## 2. While implementing

- Layering: Domain ← Application ← Infrastructure. DI + endpoint mapping happen only in
  `<Name>Module.cs`. No controllers.
- Errors: throw `ValidationException` (400) / `NotFoundException` (404) / `DomainException` (422);
  the middleware maps them. Every new command gets a FluentValidation validator.
- Never weaken security controls in passing: the generic registration error (BR-ON-3), OTP policy
  (5-min TTL, 3 attempts, Argon2id at rest), rate limits, authorization checks. If a message would
  tell a caller *why* identity verification failed or *whether an NID/account exists*, it is wrong.
- No PII in logs — no NIDs, phones, OTP codes, passwords, tokens. Log entity IDs; mask when a
  fragment is genuinely needed (see the `MaskNid`/`MaskPhone` helpers in Identity handlers).
- No secrets in `appsettings*.json` or code. Ministry credentials only via `ICredentialEncryptor`.
- Citizen-facing data always carries `NameAr`/`NameEn` (or `*Ar`/`*En`) pairs.
- Schema changes → follow the `sheba-migration` skill. Endpoints → follow the `sheba-endpoint`
  skill. Tests → follow the `sheba-testing` skill.

## 3. Definition of done (all of these, same change set)

1. `dotnet build Sheba.sln` clean.
2. `dotnet test Sheba.sln` green, with new/changed behavior covered per `sheba-testing`.
3. Docs updated **in this order**: `docs/sheba.md` first, then any affected sibling doc. If a
   diagram changed, edit both the `.mmd` source in `docs/diagrams/` *and* the inline mermaid copy
   in the doc that embeds it — they are duplicated on purpose and must match.
4. Ledger updated: remove/annotate the row in `docs/known-issues.md`, tick the box in `TASKS.md`.
5. `CHANGELOG.md` `[Unreleased]` entry for any user-visible change.
6. Commit message references the task ID (e.g. `T-SEC-2: add rate limiting to identity endpoints`).

## Common traps in this codebase

- `EnsureCreated()` fallback in `MigrationExtensions` — do not rely on it; it's being removed
  (T-DB-1). New entities need real migrations.
- Events currently dispatch in-process at SaveChanges (no outbox durability until T-EVT-1). Don't
  build logic that assumes an event is guaranteed to be delivered after a crash.
- `Notification.Domain` duplicates `IEmailService`/`ISmsService` from Shared.Kernel — inject the
  **Shared.Kernel** interfaces; the duplicates are slated for removal.
- Two `AdminUser` entities exist (Identity.Domain is the live one; Admin.Domain's is a stray).
- Endpoints currently return raw DTOs; the JSend retrofit is T-API-1 — don't hand-roll envelopes
  either way.
