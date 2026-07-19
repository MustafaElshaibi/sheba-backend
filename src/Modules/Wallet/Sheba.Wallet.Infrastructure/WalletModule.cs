using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sheba.Wallet.Application.Commands.IssueIdentityCredential;
using Sheba.Wallet.Application.Queries.GetCredentialById;
using Sheba.Wallet.Application.Queries.GetDidDocument;
using Sheba.Wallet.Application.Queries.GetMyCredentials;
using Sheba.Wallet.Application.Queries.GetRevocationStatus;
using Sheba.Wallet.Application.Queries.VerifyCredential;
using Sheba.Wallet.Domain.Interfaces;
using Sheba.Wallet.Infrastructure.Credentials;
using Sheba.Wallet.Infrastructure.Persistence;
using Sheba.Wallet.Infrastructure.Persistence.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;
using Sheba.Shared.Kernel.Security;

namespace Sheba.Wallet.Infrastructure;

public static class WalletModule
{
    /// <summary>
    /// Registers Wallet module services.
    /// T-WAL-1: throws <see cref="InvalidOperationException"/> on startup in non-Development
    /// environments if <c>Wallet:IssuerPrivateKeyPem</c> is not configured, so an operator
    /// misconfiguration is caught before the first VC issue (not at credential-issue time).
    /// </summary>
    public static IServiceCollection AddWalletModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // T-WAL-1: guard — ephemeral key is only acceptable in Development
        if (!environment.IsDevelopment() &&
            string.IsNullOrWhiteSpace(configuration["Wallet:IssuerPrivateKeyPem"]))
        {
            throw new InvalidOperationException(
                "Wallet:IssuerPrivateKeyPem must be configured in non-Development environments. " +
                "Generate a 2048-bit RSA private key (PKCS#8 PEM) and set it as a secret " +
                "before starting in production. See docs/sheba.md §5.6 for key rotation guidance.");
        }
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
        // Wallet.SubjectId/AccountId is the same Guid as Identity's AccountId (cross-context id,
        // rule 2) — the token "sub" claim IS the citizen's wallet subject, no lookup needed.
        var wallet = app.MapGroup("/api/wallet").WithTags("Wallet")
            .RequireAuthorization("CitizenOnly")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        // ── GET /api/wallet/credentials — list the caller's own VCs with decoded claims ──────────
        wallet.MapGet("/credentials", async (
            ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyCredentialsQuery(user.RequireSubjectId()), ct);
            return Results.Ok(result);
        })
        .WithName("GetMyCredentials")
        .WithSummary("List all Verifiable Credentials for the calling citizen, with decoded claims.");

        // ── GET /api/wallet/credentials/{id} — single-credential detail, owner-only ──────────────
        wallet.MapGet("/credentials/{id:guid}", async (
            Guid id, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new GetCredentialByIdQuery(id, user.RequireSubjectId(), IsAdmin: false), ct);
            return Results.Ok(result);
        })
        .WithName("GetCredentialById")
        .WithSummary("Get a single Verifiable Credential owned by the calling citizen (JWT + decoded claims).");

        // ── Public verification/presentation surface (T-WAL-2, BR-WA-2) ──────────────────────────
        // No auth: the entire point of a VC-JWT presentation flow is that the verifier (a ministry
        // portal, another citizen, an external system) does not need a Sheba account.
        var walletPublic = app.MapGroup("/api/wallet").WithTags("Wallet — Verification")
            .AllowAnonymous() // public by design — see summary above
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        walletPublic.MapPost("/verify", async (
            VerifyCredentialBody body, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new VerifyCredentialQuery(body.Jwt), ct);
            return Results.Ok(result);
        })
        .WithName("VerifyCredential")
        .WithSummary("Verify a presented VC-JWT's signature, expiry, and revocation status.");

        walletPublic.MapGet("/credentials/{id:guid}/revocation-status", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRevocationStatusQuery(id), ct);
            return Results.Ok(result);
        })
        .WithName("GetCredentialRevocationStatus")
        .WithSummary("Cheap revocation-status check by credential ID — no claims exposed.");

        walletPublic.MapGet("/did/{did}", async (
            string did, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDidDocumentQuery(did), ct);
            return Results.Ok(result);
        })
        .WithName("ResolveDidDocument")
        .WithSummary("Resolve a did:sheba:* DID document (issuer or citizen) to its public key.");

        // ── POST /api/admin/wallet/credentials/issue — force-issue a VC outside the normal ───────
        // approval-triggered flow (IdentityRequestDecidedEvent already issues one automatically).
        // Admin-only: an arbitrary AccountId in the body means this must never be citizen-callable.
        var walletAdmin = app.MapGroup("/api/admin/wallet").WithTags("Wallet — Admin")
            .RequireAuthorization("IdentityReviewer")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        walletAdmin.MapPost("/credentials/issue", async (
            IssueIdentityCredentialCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created($"/api/wallet/credentials/{result.CredentialId}", result);
        })
        .WithName("IssueIdentityCredential")
        .WithSummary("Admin: force-issue a W3C Digital Identity Credential for an approved account (testing/manual re-issue).");

        return app;
    }

    /// <summary>Request body for POST /api/wallet/verify — kept out of the query record so the
    /// query stays a clean MediatR message rather than an ASP.NET binding type.</summary>
    public sealed record VerifyCredentialBody(string Jwt);
}
