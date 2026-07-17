using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using Sheba.Admin.Application.Interfaces;
using Sheba.Admin.Infrastructure.Jobs;
using Sheba.Admin.Infrastructure.Persistence;
using Sheba.Admin.Infrastructure.Reports;
using Sheba.Admin.Infrastructure.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;

namespace Sheba.Admin.Infrastructure;

/// <summary>
/// Registers every service belonging to the Admin module.
///
/// Called once from Sheba.Api/Program.cs:
///     builder.Services.AddAdminModule(builder.Configuration);
///
/// Architecture constraints:
///   • No other module may call AddAdminModule.
///   • No other module may inject AdminDbContext.
///   • Cross-module integration goes through IDomainEvent / IMediator only.
/// </summary>
public static class AdminModule
{
    public static IServiceCollection AddAdminModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. QuestPDF — Community licence for graduation project ─────────────
        QuestPDF.Settings.License = LicenseType.Community;

        // ── 2. DbContext — bound to the "admin_data" schema ────────────────────
        services.AddDbContext<AdminDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "admin_data");
                    npgsql.MigrationsAssembly(typeof(AdminModule).Assembly.FullName);
                })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new OutboxSaveChangesInterceptor()));

        // Expose as base DbContext so the startup migration runner discovers this context (T-DB-1).
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AdminDbContext>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork<AdminDbContext>>();
        services.AddScoped<IInboxGuard, EfInboxGuard<AdminDbContext>>();

        // ── 3. Repository ──────────────────────────────────────────────────────
        services.AddScoped<IAdminAnalyticsRepository, AdminAnalyticsRepository>();

        // ── 4. Report generators ───────────────────────────────────────────────
        services.AddScoped<PdfReportGenerator>();
        services.AddScoped<ExcelReportGenerator>();
        services.AddScoped<CsvReportGenerator>();

        // ── 5. Scheduled reports job (injected by Hangfire) ─────────────────────
        services.AddScoped<ScheduledReportsJob>();

        // ── 6. Hangfire — PostgreSQL storage + background server ────────────────
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(opts =>
                opts.UseNpgsqlConnection(
                    configuration.GetConnectionString("Default"))));
        services.AddHangfireServer();

        return services;
    }

    /// <summary>
    /// Maps all admin endpoints and configures Hangfire dashboard + recurring jobs.
    /// Call after endpoint mapping in Program.cs.
    /// </summary>
    public static WebApplication UseAdminModule(this WebApplication app)
    {
        // ── Hangfire dashboard (no auth in graduation project) ─────────────────
        app.UseHangfireDashboard("/hangfire");

        // ── Register recurring jobs ────────────────────────────────────────────
        RecurringJob.AddOrUpdate<ScheduledReportsJob>(
            "weekly-identity-report",
            job => job.GenerateAndEmailWeeklyReportAsync(CancellationToken.None),
            Cron.Weekly(DayOfWeek.Monday, 7),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        return app;
    }

    /// <summary>
    /// Maps all admin API endpoints.
    /// </summary>
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        AdminEndpoints.Map(app);
        return app;
    }
}
