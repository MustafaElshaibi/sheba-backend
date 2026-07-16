# AGENTS.md — Roles, Boundaries & Operating Rules for AI/Automation Contributors

Applies to any AI agent or automation working in this repo (Claude Code, CI bots, codegen).
Conventions live in [CLAUDE.md](CLAUDE.md); architecture truth in [docs/sheba.md](docs/sheba.md).

## 1. Source-of-truth hierarchy

1. [docs/sheba.md](docs/sheba.md) — wins every conflict.
2. Sibling docs under `docs/` — extracts; if one contradicts sheba.md, fix the sibling (or
   propose a sheba.md change explicitly — never silently diverge).
3. Code — where code and docs disagree, that is a *finding*: record it in
   [docs/known-issues.md](docs/known-issues.md) + [TASKS.md](TASKS.md) rather than quietly
   "fixing" either side.
4. The archived [docs/archive/SHEBA_ARCHITECTURE.md](docs/archive/SHEBA_ARCHITECTURE.md) is
   historical only — never implement from it.

## 2. Agent roles

| Role | Scope | Typical tasks | Off-limits |
|------|-------|---------------|-----------|
| **Feature agent** | One module + its tests | Implement a TASKS.md item; new commands/queries/endpoints | Touching other modules' internals; changing Shared.Kernel contracts without a doc update |
| **Docs agent** | `docs/`, root md files | Keep docs in sync with merged changes; diagram updates (.mmd + inline copies) | Inventing behavior not in code or sheba.md |
| **Test agent** | `tests/` | Close T-TST-* debt; add regression tests for fixed bugs | Weakening assertions to make suites pass; hitting live external services |
| **Refactor agent** | Cross-cutting, mechanical | T-API-1 (JSend), T-STD-1 (Result), T-DB-1 (migrations) | Bundling behavior changes into refactors |
| **Review agent** | Read-only | Boundary-rule audits, PII-in-logs scans, doc-drift detection | Committing changes |

One task ID per change set. If work reveals a second problem, file it (known-issues + TASKS),
don't scope-creep.

## 3. Hard boundaries (any role)

- **Security posture is one-way:** never weaken authentication, authorization policies, rate
  limits, OTP policy (TTL/attempts/throttles), lockout rules, webhook verification, or encryption.
  Loosening anything requires an explicit human-approved TASKS item.
- **Anti-enumeration:** the generic registration failure message (BR-ON-3,
  [docs/business-rules.md](docs/business-rules.md)) must stay generic.
- **Secrets & PII:** never print, log, commit, or echo credentials, tokens, OTP codes, or citizen
  PII. Dev seeds in README are the only sanctioned example values. New mock citizens go in
  `MockNationalIdProvider` only.
- **Module boundaries:** no new cross-module project references, no cross-schema SQL, no new
  Shared.Kernel query ports without a sheba.md §3.1 update in the same PR.
- **Migrations:** never edit an applied migration; additive migrations only; `EnsureCreated()` is
  banned for new work.
- **OIDC surface:** `/connect/*` behavior follows the OAuth/OIDC specs, not repo conventions —
  do not JSend-wrap it, do not add custom parameters without a documented grant/extension.
- **Destructive operations** (dropping schemas/volumes, rewriting git history, deleting docs)
  require explicit human instruction in the current session.

## 4. Definition of done

1. Builds (`dotnet build Sheba.sln`) and tests (`dotnet test Sheba.sln`) pass.
2. New behavior has tests per [docs/testing.md](docs/testing.md).
3. Docs updated in the same change (sheba.md → siblings → diagrams, as applicable).
4. [TASKS.md](TASKS.md) ticked / [docs/known-issues.md](docs/known-issues.md) row updated.
5. [CHANGELOG.md](CHANGELOG.md) `[Unreleased]` entry for user-visible changes.
6. No new warnings about boundary rules (§3).

## 5. Escalate to a human when…

- A change would alter the trust model (token lifetimes, LoA gates, approval workflow order).
- The civil-registry or SMS provider contract shape must be guessed.
- Two docs conflict and the resolution isn't obvious from sheba.md.
- A dependency upgrade touches OpenIddict, Npgsql, or cryptography packages.
