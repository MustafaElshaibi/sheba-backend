# Sheba — e-Government Platform for Yemen

Sheba (سبأ) is a national digital-government backend: one **modular monolith** that is both the
country's **OpenID Connect / OAuth 2.1 Identity Provider** (OpenIddict) and its **government
service gateway**. Citizens verified against the civil registry get an admin-approved digital
identity and use it everywhere ("Sign in with Sheba" — think UAE Pass / Absher); ministries plug
in as relying parties and as integration targets fulfilled through a credential vault, pluggable
auth adapters, and a step-based service-request workflow engine.

**Stack:** .NET 9 · ASP.NET Core (minimal APIs) · OpenIddict · EF Core + PostgreSQL 16
(schema-per-module) · MediatR · Redis · MinIO · Hangfire · Serilog + Seq · Docker Compose.

## Quickstart

Prereqs: Docker Desktop. (For local dev outside containers: .NET 9 SDK.)

```bash
docker compose up --build
```

| Service | URL |
|---------|-----|
| API + Swagger | http://localhost:5000/swagger |
| OIDC discovery | http://localhost:5000/.well-known/openid-configuration |
| MailHog (dev email inbox) | http://localhost:8025 |
| Seq (logs) | http://localhost:8081 |
| MinIO console | http://localhost:9001 |
| Hangfire dashboard | http://localhost:5000/hangfire |

Startup applies migrations and seeds dev data automatically. Dev providers are pre-selected via
env: `NationalId__ActiveProvider=Mock`, `Otp__ActiveProvider=Console` (OTP codes appear in the
API container logs / Seq).

### Seeded credentials & fixtures (development only)

| What | Value |
|------|-------|
| Admin user | `ADMIN001` / `admin@sheba.gov` / `Admin@123` (SuperAdmin) |
| OIDC clients | `sheba-portal` (public, PKCE + custom grant), `sheba-admin` (confidential, secret `sheba-admin-dev-secret`), `sheba-api-internal` (client_credentials, `ministry_api`) |
| Mock citizens — valid | `1000000001` … `1000000004` (phone numbers in mock provider source) |
| Mock citizens — failure cases | `1000000099` deceased · `1000000098` suspended · `1000000097` expired NID · `1000000096` phone mismatch |

Try the onboarding flow end-to-end: `POST /api/identity/register` with a valid mock citizen →
grab the OTP from logs → `verify-otp` → `complete-registration` → click the email link in
MailHog → approve in the admin queue (`/api/admin/identity-requests`) → log in.

## Documentation

**Start here → [docs/sheba.md](docs/sheba.md)** — the master architecture & implementation plan
(single source of truth). Focused extracts:

| Doc | Contents |
|-----|----------|
| [docs/architecture.md](docs/architecture.md) | Module boundaries, communication rules, extraction path |
| [docs/coding-standards.md](docs/coding-standards.md) | Layering, conventions, DI, error handling |
| [docs/business-rules.md](docs/business-rules.md) | Onboarding, login/OTP, approval, catalog & request rules |
| [docs/database-design.md](docs/database-design.md) | Schema-per-module, all ERDs, PII & encryption map |
| [docs/api-contract.md](docs/api-contract.md) | JSend envelopes, HTTP mapping, endpoint catalog |
| [docs/security.md](docs/security.md) | Auth flows, secrets, key rotation, threat model |
| [docs/performance.md](docs/performance.md) | Caching, read models, bottlenecks, scaling ladder |
| [docs/testing.md](docs/testing.md) | Test tiers, mock NID + console OTP in tests |
| [docs/roadmap.md](docs/roadmap.md) | Phased milestones with build order |
| [docs/known-issues.md](docs/known-issues.md) | Honest gap ledger & open decisions |
| [docs/diagrams/](docs/diagrams/) | All Mermaid diagram sources (.mmd) |

Backlog: [TASKS.md](TASKS.md) · Changes: [CHANGELOG.md](CHANGELOG.md) · AI-contributor rules:
[CLAUDE.md](CLAUDE.md), [AGENTS.md](AGENTS.md).

## Repository layout

```
src/Sheba.Api/            # single host: middleware, MediatR pipeline, module composition
src/Sheba.Shared.Kernel/  # BaseEntity, domain-event marker, value objects, cross-module ports
src/Modules/<Name>/       # 10 modules × (Domain / Application / Infrastructure)
tests/                    # per-module unit tests + integration tests
docker-compose.yml        # api + postgres + redis + minio + mailhog + seq
```

## Development

```bash
dotnet build Sheba.sln
dotnet test Sheba.sln
# new migration (per module):
dotnet ef migrations add <Name> \
  --project src/Modules/<M>/Sheba.<M>.Infrastructure \
  --startup-project src/Sheba.Api
```

License/status: graduation → pilot project; not yet a production national deployment. See
[docs/known-issues.md](docs/known-issues.md) before trusting any security property.
