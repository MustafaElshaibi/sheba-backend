using Microsoft.Extensions.Logging;
using Sheba.Payment.Domain.Interfaces;

namespace Sheba.Payment.Infrastructure.Gateways;

/// <summary>Always-succeeds mock <see cref="IPaymentGateway"/> — no licensed PSP integration
/// assumed for this phase (§5.7, A6). Selected by default via <c>Payment:ActiveGateway</c>.</summary>
public sealed class MockPaymentGateway(ILogger<MockPaymentGateway> logger) : IPaymentGateway
{
    public Task<GatewayChargeResult> ChargeAsync(
        Guid paymentOrderId, decimal amount, string currency, CancellationToken ct = default)
    {
        var reference = $"MOCK-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        logger.LogInformation(
            "[MockPaymentGateway] Charged {Amount} {Currency} for order {OrderId} -> {Reference}",
            amount, currency, paymentOrderId, reference);

        return Task.FromResult(new GatewayChargeResult(true, reference, "{\"mock\":true,\"result\":\"approved\"}"));
    }

    public Task<GatewayRefundResult> RefundAsync(
        Guid paymentOrderId, string originalGatewayReference, decimal amount, string currency,
        CancellationToken ct = default)
    {
        var reference = $"MOCK-REFUND-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        logger.LogInformation(
            "[MockPaymentGateway] Refunded {Amount} {Currency} for order {OrderId} (orig {OriginalRef}) -> {Reference}",
            amount, currency, paymentOrderId, originalGatewayReference, reference);

        return Task.FromResult(new GatewayRefundResult(true, reference, "{\"mock\":true,\"result\":\"refunded\"}"));
    }
}
