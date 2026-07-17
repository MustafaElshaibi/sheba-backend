using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Notification.Infrastructure.Adapters;
using Sheba.Notification.Infrastructure.Persistence;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;

namespace Sheba.Notification.Infrastructure;

/// <summary>
/// Registers every service belonging to the Notification module.
///
/// Called once from Sheba.Api/Program.cs:
///     builder.Services.AddNotificationModule(builder.Configuration);
///
/// Architecture constraints:
///   • No other module may call AddNotificationModule.
///   • No other module may inject NotificationDbContext.
///   • Cross-module notifications go through IEmailService / ISmsService (Shared.Kernel).
///
/// Switching adapters via configuration:
///   Notification:Email:ActiveProvider  → "Mailhog" (dev default) | "Smtp" (prod)
///   Notification:Sms:ActiveProvider   → "Console" (dev default)  | "Twilio" (prod)
/// </summary>
public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. DbContext — bound to the "notification" schema ─────────────────
        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "notification");
                    npgsql.MigrationsAssembly(typeof(NotificationModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
            options.AddInterceptors(new OutboxSaveChangesInterceptor());
        });

        // Expose as base DbContext so the startup migration runner discovers this context (T-DB-1).
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddScoped<IUnitOfWork, EfUnitOfWork<NotificationDbContext>>();
        services.AddScoped<IInboxGuard, EfInboxGuard<NotificationDbContext>>();

        // ── 2. Email adapter ──────────────────────────────────────────────────
        // IEmailService is defined in Shared.Kernel so any module can inject it.
        var emailProvider = configuration["Notification:Email:ActiveProvider"] ?? "Mailhog";

        if (emailProvider.Equals("Mailhog", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailService, MailhogEmailProvider>();
        else
            services.AddScoped<IEmailService, SmtpEmailProvider>();

        // ── 3. SMS adapter ────────────────────────────────────────────────────
        // ISmsService is defined in Shared.Kernel so any module can inject it.
        var smsProvider = configuration["Notification:Sms:ActiveProvider"] ?? "Console";

        if (smsProvider.Equals("Console", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<ISmsService, ConsoleOtpProvider>();
        else
            services.AddScoped<ISmsService, TwilioSmsProvider>();

        return services;
    }

    /// <summary>
    /// Maps Notification module API endpoints.
    /// (None for graduation — Notification is backend-only; events trigger sends.)
    /// </summary>
    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        // Future: GET /api/notifications/{accountId} — citizen notification history
        return app;
    }
}
