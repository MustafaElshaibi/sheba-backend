# Architecture — Modular Monolith, Module Boundaries, Communication

> Extract of [sheba.md](sheba.md) §3, §5, §11, §15. If anything here conflicts with
> [sheba.md](sheba.md), sheba.md wins.

## 1. Style

One ASP.NET Core 9 deployable (`Sheba.Api`), one PostgreSQL database, ten modules, each a bounded
context with its own Domain / Application / Infrastructure projects and its own DB schema. The
system diagram is in [sheba.md §3.2](sheba.md#32-system-diagram)
(source: [diagrams/architecture-overview.mmd](diagrams/architecture-overview.mmd)).

## 2. Solution layout (actual, in-repo)

```
Sheba.sln
├── src/
│   ├── Sheba.Api/                      # Host: Program.cs, middleware, MediatR pipeline behaviors
│   ├── Sheba.Shared.Kernel/            # BaseEntity, IDomainEvent, exceptions, value objects,
│   │                                   #   cross-module query ports, JSend + outbox/inbox primitives
│   │                                   #   (target: Result<T>, T-STD-1)
│   └── Modules/<Name>/
│       ├── Sheba.<Name>.Domain/        # Entities, enums, domain events, ports (interfaces)
│       ├── Sheba.<Name>.Application/   # Commands, queries, handlers, validators, DTOs
│       └── Sheba.<Name>.Infrastructure/# DbContext, repositories, adapters, <Name>Module.cs
│                                       #   (DI registration + minimal-API endpoint mapping)
└── tests/
    ├── Sheba.Identity.Tests/           # unit tests (real coverage exists here)
    ├── Sheba.Ministry.Tests/           # placeholder — see testing.md
    ├── Sheba.ServiceRequest.Tests/     # placeholder — see testing.md
    └── Sheba.Integration.Tests/        # placeholder — see testing.md
```

Modules: `Identity`, `Citizen`, `Ministry`, `ServiceRequest`, `Document`, `Wallet`, `Payment`,
`Notification`, `Audit`, `Admin`. The **Gateway** module is not a project — it is the host's
middleware pipeline (see §6 below).

Each `<Name>Module.cs` exposes two extension methods: `Add<Name>Module(IServiceCollection, IConfiguration)`
(DI + DbContext + adapters) and `Map<Name>Endpoints(IEndpointRouteBuilder)` (route groups).
`Program.cs` composes all ten fluently.

## 3. Module boundary rules (enforced)

1. **No cross-module project references.** A module project references only
   `Sheba.Shared.Kernel`. Never another module's Domain/Application/Infrastructure.
2. **No cross-schema DB access.** Each DbContext sets `HasDefaultSchema(...)`; no FK constraints,
   joins, or queries across schemas. Cross-context references are bare UUIDs
   ([sheba.md §8.2](sheba.md#82-cross-context-references)).
3. **Communication channel A — integration events:** MediatR `INotification` records implementing
   `IDomainEvent`, raised by aggregates, dispatched via the transactional outbox
   ([sheba.md §11](sheba.md#11-event-driven-design)). Payloads carry IDs and minimal facts, no PII
   dumps.
4. **Communication channel B — shared-kernel query ports:** narrow read-only interfaces declared
   in `Sheba.Shared.Kernel.Interfaces` (`ICitizenAccountQueryService`, `IIdentityStatsProvider`),
   implemented by the owning module as an adapter, consumed by others via DI. Add a new port only
   when an event-carried ID is not enough; keep ports read-only.
5. **Documented exception:** ServiceRequest consumes Ministry's `IMinistryRepository` +
   `IMinistryAuthAdapter` in-process. This is deliberate — the two modules extract together as the
   integration pair ([sheba.md §3.4](sheba.md#34-migration-path-to-services)). Do not add further
   exceptions.

## 4. Request pipeline

```
HTTPS → ExceptionHandler (JSend error/fail) → Serilog request logging → RateLimiter (T-SEC-2)
     → CORS → Authentication (OpenIddict) → Authorization (policies) → JSend result filter
     → Module endpoint group → MediatR:
        LoggingBehavior → ValidationBehavior → AuthorizationBehavior → Transaction+Outbox → Handler
```

The Transaction behavior wraps commands marked `ITransactionalCommand` in an EF transaction (a real
`IUnitOfWork` per module now backs this, T-EVT-1 — though no command has adopted the marker yet). A
`SaveChangesInterceptor` — not the Transaction behavior — flushes raised domain events into the
module's outbox table on every `SaveChanges` call, transaction or not; see
[sheba.md §11.1](sheba.md#111-transactional-outbox-the-reliability-backbone).

## 5. Module communication picture

- Identity is the **event root**: `AccountRegisteredEvent`, `IdentityRequestSubmittedEvent`,
  `IdentityRequestDecidedEvent`.
- Fan-out on approval: Citizen creates the profile, Wallet issues the identity VC, Notification
  emails the citizen, Admin updates BI snapshots.
- ServiceRequest emits submission/step/completion events consumed by Notification and Admin.
- Full catalog: [sheba.md §11.2](sheba.md#112-integration-event-catalog).

## 6. Gateway module

Cross-cutting concerns owned by the host: routing to module endpoint groups, authN/Z enforcement,
rate limiting, JSend response wrapping, correlation IDs, CORS. When the first module is extracted,
these responsibilities move to a YARP reverse-proxy container unchanged in meaning — which is why
they are named as a module even though they have no schema or entities.

## 7. Extraction path

Order and mechanics: [sheba.md §3.4](sheba.md#34-migration-path-to-services). Summary:
Notification → Document → (ServiceRequest + Ministry) → … → Identity last. Per extraction: own
host + own DB (schema moves), outbox dispatcher publishes to RabbitMQ instead of MediatR, query
ports become HTTP/gRPC clients behind the same interfaces.
