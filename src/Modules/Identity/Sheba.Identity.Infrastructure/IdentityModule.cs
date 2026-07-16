using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Sheba.Identity.Application.Commands.ApproveIdentityRequest;
using Sheba.Identity.Application.Commands.CompleteRegistration;
using Sheba.Identity.Application.Commands.LoginCitizen;
using Sheba.Identity.Application.Commands.RegisterCitizen;
using Sheba.Identity.Application.Commands.RejectIdentityRequest;
using Sheba.Identity.Application.Commands.RequestLoaUpgrade;
using Sheba.Identity.Application.Commands.VerifyEmail;
using Sheba.Identity.Application.Commands.VerifyLoginOtp;
using Sheba.Identity.Application.Commands.VerifyOtp;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Application.Queries.GetAccountById;
using Sheba.Identity.Application.Queries.GetIdentityRequests;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Adapters;
using Sheba.Identity.Infrastructure.Persistence;
using Sheba.Identity.Infrastructure.Persistence.Repositories;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Infrastructure;

/// <summary>
/// Registers every service belonging to the Identity module.
///
/// Called once from Sheba.Api/Program.cs:
///     builder.Services.AddIdentityModule(builder.Configuration);
///
/// Architecture constraints:
///   • No other module may call AddIdentityModule.
///   • No other module may inject IdentityDbContext.
///   • Cross-module integration goes through IDomainEvent / IMediator only.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. DbContext — bound to the "identity" schema ─────────────────────
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "identity");
                    npgsql.MigrationsAssembly(typeof(IdentityModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });

            // Register the OpenIddict entity sets on this DbContext so a single
            // Identity migration creates both the identity tables and the
            // OpenIddict application/authorization/scope/token tables.
            options.UseOpenIddict();
        });

        // Expose IdentityDbContext as the base DbContext type as well, so the
        // startup migration runner (GetServices<DbContext>()) discovers it and
        // applies its migrations automatically. Each module does the same for
        // its own DbContext; because they are keyed by concrete type in DI,
        // there is no cross-module conflict.
        services.AddScoped<Microsoft.EntityFrameworkCore.DbContext>(
            sp => sp.GetRequiredService<IdentityDbContext>());

        // ── 2. NID adapter (civil registry) ────────────────────────────────────
        var nidProvider = configuration["NationalId:ActiveProvider"] ?? "Mock";
        if (nidProvider == "Mock")
            services.AddScoped<INationalIdProvider, MockNationalIdProvider>();
        else
            services.AddScoped<INationalIdProvider, HttpNationalIdProvider>();

        // ── 3. OTP adapter ─────────────────────────────────────────────────────
        var otpProvider = configuration["Otp:ActiveProvider"] ?? "Console";
        if (otpProvider == "Console")
            services.AddScoped<IOtpProvider, ConsoleOtpProvider>();
        else
            services.AddScoped<IOtpProvider, TwilioOtpProvider>();

        // ── 4. Named HTTP client for HttpNationalIdProvider (future-proof) ──────
        services.AddHttpClient("CivilRegistry", client =>
        {
            var baseUrl = configuration["NationalId:BaseUrl"] ?? "https://localhost:7000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // ── 5. Repository (IIdentityRepository → IdentityRepository) ──────────
        services.AddScoped<IIdentityRepository, IdentityRepository>();

        // ── 5b. Password hasher (IPasswordHasher → Argon2idPasswordHasher) ─────
        services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();

        // ── 5c. OTP hasher (IOtpHasher → Argon2idOtpHasher) ────────────────────
        services.AddScoped<IOtpHasher, Argon2idOtpHasher>();

        // ── 5d. Cross-module query service — Wallet module reads citizen data via this ──
        services.AddScoped<ICitizenAccountQueryService, CitizenAccountQueryAdapter>();

        // ── 5e. Cross-module stats — Admin module reads live KPI counts via this ──
        services.AddScoped<IIdentityStatsProvider, IdentityStatsAdapter>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(Application.Commands.RegisterCitizen.RegisterCitizenHandler).Assembly,
                typeof(Domain.Entities.Account).Assembly,
                typeof(Domain.Entities.AdminUser).Assembly);
        });

        services.AddOpenIddict()
            .AddCore(core =>
            {
                core.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>();
            })
            .AddServer(server =>
            {
                // Endpoints
                server
                    .SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetUserinfoEndpointUris("/connect/userinfo")
                    .SetIntrospectionEndpointUris("/connect/introspect")
                    .SetRevocationEndpointUris("/connect/revoke")
                    .SetLogoutEndpointUris("/connect/logout");

                // Supported grant types
                server
                    .AllowAuthorizationCodeFlow()
                        .RequireProofKeyForCodeExchange()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();

                // Custom NID+OTP grant type
                server.AllowCustomFlow("urn:sheba:grant:national_id_otp");

                // Scopes
                server.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Phone,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    "civil_data",      // NID-derived identity claims
                    "ministry_api",    // M2M access for ministries
                    "admin_api"        // Internal admin portal
                );

                // Token lifetimes
                server.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                server.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
                server.SetIdentityTokenLifetime(TimeSpan.FromMinutes(15));
                server.SetDeviceCodeLifetime(TimeSpan.FromMinutes(5));

                // Emit the access token as a plain signed JWT (not encrypted JWE) so it can
                // be inspected in jwt.io during the demo. The token is still RS256-signed and
                // validated server-side. Refresh tokens remain rotated on each use (OpenIddict
                // default), and reuse of a consumed refresh token is rejected.
                server.DisableAccessTokenEncryption();

                // Signing/encryption certificates
                if (IsProduction(configuration))
                {
                    // TODO Week 6: Load from Azure Key Vault / cert store
                    // server.AddSigningCertificate(...);
                    // server.AddEncryptionCertificate(...);
                    server.AddDevelopmentSigningCertificate();
                    server.AddDevelopmentEncryptionCertificate();
                }
                else
                {
                    server.AddDevelopmentSigningCertificate();
                    server.AddDevelopmentEncryptionCertificate();
                }

                // ASP.NET Core host integration
                server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserinfoEndpointPassthrough()
                    .EnableLogoutEndpointPassthrough()
                    .DisableTransportSecurityRequirement(); // allow HTTP in dev
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.UseAspNetCore();
            });

        return services;
    }

    /// <summary>
    /// Maps Identity module endpoints (citizen registration/login + admin review).
    /// OIDC endpoints (/connect/*, /.well-known/*) are handled automatically by
    /// OpenIddict.AspNetCore via the passthrough configured above.
    /// Called from Sheba.Api/Program.cs after app.Build().
    /// </summary>
    public static WebApplication MapIdentityEndpoints(this WebApplication app)
    {
        // ── Citizen registration + login (public) ────────────────────────────
        var citizen = app.MapGroup("/api/identity").WithTags("Identity");

        citizen.MapPost("/register", async (
            RegisterCitizenCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
        .WithName("RegisterCitizen")
        .WithSummary("Step 1: validate NID + phone, create pending account, send OTP.");

        citizen.MapPost("/verify-otp", async (
            VerifyOtpCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("VerifyOtp")
        .WithSummary("Step 2: verify the OTP sent to the citizen's phone.");

        citizen.MapPost("/complete-registration", async (
            CompleteRegistrationCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
        .WithName("CompleteRegistration")
        .WithSummary("Step 3: set username, email, password; an email verification link is sent.");

        citizen.MapPost("/verify-email", async (
            VerifyEmailCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("VerifyEmail")
        .WithSummary("Step 4: verify the email link sent after completing registration.");

        citizen.MapPost("/login", async (
            LoginCitizenCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.OtpSent ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("LoginCitizen")
        .WithSummary("Login step 1: validate credentials and dispatch a login OTP.");

        citizen.MapPost("/login/verify-otp", async (
            VerifyLoginOtpCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.Succeeded ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("VerifyLoginOtp")
        .WithSummary("Login step 2: verify the login OTP. Token issuance happens via /connect/token.");

        citizen.MapPost("/loa/upgrade", async (
            RequestLoaUpgradeCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        })
        .WithName("RequestLoaUpgrade")
        .WithSummary("Request a Level-of-Assurance upgrade (LoA 2/3) — enters the admin review queue.");

        // ── Admin identity-request review queue ───────────────────────────────
        var admin = app.MapGroup("/api/admin/identity-requests").WithTags("Admin — Identity Requests");

        admin.MapGet("/", async (
            IMediator mediator,
            RequestStatus? status,
            int page,
            int pageSize,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new GetIdentityRequestsQuery(status, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize),
                ct);
            return Results.Ok(result);
        })
        .WithName("GetIdentityRequests")
        .WithSummary("List identity requests (admin review queue).");

        admin.MapPost("/{requestId:guid}/approve", async (
            Guid requestId,
            ApproveIdentityRequestBody body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new ApproveIdentityRequestCommand(requestId, body.ReviewedByAdminId, body.Notes), ct);
            return Results.Ok(result);
        })
        .WithName("ApproveIdentityRequest")
        .WithSummary("Approve an identity request — activates the citizen account.");

        admin.MapPost("/{requestId:guid}/reject", async (
            Guid requestId,
            RejectIdentityRequestBody body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new RejectIdentityRequestCommand(requestId, body.ReviewedByAdminId, body.RejectionReason, body.Notes), ct);
            return Results.Ok(result);
        })
        .WithName("RejectIdentityRequest")
        .WithSummary("Reject an identity request with a reason.");

        // ── Admin account lookup ──────────────────────────────────────────────
        app.MapGet("/api/admin/accounts/{id:guid}", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAccountByIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithTags("Admin — Accounts")
        .WithName("GetAccountById")
        .WithSummary("Get a citizen account by ID.");

        return app;
    }

    /// <summary>Request body for the approve endpoint.</summary>
    public sealed record ApproveIdentityRequestBody(Guid ReviewedByAdminId, string? Notes = null);

    /// <summary>Request body for the reject endpoint.</summary>
    public sealed record RejectIdentityRequestBody(Guid ReviewedByAdminId, string RejectionReason, string? Notes = null);

    /// <summary>
    /// Seeds OpenIddict applications and the mock citizen registry.
    /// Called from MigrationExtensions.MigrateAllModulesAsync on startup.
    /// Idempotent — checks before inserting.
    /// </summary>
    public static async Task SeedIdentityAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentityDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // ── 1. Seed admin user ───────────────────────────────────────────────
        await SeedAdminUserAsync(db, logger);

        // ── 2. Seed OpenIddict applications ─────────────────────────────────
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        await SeedOidcApplicationAsync(
            manager, logger,
            clientId: "sheba-portal",
            displayName: "Sheba Citizen Portal",
            type: OpenIddictConstants.ClientTypes.Public,
            redirectUri: "https://localhost:4200/callback",
            postLogoutUri: "https://localhost:4200",
            scopes: new[] { "openid", "profile", "email", "phone", "civil_data", "offline_access" });

        await SeedOidcApplicationAsync(
            manager, logger,
            clientId: "sheba-admin",
            displayName: "Sheba Admin Portal",
            type: OpenIddictConstants.ClientTypes.Confidential,
            redirectUri: "https://localhost:4300/callback",
            postLogoutUri: "https://localhost:4300",
            scopes: new[] { "openid", "profile", "admin_api" },
            clientSecret: "sheba-admin-dev-secret");

        await SeedMachineClientAsync(
            manager, logger,
            clientId: "sheba-api-internal",
            displayName: "Sheba Internal API",
            clientSecret: "sheba-api-internal-dev-secret",
            scopes: new[] { "ministry_api" });

        logger.LogInformation("[IdentityModule] OpenIddict seed completed.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task SeedOidcApplicationAsync(
        IOpenIddictApplicationManager manager,
        ILogger logger,
        string clientId,
        string displayName,
        string type,
        string redirectUri,
        string postLogoutUri,
        string[] scopes,
        string? clientSecret = null)
    {
        if (await manager.FindByClientIdAsync(clientId) is not null)
        {
            logger.LogDebug("[IdentityModule] OIDC app {ClientId} already exists — skipping.", clientId);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId   = clientId,
            DisplayName = displayName,
            ClientType  = type,
            RedirectUris  = { new Uri(redirectUri) },
            PostLogoutRedirectUris = { new Uri(postLogoutUri) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
            }
        };

        // Confidential clients must present a secret; public clients (PKCE) must not.
        if (string.Equals(type, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase))
            descriptor.ClientSecret = clientSecret;

        // Public citizen-facing clients may use the custom National ID + OTP grant.
        if (string.Equals(type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase))
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Prefixes.GrantType + "urn:sheba:grant:national_id_otp");

        foreach (var scope in scopes)
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);

        await manager.CreateAsync(descriptor);
        logger.LogInformation("[IdentityModule] Seeded OIDC app: {ClientId}", clientId);
    }

    private static async Task SeedMachineClientAsync(
        IOpenIddictApplicationManager manager,
        ILogger logger,
        string clientId,
        string displayName,
        string clientSecret,
        string[] scopes)
    {
        if (await manager.FindByClientIdAsync(clientId) is not null)
        {
            logger.LogDebug("[IdentityModule] Machine client {ClientId} already exists — skipping.", clientId);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId     = clientId,
            DisplayName  = displayName,
            ClientType   = OpenIddictConstants.ClientTypes.Confidential,
            ClientSecret = clientSecret,
            Permissions  =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            }
        };

        foreach (var scope in scopes)
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);

        await manager.CreateAsync(descriptor);
        logger.LogInformation("[IdentityModule] Seeded machine client: {ClientId}", clientId);
    }

    private static bool IsProduction(IConfiguration configuration) =>
        string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"],
            "Production",
            StringComparison.OrdinalIgnoreCase);

    private static async Task SeedAdminUserAsync(IdentityDbContext db, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync())
        {
            logger.LogDebug("[IdentityModule] Admin users already seeded — skipping.");
            return;
        }

        var passwordHasher = new Argon2idPasswordHasher();
        var admin = AdminUser.Create(
            employeeId: "ADMIN001",
            email: "admin@sheba.gov",
            fullName: "Sheba Super Admin",
            role: AdminRole.SuperAdmin,
            passwordHash: passwordHasher.Hash("Admin@123"),
            department: "Platform Operations");

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "[IdentityModule] Seeded admin user ADMIN001 (admin@sheba.gov / Admin@123).");
    }
}
