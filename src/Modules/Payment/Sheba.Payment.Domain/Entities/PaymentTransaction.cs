using Sheba.Shared.Kernel.Entities;

namespace Sheba.Payment.Domain.Entities;

/// <summary>Immutable log of a gateway call (charge/refund) against a <see cref="PaymentOrder"/> —
/// the audit trail behind the mock <see cref="Interfaces.IPaymentGateway"/> seam (T-PAY-1).</summary>
public sealed class PaymentTransaction : BaseEntity
{
    public Guid PaymentOrderId { get; private set; }
    public string TransactionType { get; private set; } = string.Empty; // "Charge" | "Refund"
    public decimal Amount { get; private set; }
    public bool Succeeded { get; private set; }
    public string? GatewayResponse { get; private set; }

    private PaymentTransaction() { }

    public static PaymentTransaction Create(
        Guid paymentOrderId, string transactionType, decimal amount, bool succeeded, string? gatewayResponse) =>
        new()
        {
            PaymentOrderId = paymentOrderId,
            TransactionType = transactionType,
            Amount = amount,
            Succeeded = succeeded,
            GatewayResponse = gatewayResponse
        };
}
