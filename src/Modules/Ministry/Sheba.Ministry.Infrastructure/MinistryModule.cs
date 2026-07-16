using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Ministry.Application.Commands.CreateMinistry;
using Sheba.Ministry.Application.Commands.RegisterMinistryEndpoint;
using Sheba.Ministry.Application.Commands.RegisterMinistryWebhook;
using Sheba.Ministry.Application.Commands.SetMinistryAuthConfig;
using Sheba.Ministry.Application.Commands.TestMinistryConnection;
using Sheba.Ministry.Application.Commands.UpdateMinistry;
using Sheba.Ministry.Application.Queries.GetMinistries;
using Sheba.Ministry.Application.Queries.GetMinistryById;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Ministry.Infrastructure.Adapters;
using Sheba.Ministry.Infrastructure.Persistence;
using Sheba.Ministry.Infrastructure.Persistence.Repositories;
using Sheba.Ministry.Infrastructure.Security;

namespace Sheba.Ministry.Infrastructure;

/// <summary>
/// Registers all services belonging to the Ministry module.
///
/// Called once from Sheba.Api/Program.cs:
///     builder.Services.AddMinistryModule(builder.Configuration);
///
/// Architecture constraints:
///   - No other module may call AddMinistryModule.
///   - No other module may inject MinistryDbContext.
///   - Cross-module data goes through MediatR INotification only.
/// </summary>
public static class MinistryModule
{
    public static IServiceCollection AddMinistryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. DbContext ──────────────────────────────────────────────────────
        services.AddDbContext<MinistryDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "ministry");
                    npgsql.MigrationsAssembly(typeof(MinistryModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        // Register as base DbContext for automatic migration discovery
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<MinistryDbContext>());

        // ── 2. Repository ─────────────────────────────────────────────────────
        services.AddScoped<IMinistryRepository, MinistryRepository>();

        // ── 3. Credential encryption (AES-256-GCM) ────────────────────────────
        services.AddSingleton<ICredentialEncryptor, AesGcmCredentialEncryptor>();

        // ── 4. Auth adapters (registered as IMinistryAuthAdapter collection) ──
        services.AddScoped<OidcMinistryAuthAdapter>();
        services.AddScoped<IMinistryAuthAdapter, OidcMinistryAuthAdapter>(sp =>
            sp.GetRequiredService<OidcMinistryAuthAdapter>());
        services.AddScoped<IMinistryAuthAdapter, OAuth2MinistryAuthAdapter>();
        services.AddScoped<IMinistryAuthAdapter, ApiKeyMinistryAuthAdapter>();
        services.AddScoped<IMinistryAuthAdapter, BearerTokenMinistryAuthAdapter>();
        services.AddScoped<IMinistryAuthAdapter, BasicAuthMinistryAuthAdapter>();

        return services;
    }

    /// <summary>
    /// Maps Ministry module API endpoints.
    /// Called from Sheba.Api/Program.cs after app.Build().
    /// </summary>
    public static WebApplication MapMinistryEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/ministry").WithTags("Ministry");

        // ── GET /api/ministry — list all ministries ──────────────────────────
        api.MapGet("/", async (IMediator mediator, bool? includeInactive, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMinistriesQuery(includeInactive ?? false), ct);
            return Results.Ok(result);
        })
        .WithName("GetMinistries")
        .WithSummary("List all ministries (optionally include inactive).");

        // ── GET /api/ministry/{id} — get ministry detail ─────────────────────
        api.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMinistryByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetMinistryById")
        .WithSummary("Get full ministry detail with auth configs, endpoints, and webhooks.");

        // ── POST /api/ministry — create ministry ─────────────────────────────
        api.MapPost("/", async (CreateMinistryCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/ministry/{result.MinistryId}", result);
        })
        .WithName("CreateMinistry")
        .WithSummary("Register a new ministry or sub-ministry.");

        // ── PUT /api/ministry/{id} — update ministry ─────────────────────────
        api.MapPut("/{id:guid}", async (Guid id, UpdateMinistryCommand command, IMediator mediator, CancellationToken ct) =>
        {
            // Ensure the route ID matches the command
            var cmd = command with { MinistryId = id };
            var result = await mediator.Send(cmd, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateMinistry")
        .WithSummary("Update ministry details.");

        // ── POST /api/ministry/{id}/auth-config — set auth configuration ─────
        api.MapPost("/{id:guid}/auth-config", async (
            Guid id, SetMinistryAuthConfigCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = command with { MinistryId = id };
            var result = await mediator.Send(cmd, ct);
            return Results.Created($"/api/ministry/{id}/auth-config/{result.AuthConfigId}", result);
        })
        .WithName("SetMinistryAuthConfig")
        .WithSummary("Add an auth configuration with encrypted credentials.");

        // ── POST /api/ministry/{id}/test-connection — test ministry API ──────
        api.MapPost("/{id:guid}/test-connection", async (
            Guid id, TestConnectionBody body, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new TestMinistryConnectionCommand(body.AuthConfigId), ct);
            return Results.Ok(result);
        })
        .WithName("TestMinistryConnection")
        .WithSummary("Test connectivity to a ministry API using the selected auth config.");

        // ── POST /api/ministry/{id}/endpoints — register endpoint ────────────
        api.MapPost("/{id:guid}/endpoints", async (
            Guid id, RegisterMinistryEndpointCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = command with { MinistryId = id };
            var result = await mediator.Send(cmd, ct);
            return Results.Created($"/api/ministry/{id}/endpoints/{result.EndpointId}", result);
        })
        .WithName("RegisterMinistryEndpoint")
        .WithSummary("Register a new API endpoint for this ministry.");

        // ── POST /api/ministry/{id}/webhooks — register webhook ──────────────
        api.MapPost("/{id:guid}/webhooks", async (
            Guid id, RegisterMinistryWebhookCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = command with { MinistryId = id };
            var result = await mediator.Send(cmd, ct);
            return Results.Created($"/api/ministry/{id}/webhooks/{result.WebhookId}", result);
        })
        .WithName("RegisterMinistryWebhook")
        .WithSummary("Register a webhook for ministry callback notifications.");

        return app;
    }

    /// <summary>Body for the test-connection endpoint.</summary>
    public sealed record TestConnectionBody(Guid AuthConfigId);
}
