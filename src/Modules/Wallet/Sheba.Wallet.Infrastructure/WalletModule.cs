using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Wallet.Application.Commands.IssueIdentityCredential;
using Sheba.Wallet.Application.Queries.GetMyCredentials;
using Sheba.Wallet.Domain.Interfaces;
using Sheba.Wallet.Infrastructure.Credentials;
using Sheba.Wallet.Infrastructure.Persistence;
using Sheba.Wallet.Infrastructure.Persistence.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;

namespace Sheba.Wallet.Infrastructure;

public static class WalletModule
{
    public static IServiceCollection AddWalletModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WalletDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "wallet");
                    npgsql.MigrationsAssembly(typeof(WalletModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
            options.AddInterceptors(new OutboxSaveChangesInterceptor());
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<WalletDbContext>());
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<WalletDbContext>>();
        services.AddScoped<IInboxGuard, EfInboxGuard<WalletDbContext>>();

        // RSA credential signer — singleton so the RSA key is stable for the process lifetime
        services.AddSingleton<ICredentialSigner, RsaCredentialSigner>();

        return services;
    }

    public static WebApplication MapWalletEndpoints(this WebApplication app)
    {
        var wallet = app.MapGroup("/api/wallet").WithTags("Wallet")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        // ── GET /api/wallet/credentials — list the citizen's VCs with decoded claims ─────────────
        wallet.MapGet("/credentials", async (
            Guid citizenId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyCredentialsQuery(citizenId), ct);
            return Results.Ok(result);
        })
        .WithName("GetMyCredentials")
        .WithSummary("List all Verifiable Credentials for the citizen, with decoded claims.");

        // ── POST /api/wallet/credentials/issue — issue an identity VC (admin/testing) ────────────
        wallet.MapPost("/credentials/issue", async (
            IssueIdentityCredentialCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/wallet/credentials/{result.CredentialId}", result);
        })
        .WithName("IssueIdentityCredential")
        .WithSummary("Issue a W3C Digital Identity Credential (JWT/RS256) for an approved account.");

        return app;
    }
}
