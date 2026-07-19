using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheba.Payment.Application.Commands.ConfirmPayment;
using Sheba.Payment.Application.Commands.RefundPayment;
using Sheba.Payment.Application.Queries.GetPaymentOrder;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Payment.Infrastructure.Adapters;
using Sheba.Payment.Infrastructure.Gateways;
using Sheba.Payment.Infrastructure.Persistence;
using Sheba.Payment.Infrastructure.Persistence.Repositories;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Outbox;
using Sheba.Shared.Kernel.Persistence;
using Sheba.Shared.Kernel.Security;

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
            options.AddInterceptors(new OutboxSaveChangesInterceptor());
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<PaymentDbContext>());
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<PaymentDbContext>>();
        services.AddScoped<IInboxGuard, EfInboxGuard<PaymentDbContext>>();

        // ── Payment gateway (T-PAY-1) ────────────────────────────────────────
        // Config-selected like INationalIdProvider/IOtpProvider — mock is the only implementation
        // that ships this phase (§5.7, A6); a real PSP adapter is a new class + config entry.
        var gateway = configuration["Payment:ActiveGateway"] ?? "Mock";
        switch (gateway)
        {
            case "Mock":
            default:
                services.AddScoped<IPaymentGateway, MockPaymentGateway>();
                break;
        }

        // Cross-module query/command port — ServiceRequest drives payment steps via this,
        // never via Sheba.Payment.Domain/Infrastructure directly (T-ARC-1).
        services.AddScoped<IPaymentOrderPort, PaymentOrderPortAdapter>();

        return services;
    }

    public static WebApplication MapPaymentEndpoints(this WebApplication app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payment")
            .RequireAuthorization() // any authenticated principal; ownership enforced in handlers
            .AddEndpointFilter<Sheba.Shared.Kernel.Responses.JSendWrappingFilter>(); // JSend envelopes (T-API-1)

        payments.MapGet("/{paymentOrderId:guid}", async (
            Guid paymentOrderId, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var isAdmin = user.GetRole() != "Citizen";
            var result = await mediator.Send(
                new GetPaymentOrderQuery(paymentOrderId, user.RequireSubjectId(), isAdmin), ct);
            return Results.Ok(result);
        })
        .WithName("GetPaymentOrder")
        .WithSummary("Get your own payment order (or any order, for admins).");

        payments.MapPost("/{paymentOrderId:guid}/confirm", async (
            Guid paymentOrderId, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
        {
            var isAdmin = user.GetRole() != "Citizen";
            var result = await mediator.Send(
                new ConfirmPaymentCommand(paymentOrderId, user.RequireSubjectId(), isAdmin), ct);
            return Results.Ok(result);
        })
        .WithName("ConfirmPayment")
        .WithSummary("Confirm your own payment order via the (mock) gateway.");

        payments.MapPost("/{paymentOrderId:guid}/refund", async (
            Guid paymentOrderId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RefundPaymentCommand(paymentOrderId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("SuperAdminOnly") // BR-PA-3: refunds are System Admin only
        .WithName("RefundPayment")
        .WithSummary("Refund a completed payment order via the (mock) gateway (System Admin only).");

        return app;
    }
}
