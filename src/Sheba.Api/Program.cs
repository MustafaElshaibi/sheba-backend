using FluentValidation;
using Hangfire;
using MediatR;
using Serilog;
using Sheba.Admin.Infrastructure;
using Sheba.Api.Behaviors;
using Sheba.Api.Extensions;
using Sheba.Api.Middleware;
using Sheba.Api.Outbox;
using Sheba.Api.RateLimiting;
using Sheba.Audit.Infrastructure;
using Sheba.Audit.Infrastructure.Behaviors;
using Sheba.Citizen.Infrastructure;
using Sheba.Document.Infrastructure;
using Sheba.Identity.Infrastructure;
using Sheba.Identity.Infrastructure.Jobs;
using Sheba.Identity.Infrastructure.Oidc;
using Sheba.Ministry.Infrastructure;
using Sheba.Ministry.Infrastructure.Jobs;
using Sheba.Notification.Infrastructure;
using Sheba.Payment.Infrastructure;
using Sheba.ServiceRequest.Infrastructure;
using Sheba.ServiceRequest.Infrastructure.Jobs;
using Sheba.Wallet.Infrastructure;
using StackExchange.Redis;

// ── Bootstrap logger (captures startup errors before full Serilog is configured) ──────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog — structured logging → Console + Seq ──────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

    // ── Redis — singleton connection multiplexer ───────────────────────────────────────────────
    var redisMultiplexer = ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379");
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379");

    // ── Rate limiting (T-SEC-2) — Redis-backed sliding windows on auth-sensitive endpoints ──────
    builder.Services.AddShebaRateLimiting(redisMultiplexer);

    // ── CORS (T-GW-1) — config-driven allow-list, no wildcard ──────────────────────────────────
    builder.Services.AddShebaCors(builder.Configuration);

    // ── Module Registration ────────────────────────────────────────────────────────────────────
    // Each module registers its own DbContext, repositories, adapters, and validators.
    // Order matters for OpenIddict (Identity must be registered before AddAuthentication).
    builder.Services
        .AddIdentityModule(builder.Configuration)
        .AddCitizenModule(builder.Configuration)
        .AddMinistryModule(builder.Configuration)
        .AddServiceRequestModule(builder.Configuration)
        .AddDocumentModule(builder.Configuration)
        .AddWalletModule(builder.Configuration)
        .AddPaymentModule(builder.Configuration)
        .AddNotificationModule(builder.Configuration)
        .AddAuditModule(builder.Configuration)
        .AddAdminModule(builder.Configuration);

    // ── MediatR — discovers all handlers across all module Application + Infrastructure assemblies ──
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(
            // Infrastructure assemblies (module registration + some handlers)
            typeof(IdentityModule).Assembly,
            typeof(CitizenModule).Assembly,
            typeof(MinistryModule).Assembly,
            typeof(ServiceRequestModule).Assembly,
            typeof(DocumentModule).Assembly,
            typeof(WalletModule).Assembly,
            typeof(PaymentModule).Assembly,
            typeof(NotificationModule).Assembly,
            typeof(AuditModule).Assembly,
            typeof(AdminModule).Assembly,
            // Application assemblies (commands, queries, event handlers)
            typeof(Sheba.Citizen.Application.Commands.UpdateProfile.UpdateProfileCommand).Assembly,
            typeof(Sheba.Ministry.Application.Commands.CreateMinistry.CreateMinistryCommand).Assembly,
            typeof(Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest.SubmitServiceRequestCommand).Assembly,
            typeof(Sheba.Document.Application.Commands.UploadDocument.UploadDocumentCommand).Assembly,
            typeof(Sheba.Wallet.Application.Commands.IssueIdentityCredential.IssueIdentityCredentialCommand).Assembly,
            typeof(Sheba.Admin.Application.Analytics.GetKpiSummary.GetKpiSummaryQuery).Assembly,
            typeof(Sheba.Audit.Application.Queries.GetAuditLog.GetAuditLogQuery).Assembly));

    // ── Pipeline Behaviors (registered in execution order) ────────────────────────────────────
    // 1. LoggingBehavior     — always runs; wraps the full pipeline with timing
    // 2. ValidationBehavior  — runs FluentValidation before the handler
    // 3. TransactionBehavior — wraps commands marked ITransactionalCommand in a UoW transaction
    // 4. AuditLoggingBehavior — writes a redacted audit_events row for every command (T-AUD-4)
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));

    // ── FluentValidation — auto-discovers all validators in all module assemblies ──────────────
    // Both Infrastructure AND Application assemblies: command validators live in Application
    // (e.g. RegisterCitizenValidator) — scanning only Infrastructure silently registers nothing
    // and ValidationBehavior becomes a no-op (the empty-body register 500 bug).
    builder.Services.AddValidatorsFromAssemblies(
    [
        typeof(IdentityModule).Assembly,
        typeof(CitizenModule).Assembly,
        typeof(MinistryModule).Assembly,
        typeof(ServiceRequestModule).Assembly,
        typeof(DocumentModule).Assembly,
        typeof(WalletModule).Assembly,
        typeof(PaymentModule).Assembly,
        typeof(NotificationModule).Assembly,
        typeof(AuditModule).Assembly,
        typeof(AdminModule).Assembly,
        // Application assemblies (where the validators actually are)
        typeof(Sheba.Identity.Application.Commands.RegisterCitizen.RegisterCitizenValidator).Assembly,
        typeof(Sheba.Citizen.Application.Commands.UpdateProfile.UpdateProfileValidator).Assembly,
        typeof(Sheba.Ministry.Application.Commands.CreateMinistry.CreateMinistryCommand).Assembly,
        typeof(Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest.SubmitServiceRequestCommand).Assembly,
        typeof(Sheba.Document.Application.Commands.UploadDocument.UploadDocumentCommand).Assembly,
        typeof(Sheba.Wallet.Application.Commands.IssueIdentityCredential.IssueIdentityCredentialCommand).Assembly,
        typeof(Sheba.Admin.Application.Analytics.GetKpiSummary.GetKpiSummaryQuery).Assembly
    ]);

    // ── Swagger / OpenAPI ────────────────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Sheba e-Government API",
            Version = "v1",
            Description = """
                **Sheba** — Yemen's national e-Government platform (Graduation Project).

                A modular-monolith IAM & service portal built on ASP.NET Core 9 with:
                OpenIddict (OIDC/OAuth 2.1), PostgreSQL, Redis, MinIO, MediatR, Serilog.

                ### Modules
                - **Identity** — registration, OTP login, admin approval, OIDC provider
                - **Ministry** — ministry registry, auth-config vault, endpoints, webhooks
                - **Service Catalog** — government service definitions with JSON Schema forms
                - **Service Requests** — citizen request lifecycle + workflow engine
                - **Payment** — mock fee collection
                - **Citizen / Document / Wallet / Notification / Audit / Admin**
                """,
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "Sheba Platform Team",
                Email = "support@sheba.gov"
            }
        });

        // OAuth2 / Bearer security definition for protected endpoints
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter the JWT access token issued by /connect/token."
        });
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Resolve schema-id conflicts (records with the same short name across modules)
        options.CustomSchemaIds(t => t.FullName?.Replace("+", ".") ?? t.Name);

        // Document the JSend envelope on every non-exempt response (T-API-1)
        options.OperationFilter<Sheba.Api.Swagger.JSendOperationFilter>();
    });

    // ── Authorization ─────────────────────────────────────────────────────────────────────────
    // Authentication is configured inside IdentityModule.cs via OpenIddict's UseAspNetCore().
    //
    // Policy names are matched against the "role" claim, which OidcEndpoints sets to "Citizen"
    // for citizen tokens and to the AdminRole enum name (SuperAdmin, IdentityReviewer,
    // MinistryManager, Auditor, Support) for admin tokens — see §10.1. Policies compose the
    // "which admin sub-roles may act" question; per-resource ownership (a citizen touching only
    // their own data, a Ministry Admin touching only their own ministry) is enforced in handlers,
    // not here, per the same design note in docs/sheba.md §10 — a claims-based policy can't
    // express "this ministry_id row belongs to this admin" on its own.
    const string RoleClaim = "role";
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("CitizenOnly", p => p.RequireClaim(RoleClaim, "Citizen"))
        .AddPolicy("SuperAdminOnly", p => p.RequireClaim(RoleClaim, "SuperAdmin"))
        .AddPolicy("IdentityReviewer", p => p.RequireClaim(RoleClaim, "SuperAdmin", "IdentityReviewer"))
        .AddPolicy("MinistryManager", p => p.RequireClaim(RoleClaim, "SuperAdmin", "MinistryManager"))
        .AddPolicy("Auditor", p => p.RequireClaim(RoleClaim, "SuperAdmin", "Auditor"))
        .AddPolicy("AnyAdmin", p => p.RequireClaim(RoleClaim,
            "SuperAdmin", "IdentityReviewer", "MinistryManager", "Auditor", "Support"));

    // ── HTTP Resilience (for ministry calls) ──────────────────────────────────────────────────
    builder.Services.AddHttpClient("MinistryClient")
        .AddStandardResilienceHandler();

    // ── Outbox dispatcher (T-EVT-1) — Hangfire is registered by AddAdminModule above ──────────
    builder.Services.AddScoped<OutboxDispatcherJob>();
    builder.Services.AddScoped<AccountPurgeJob>();
    builder.Services.AddScoped<SlaSweepJob>();

    // ── Build ─────────────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────────────────────────────
    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();  // T-GW-1 — before request logging so it's in scope
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms [{CorrelationId}]";
    });

    // ── API Documentation — Swagger UI (always enabled so the API is explorable) ─────────────────
    app.UseSwagger();                                   // /swagger/v1/swagger.json
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Sheba e-Government API v1");
        options.RoutePrefix = "swagger";                // UI at /swagger
        options.DocumentTitle = "Sheba e-Government API — Swagger";
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });

    app.UseRateLimiter();  // T-SEC-2 — before auth, so a flooded caller never reaches OpenIddict
    app.UseCors(CorsExtensions.PolicyName);  // T-GW-1

    app.UseAuthentication();
    app.UseAuthorization();

    // ── Endpoint Routing (one route group per module) ─────────────────────────────────────────
    // Mapped BEFORE migrations/seeding so the API (and Swagger) are always available even if the
    // database is temporarily unreachable during startup.
    app.MapIdentityEndpoints();        // /api/identity/...  +  /connect/...  (OIDC)
    app.MapOidcEndpoints();            // /connect/token, /connect/userinfo, /connect/logout
    app.MapAuthorizeEndpoints();       // /connect/authorize, /connect/consent[/decide]  (T-OIDC-1)
    app.MapRelyingPartyEndpoints();    // /api/admin/relying-parties/...
    app.MapCitizenEndpoints();         // /api/citizen/...
    app.MapMinistryEndpoints();        // /api/ministry/...
    app.MapServiceRequestEndpoints();  // /api/services/...
    app.MapDocumentEndpoints();        // /api/documents/...
    app.MapWalletEndpoints();          // /api/wallet/...
    app.MapPaymentEndpoints();         // /api/payments/...
    app.MapNotificationEndpoints();    // /api/notifications/...
    app.MapAuditEndpoints();           // /api/audit/...
    app.MapAdminEndpoints();           // /api/admin/...
    app.UseAdminModule();              // Hangfire dashboard + recurring jobs

    // Outbox dispatcher (T-EVT-1): one recurring job, deduplicated by id across restarts, that
    // internally polls every 5s for the rest of each minute — see OutboxDispatcherJob for why.
    RecurringJob.AddOrUpdate<OutboxDispatcherJob>(
        "outbox-dispatcher",
        job => job.DispatchAsync(CancellationToken.None),
        Cron.Minutely());

    // Account purge (T-ID-1): abandoned PendingVerification registrations + spent OTP records.
    RecurringJob.AddOrUpdate<AccountPurgeJob>(
        "account-purge",
        job => job.PurgeAsync(CancellationToken.None),
        Cron.Hourly());

    // SLA sweep (T-SRV-3): expire overdue AwaitingMinistry requests.
    RecurringJob.AddOrUpdate<SlaSweepJob>(
        "sla-sweep",
        job => job.SweepAsync(CancellationToken.None),
        Cron.Hourly());

    // Ministry health sweep (Phase 2 roadmap): exercise every active ministry auth config's
    // TestConnectionAsync so the admin dashboard's health status doesn't go stale between
    // manual "test connection" clicks.
    RecurringJob.AddOrUpdate<MinistryHealthSweepJob>(
        "ministry-health-sweep",
        job => job.SweepAsync(CancellationToken.None),
        "*/15 * * * *");

    // ── Run all module EF Core migrations + seed data on startup ────────────────────────────────
    // Wrapped so a transient DB outage does not prevent the API from serving Swagger/endpoints.
    try
    {
        await app.MigrateAllModulesAsync();
        await IdentityModule.SeedIdentityAsync(app);
        await MinistryModule.SeedMinistriesAsync(app); // must run before the service catalog (T-MIN-1)
        await ServiceRequestModule.SeedServiceCatalogAsync(app);
    }
    catch (Exception dbEx)
    {
        Log.Error(dbEx,
            "Database migration/seeding failed at startup. The API will still run so Swagger and " +
            "endpoints are reachable, but data operations will fail until the database is available.");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Sheba API failed to start.");
}
finally
{
    Log.CloseAndFlush();
}
