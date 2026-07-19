namespace Sheba.Payment.Domain.Interfaces;

/// <summary>
/// Pluggable payment-gateway seam (T-PAY-1). Selected by <c>Payment:ActiveGateway</c> config,
/// mirroring the <c>INationalIdProvider</c>/<c>IOtpProvider</c> pattern — the mock implementation
/// is the only one that ships today (§5.7, A6: no licensed PSP integration assumed for this
/// phase); a real adapter is a config entry + new class, never an <c>#if</c>/env check.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayChargeResult> ChargeAsync(
        Guid paymentOrderId, decimal amount, string currency, CancellationToken ct = default);

    Task<GatewayRefundResult> RefundAsync(
        Guid paymentOrderId, string originalGatewayReference, decimal amount, string currency,
        CancellationToken ct = default);
}

public sealed record GatewayChargeResult(bool Success, string? GatewayReference, string? RawResponse);

public sealed record GatewayRefundResult(bool Success, string? RefundReference, string? RawResponse);
