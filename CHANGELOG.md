# Changelog

All notable changes to Sheba are documented here. Format:
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning:
[SemVer](https://semver.org/) once releases begin.

## [Unreleased]

### Added
- Master architecture & implementation plan [docs/sheba.md](docs/sheba.md) (single source of
  truth) with requirements-traceability table, STRIDE threat model, JSend API standard, and
  extraction roadmap.
- Focused doc set: architecture, coding-standards, business-rules, database-design (all 10
  bounded-context ERDs), api-contract (JSend envelopes + endpoint catalog), security,
  performance, testing, roadmap, known-issues.
- Mermaid diagram sources under `docs/diagrams/` (architecture overview, onboarding state
  machine, login+OTP sequence, ministry integration flow, service-request lifecycle, 10 ERDs).
- Root project files: README (quickstart + seeded fixtures), CLAUDE.md and AGENTS.md (AI
  contributor rules), TASKS.md (phased backlog with T-XXX ids).

### Changed
- Credential-vault encryption standard confirmed as **AES-256-GCM**, superseding the old
  ADR-011 (RSA-OAEP) — see [docs/security.md](docs/security.md) §3.
- API response standard set to **JSend** (retrofit tracked as T-API-1); RFC 7807 responses remain
  in code until then.

### Deprecated
- `docs/SHEBA_ARCHITECTURE.md` moved to [docs/archive/](docs/archive/) — historical reference
  only; superseded by [docs/sheba.md](docs/sheba.md).

[Unreleased]: https://example.invalid/sheba/compare/HEAD
