using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Audit.Application.Interfaces;
using Sheba.Audit.Application.Queries.GetAuditLog;
using Sheba.Audit.Infrastructure.Persistence;
using Sheba.Audit.Infrastructure.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;

namespace Sheba.Audit.Infrastructure;

/// <summary>
/// Registers every service belonging to the Audit module.
///
/// Architecture constraints:
///   • The audit schema is append-only (INSERT only for the app DB user).
///   • No other module may inject AuditDbContext directly.
///   • The AuditLoggingBehavior is registered globally in Program.cs (not here)
///     because it's an IPipelineBehavior that runs for ALL modules' commands.
/// </summary>
public static class AuditModule
{
    public static IServiceCollection AddAuditModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext — bound to the "audit" schema ──────────────────────────
        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "audit");
                    npgsql.MigrationsAssembly(typeof(AuditModule).Assembly.FullName);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxSaveChangesInterceptor()));

        // Expose as base DbContext so the startup migration runner discovers this context (T-DB-1).
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AuditDbContext>());

        // ── Repository ───────────────────────────────────────────────────────
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<AuditDbContext>>();
        services.AddScoped<IInboxGuard, EfInboxGuard<AuditDbContext>>();

        return services;
    }

    /// <summary>
    /// Maps audit admin endpoints under /api/admin/audit.
    /// </summary>
    public static WebApplication MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/audit")
            .WithTags("Audit")
            .RequireAuthorization("Auditor") // T-AUTH-2 — append-only audit trail, SuperAdmin/Auditor only
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        group.MapGet("/", async (
            Guid? actorId,
            string? entityType,
            string? action,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetAuditLogQuery(
                ActorId: actorId,
                EntityType: entityType,
                Action: action,
                From: from,
                To: to,
                Page: page ?? 1,
                PageSize: pageSize ?? 25);

            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetAuditLog")
        .WithSummary("Paginated audit log with filtering by actor, entity type, date range, and action")
        .Produces<GetAuditLogResponse>(200);

        return app;
    }
}
