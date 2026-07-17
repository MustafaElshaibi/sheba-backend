namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module port over the Payment module's order lifecycle. Defined in Shared.Kernel so
/// ServiceRequest (which drives payment steps in its workflow) never references
/// Sheba.Payment.Domain/Infrastructure directly (rule 1/3, T-ARC-1). Implemented in
/// Payment.Infrastructure, which owns the `payment.payment_orders` table.
///
/// Deliberately narrower than Payment's own `IPaymentRepository`: no raw `PaymentOrder` entity
/// crosses the boundary, only the read-only <see cref="PaymentOrderInfo"/> DTO.
/// </summary>
public interface IPaymentOrderPort
{
    Task<PaymentOrderInfo?> GetByIdAsync(Guid paymentOrderId, CancellationToken ct = default);

    Task<PaymentOrderInfo?> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken ct = default);

    Task<PaymentOrderInfo> CreateOrderAsync(
        Guid serviceRequestId,
        Guid citizenId,
        decimal totalAmount,
        string currency,
        string? description,
        CancellationToken ct = default);

    Task<PaymentOrderInfo> MarkPaidAsync(
        Guid paymentOrderId, string? gatewayReference, CancellationToken ct = default);
}

/// <summary>Mirrors <c>Sheba.Payment.Domain.Enums.PaymentStatus</c> — kept in sync manually since
/// the concrete enum must not cross the module boundary.</summary>
public enum PaymentOrderStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}

/// <summary>Read-only DTO for cross-module payment order queries — no gateway secrets, no
/// mutable entity reference.</summary>
public sealed record PaymentOrderInfo(
    Guid Id,
    Guid ServiceRequestId,
    Guid CitizenId,
    string OrderNumber,
    decimal TotalAmount,
    string Currency,
    PaymentOrderStatus Status,
    string? PaymentUrl,
    string? GatewayReference);
