using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Sheba.Identity.Application.Commands.ApproveIdentityRequest;
using Sheba.Identity.Application.Commands.CompleteRegistration;
using Sheba.Identity.Application.Commands.ConfirmAdminMfa;
using Sheba.Identity.Application.Commands.CreateAdminUser;
using Sheba.Identity.Application.Commands.EnrollAdminMfa;
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
using Sheba.Shared.Kernel.RateLimiting;
using Sheba.Shared.Kernel.Results;
using Sheba.Shared.Kernel.Security;

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

            // Outbox interceptor (T-EVT-1): converts raised domain events into outbox_messages
            // rows in the same SaveChanges call as the aggregate write. Stateless — one shared
            // instance is safe across the DbContext pool.
            options.AddInterceptors(new Sheba.Shared.Kernel.Outbox.OutboxSaveChangesInterceptor());
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

        // ── 5a. Unit of work + inbox guard (T-EVT-1) ───────────────────────────
        services.AddScoped<IUnitOfWork, Sheba.Shared.Kernel.Persistence.EfUnitOfWork<IdentityDbContext>>();
        services.AddScoped<IInboxGuard, Sheba.Shared.Kernel.Persistence.EfInboxGuard<IdentityDbContext>>();

        // ── 5b. Password hasher (IPasswordHasher → Argon2idPasswordHasher) ─────
        services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();

        // ── 5c. OTP hasher (IOtpHasher → Argon2idOtpHasher) ────────────────────
        services.AddScoped<IOtpHasher, Argon2idOtpHasher>();

        // ── 5d. Cross-module query service — Wallet module reads citizen data via this ──
        services.AddScoped<ICitizenAccountQueryService, CitizenAccountQueryAdapter>();

        // ── 5e. Cross-module stats — Admin module reads live KPI counts via this ──
        services.AddScoped<IIdentityStatsProvider, IdentityStatsAdapter>();

        // ── 5f. Admin TOTP MFA (T-SEC-1 in progress) — both adapters are stateless ──
        services.AddSingleton<Domain.Interfaces.IMfaSecretEncryptor, Security.AesGcmMfaSecretEncryptor>();
        services.AddSingleton<Domain.Interfaces.ITotpService, Security.OtpNetTotpService>();

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

                // Custom grant types: citizen NID+OTP login, admin password login
                server.AllowCustomFlow("urn:sheba:grant:national_id_otp");
                server.AllowCustomFlow(Oidc.OidcEndpoints.ShebaAdminGrantType);

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

                // Signing/encryption certificates — rotation-by-overlap (T-SEC-4). Each config
                // list is precedence-ordered (first entry signs new tokens; every listed
                // certificate still validates tokens it previously signed), loaded via
                // Security.SigningCertificateLoader from Identity:SigningCertificates /
                // Identity:EncryptionCertificates. Runbook: docs/security.md §4.1. Unconfigured
                // (every environment today) falls back to the ephemeral development certificate,
                // unchanged from before this feature existed.
                var signingCertificates = Security.SigningCertificateLoader.Load(
                    configuration, "Identity:SigningCertificates");
                if (signingCertificates.Count > 0)
                    foreach (var certificate in signingCertificates)
                        server.AddSigningCertificate(certificate);
                else
                    server.AddDevelopmentSigningCertificate();

                var encryptionCertificates = Security.SigningCertificateLoader.Load(
                    configuration, "Identity:EncryptionCertificates");
                if (encryptionCertificates.Count > 0)
                    foreach (var certificate in encryptionCertificates)
                        server.AddEncryptionCertificate(certificate);
                else
                    server.AddDevelopmentEncryptionCertificate();

                // Access-token hardening for external RPs (T-SEC-5): with a real encryption
                // certificate configured, access tokens are encrypted JWE — an external RP holds
                // an opaque blob it cannot read the claims of (national_id_hash, loa, etc.),
                // though Sheba.Api's own resource-server validation below decrypts them locally
                // since it shares the same certificate. Unconfigured (every environment today)
                // keeps the plain signed JWT so it can still be inspected in jwt.io during
                // development; the token is still RS256-signed and validated server-side either
                // way, and refresh-token rotation/reuse-detection (T-SEC-9) is unaffected by this
                // toggle.
                if (encryptionCertificates.Count == 0)
                    server.DisableAccessTokenEncryption();

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

        // Make OpenIddict validation the default authN/challenge scheme. Without this,
        // RequireAuthorization on any endpoint throws "No authenticationScheme was specified"
        // (500) instead of challenging with a 401 the moment an anonymous caller shows up.
        //
        // Also registers a SEPARATE, non-default cookie scheme (T-OIDC-1) used only by the
        // browser-redirect /connect/authorize flow — API callers are unaffected since it's never
        // the default scheme; only code that explicitly names AuthorizeEndpoints.SheebaSessionScheme
        // (the session-establish bridge below, and AuthorizeEndpoints itself) ever touches it.
        services.AddAuthentication(options =>
        {
            options.DefaultScheme          = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        })
        .AddCookie(Oidc.AuthorizeEndpoints.SheebaSessionScheme, options =>
        {
            options.Cookie.Name = "sheba_session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax; // must survive the RP's top-level redirect
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // dev runs over plain HTTP
            options.ExpireTimeSpan = TimeSpan.FromDays(30); // matches the refresh-token/SSO session window
            options.SlidingExpiration = true;
            // No LoginPath redirect — AuthorizeEndpoints handles "not authenticated" itself and
            // this scheme is never challenged directly.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
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
        // AllowAnonymous is correct here, not an oversight: these ARE the pre-authentication
        // flows — a caller has no token yet because getting one is the point of this group.
        var citizen = app.MapGroup("/api/identity").WithTags("Identity").AllowAnonymous()
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        citizen.MapPost("/register", async (
            RegisterCitizenCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(RateLimitPolicyNames.IdentityRegister) // T-SEC-2
        .WithName("RegisterCitizen")
        .WithSummary("Step 1: validate NID + phone, create pending account, send OTP.");

        citizen.MapPost("/verify-otp", async (
            VerifyOtpCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(RateLimitPolicyNames.IdentityOtp) // T-SEC-2
        .WithName("VerifyOtp")
        .WithSummary("Step 2: verify the OTP sent to the citizen's phone.");

        citizen.MapPost("/complete-registration", async (
            CompleteRegistrationCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithName("CompleteRegistration")
        .WithSummary("Step 3: set username, email, password; an email verification link is sent.");

        citizen.MapPost("/verify-email", async (
            VerifyEmailCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyEmail")
        .WithSummary("Step 4: verify the email link sent after completing registration.");

        citizen.MapPost("/login", async (
            LoginCitizenCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(RateLimitPolicyNames.IdentityLogin) // T-SEC-2
        .WithName("LoginCitizen")
        .WithSummary("Login step 1: validate credentials and dispatch a login OTP.");

        citizen.MapPost("/login/verify-otp", async (
            VerifyLoginOtpCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireRateLimiting(RateLimitPolicyNames.IdentityOtp) // T-SEC-2
        .WithName("VerifyLoginOtp")
        .WithSummary("Login step 2: verify the login OTP. Token issuance happens via /connect/token.");

        // LoA upgrade is a citizen action on their OWN account, so it needs a real principal —
        // separate group so it can require auth while the rest of `citizen` stays anonymous.
        var citizenAuthed = app.MapGroup("/api/identity").WithTags("Identity").RequireAuthorization("CitizenOnly")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        citizenAuthed.MapPost("/loa/upgrade", async (
            LoaUpgradeBody body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            // AccountId comes from the verified token, never the body — a citizen requesting an
            // upgrade for someone else's account is exactly the caller-supplied-identity bug
            // this endpoint used to have.
            var command = new RequestLoaUpgradeCommand(user.RequireSubjectId(), body.TargetLevel);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithName("RequestLoaUpgrade")
        .WithSummary("Request a Level-of-Assurance upgrade (LoA 2/3) for your own account — enters the admin review queue.");

        // ── Session bridge for the browser /connect/authorize flow (T-OIDC-1) ──
        // Any authenticated principal (bearer token) — not policy-restricted to citizens, since
        // nothing here is citizen-specific; it only re-issues whatever claims the caller already
        // proved. Separate group from citizenAuthed above because its RequireAuthorization()
        // (default scheme, no policy) differs from citizenAuthed's CitizenOnly policy.
        var sessionBridge = app.MapGroup("/api/identity/session").WithTags("Identity — Session")
            .RequireAuthorization()
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        sessionBridge.MapPost("/establish", async (HttpContext context, ClaimsPrincipal user) =>
        {
            // Re-issues the caller's already-validated bearer claims as a browser cookie —
            // bridges the existing API-based login (password + OTP, custom grant) to the
            // cookie-based session /connect/authorize needs for the browser-redirect PKCE flow.
            // No new authentication happens here; RequireAuthorization above already proved it.
            var identity = new ClaimsIdentity(user.Claims, Oidc.AuthorizeEndpoints.SheebaSessionScheme);
            await context.SignInAsync(Oidc.AuthorizeEndpoints.SheebaSessionScheme, new ClaimsPrincipal(identity));
            return Results.Ok(new { established = true });
        })
        .WithName("EstablishSession")
        .WithSummary("Bridges an existing bearer token into a browser session cookie for /connect/authorize SSO.");

        // ── Admin self-service MFA (T-SEC-1) ──────────────────────────────────
        // AnyAdmin, not a specific role — every admin manages their own second factor regardless
        // of sub-role. AdminId always comes from the caller's own token, never the body.
        var adminMfa = app.MapGroup("/api/admin/mfa").WithTags("Admin — MFA").RequireAuthorization("AnyAdmin")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        adminMfa.MapPost("/enroll", async (
            ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new EnrollAdminMfaCommand(user.RequireSubjectId()), ct);
            return result.ToHttpResult();
        })
        .WithName("EnrollAdminMfa")
        .WithSummary("Step 1: generate a TOTP secret for the calling admin (unconfirmed until /verify).");

        adminMfa.MapPost("/verify", async (
            ConfirmAdminMfaBody body, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new ConfirmAdminMfaCommand(user.RequireSubjectId(), body.TotpCode), ct);
            return result.ToHttpResult();
        })
        .WithName("ConfirmAdminMfa")
        .WithSummary("Step 2: confirm enrollment with a live code — enables MFA and issues recovery codes.");

        // ── Admin user provisioning (T-AUTH-1 prerequisite) ───────────────────
        // SuperAdminOnly — account provisioning is a privileged operation. A MinistryManager
        // requires ministryId; every other role must omit it (AdminUser.Create enforces this).
        var adminUsers = app.MapGroup("/api/admin/admin-users").WithTags("Admin — Users")
            .RequireAuthorization("SuperAdminOnly")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        adminUsers.MapPost("/", async (
            CreateAdminUserCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateAdminUser")
        .WithSummary("Provision a new admin account, optionally scoped to a ministry (MinistryManager role).");

        // ── Admin identity-request review queue ───────────────────────────────
        var admin = app.MapGroup("/api/admin/identity-requests")
            .WithTags("Admin — Identity Requests")
            .RequireAuthorization("IdentityReviewer")
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

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
            return result.ToHttpResult();
        })
        .WithName("GetIdentityRequests")
        .WithSummary("List identity requests (admin review queue).");

        admin.MapPost("/{requestId:guid}/approve", async (
            Guid requestId,
            ApproveIdentityRequestBody body,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Reviewer identity comes from the admin's own token (sub), never a body field —
            // otherwise any reviewer could attribute their decision to a different admin.
            var result = await mediator.Send(
                new ApproveIdentityRequestCommand(requestId, user.RequireSubjectId(), body.Notes), ct);
            return result.ToHttpResult();
        })
        .WithName("ApproveIdentityRequest")
        .WithSummary("Approve an identity request — activates the citizen account.");

        admin.MapPost("/{requestId:guid}/reject", async (
            Guid requestId,
            RejectIdentityRequestBody body,
            ClaimsPrincipal user,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new RejectIdentityRequestCommand(requestId, user.RequireSubjectId(), body.RejectionReason, body.Notes), ct);
            return result.ToHttpResult();
        })
        .WithName("RejectIdentityRequest")
        .WithSummary("Reject an identity request with a reason.");

        // ── Admin account lookup ──────────────────────────────────────────────
        app.MapGet("/api/admin/accounts/{id:guid}", async (
            Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAccountByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .WithTags("Admin — Accounts")
        .RequireAuthorization("IdentityReviewer")
        .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>() // JSend envelopes (T-API-1)
        .WithName("GetAccountById")
        .WithSummary("Get a citizen account by ID.");

        return app;
    }

    /// <summary>Request body for the LoA upgrade endpoint — AccountId is never client-supplied.</summary>
    public sealed record LoaUpgradeBody(int TargetLevel);

    /// <summary>Request body for the MFA confirm endpoint — AdminId is never client-supplied.</summary>
    public sealed record ConfirmAdminMfaBody(string TotpCode);

    /// <summary>Request body for the approve endpoint — the reviewer id comes from the token.</summary>
    public sealed record ApproveIdentityRequestBody(string? Notes = null);

    /// <summary>Request body for the reject endpoint — the reviewer id comes from the token.</summary>
    public sealed record RejectIdentityRequestBody(string RejectionReason, string? Notes = null);

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
            clientSecret: "sheba-admin-dev-secret",
            allowAdminPasswordGrant: true);

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
        string? clientSecret = null,
        bool allowAdminPasswordGrant = false)
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

        // Admin clients (e.g. sheba-admin) may use the custom admin password grant.
        if (allowAdminPasswordGrant)
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Prefixes.GrantType + Oidc.OidcEndpoints.ShebaAdminGrantType);

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
