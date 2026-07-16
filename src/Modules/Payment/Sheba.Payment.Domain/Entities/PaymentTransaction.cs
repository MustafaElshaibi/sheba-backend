using Sheba.Shared.Kernel.Entities;

namespace Sheba.Payment.Domain.Entities;

/// <summary>Placeholder for future payment gateway transaction logging.</summary>
public sealed class PaymentTransaction : BaseEntity
{
    public Guid PaymentOrderId { get; private set; }
    public string TransactionType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string? GatewayResponse { get; private set; }
    private PaymentTransaction() { }
}
