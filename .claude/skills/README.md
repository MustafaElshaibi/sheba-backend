# .claude/skills — Project Skills (placeholder)

Project-specific Claude Code skills live here, one directory per skill with a `SKILL.md`.

Planned candidates (create when the workflow stabilizes):

- `add-module-endpoint/` — checklist-driven: command + validator + handler + endpoint mapping +
  JSend contract test + doc touch-points.
- `add-migration/` — the exact `dotnet ef` invocation per module + review checklist
  (see [../../docs/database-design.md](../../docs/database-design.md) §4).
- `sync-docs/` — after a merged change, walk sheba.md → siblings → diagrams for drift.

Rules for any skill added here: respect [AGENTS.md](../../AGENTS.md) hard boundaries; docs
updated in the same change set; reference TASKS.md ids.
