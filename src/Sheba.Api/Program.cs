using FluentValidation;
using MediatR;
using Serilog;
using Sheba.Admin.Infrastructure;
using Sheba.Api.Behaviors;
using Sheba.Api.Extensions;
using Sheba.Api.Middleware;
using Sheba.Audit.Infrastructure;
using Sheba.Citizen.Infrastructure;
using Sheba.Document.Infrastructure;
using Sheba.Identity.Infrastructure;
using Sheba.Identity.Infrastructure.Oidc;
using Sheba.Ministry.Infrastructure;
using Sheba.Notification.Infrastructure;
using Sheba.Payment.Infrastructure;
using Sheba.ServiceRequest.Infrastructure;
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
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(
            builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));

    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379");

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
            typeof(Sheba.Ministry.Application.Commands.CreateMinistry.CreateMinistryCommand).Assembly,
            typeof(Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest.SubmitServiceRequestCommand).Assembly,
            typeof(Sheba.Document.Application.Commands.UploadDocument.UploadDocumentCommand).Assembly,
            typeof(Sheba.Wallet.Application.Commands.IssueIdentityCredential.IssueIdentityCredentialCommand).Assembly,
            typeof(Sheba.Admin.Application.Analytics.GetKpiSummary.GetKpiSummaryQuery).Assembly));

    // ── Pipeline Behaviors (registered in execution order) ────────────────────────────────────
    // 1. LoggingBehavior   — always runs; wraps the full pipeline with timing
    // 2. ValidationBehavior — runs FluentValidation before the handler
    // 3. TransactionBehavior — wraps commands marked ITransactionalCommand in a UoW transaction
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

    // ── FluentValidation — auto-discovers all validators in all module assemblies ──────────────
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
        typeof(AdminModule).Assembly
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
    });

    // ── Authorization ─────────────────────────────────────────────────────────────────────────
    // Authentication is configured inside IdentityModule.cs via OpenIddict's UseAspNetCore()
    builder.Services.AddAuthorization();

    // ── HTTP Resilience (for ministry calls) ──────────────────────────────────────────────────
    builder.Services.AddHttpClient("MinistryClient")
        .AddStandardResilienceHandler();

    // ── Build ─────────────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware Pipeline ───────────────────────────────────────────────────────────────────
    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
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

    app.UseAuthentication();
    app.UseAuthorization();

    // ── Endpoint Routing (one route group per module) ─────────────────────────────────────────
    // Mapped BEFORE migrations/seeding so the API (and Swagger) are always available even if the
    // database is temporarily unreachable during startup.
    app.MapIdentityEndpoints();        // /api/identity/...  +  /connect/...  (OIDC)
    app.MapOidcEndpoints();            // /connect/token, /connect/userinfo, /connect/logout
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

    // ── Run all module EF Core migrations + seed data on startup ────────────────────────────────
    // Wrapped so a transient DB outage does not prevent the API from serving Swagger/endpoints.
    try
    {
        await app.MigrateAllModulesAsync();
        await IdentityModule.SeedIdentityAsync(app);
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
