using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Payment.Infrastructure.Persistence;
using Sheba.Payment.Infrastructure.Persistence.Repositories;

namespace Sheba.Payment.Infrastructure;

public static class PaymentModule
{
    public static IServiceCollection AddPaymentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations", "payment");
                    npgsql.MigrationsAssembly(typeof(PaymentModule).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<PaymentDbContext>());
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }

    public static WebApplication MapPaymentEndpoints(this WebApplication app)
    {
        // Payment endpoints are mapped via ServiceRequestModule (MarkPaymentComplete)
        // This module provides the domain + infrastructure only
        return app;
    }
}
