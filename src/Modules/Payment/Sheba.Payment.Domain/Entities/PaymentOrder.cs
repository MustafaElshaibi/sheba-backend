using Sheba.Payment.Domain.Enums;
using Sheba.Shared.Kernel.Entities;

namespace Sheba.Payment.Domain.Entities;

/// <summary>
/// A payment order created when a service request reaches a Payment workflow step.
/// For graduation: mock payment — no real gateway integration.
/// </summary>
public sealed class PaymentOrder : BaseEntity
{
    public Guid ServiceRequestId { get; private set; }
    public Guid CitizenId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "YER";
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string? Description { get; private set; }
    public string? PaymentUrl { get; private set; }   // mock: /api/payments/{id}/pay
    public DateTime? PaidAt { get; private set; }
    public string? GatewayReference { get; private set; }

    private PaymentOrder() { }

    public static PaymentOrder Create(
        Guid serviceRequestId, Guid citizenId,
        decimal totalAmount, string currency = "YER",
        string? description = null)
    {
        var order = new PaymentOrder
        {
            ServiceRequestId = serviceRequestId,
            CitizenId = citizenId,
            TotalAmount = totalAmount,
            Currency = currency.ToUpperInvariant(),
            Description = description,
            OrderNumber = $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
        };
        order.PaymentUrl = $"/api/payments/{order.Id}/pay";
        return order;
    }

    public void MarkPaid(string? gatewayReference = null)
    {
        Status = PaymentStatus.Completed;
        PaidAt = DateTime.UtcNow;
        GatewayReference = gatewayReference ?? $"MOCK-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        Touch();
    }

    public void MarkFailed() { Status = PaymentStatus.Failed; Touch(); }
    public void Cancel() { Status = PaymentStatus.Cancelled; Touch(); }
}
