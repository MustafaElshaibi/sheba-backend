using Sheba.Payment.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Domain.Entities;

/// <summary>
/// A payment order created when a service request reaches a Payment workflow step.
/// For graduation: mock payment gateway behind <see cref="Interfaces.IPaymentGateway"/> — no real
/// PSP integration (T-PAY-1).
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
    public DateTime? RefundedAt { get; private set; }
    public string? RefundReference { get; private set; }

    private PaymentOrder() { }

    public static PaymentOrder Create(
        Guid serviceRequestId, Guid citizenId,
        decimal totalAmount, string currency = "YER",
        string? description = null)
    {
        if (totalAmount <= 0)
            throw new DomainException("Payment order total amount must be positive.");

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

    /// <summary>Confirms the order paid and raises <see cref="PaymentCompletedEvent"/> so
    /// ServiceRequest (resume workflow) and Admin (revenue snapshot) react out-of-band via the
    /// outbox (T-PAY-1) — this aggregate never calls into another module directly.</summary>
    public void MarkPaid(string? gatewayReference = null)
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
            throw new DomainException($"Cannot mark a payment order paid from status '{Status}'.");

        Status = PaymentStatus.Completed;
        PaidAt = DateTime.UtcNow;
        GatewayReference = gatewayReference ?? $"MOCK-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        Touch();

        RaiseDomainEvent(new PaymentCompletedEvent(
            Id, ServiceRequestId, CitizenId, TotalAmount, Currency, GatewayReference, PaidAt.Value));
    }

    public void MarkFailed()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot mark a payment order failed from status '{Status}'.");

        Status = PaymentStatus.Failed;
        Touch();
    }

    public void Cancel()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot cancel a payment order from status '{Status}'.");

        Status = PaymentStatus.Cancelled;
        Touch();
    }

    /// <summary>Refunds a completed order. No cross-module event today — nothing downstream
    /// reacts to refunds yet (workflow has already advanced past the payment step).</summary>
    public void Refund(string? refundReference = null)
    {
        if (Status != PaymentStatus.Completed)
            throw new DomainException($"Cannot refund a payment order from status '{Status}'.");

        Status = PaymentStatus.Refunded;
        RefundedAt = DateTime.UtcNow;
        RefundReference = refundReference ?? $"MOCK-REFUND-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
        Touch();
    }
}
