namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when a PaymentOrder is confirmed paid (T-PAY-1). Declared in Shared.Kernel because two
/// other modules subscribe: ServiceRequest (resume the paused workflow step) and Admin (revenue
/// snapshot). Replaces the old direct <c>MarkPaymentCompleteCommand</c> coupling where
/// ServiceRequest.Application called into the Payment port and advanced its own workflow inline.
///
/// Handlers:
///   - ServiceRequest module: completes the active Payment step execution and advances the
///     workflow to the next step.
///   - Admin module: increments today's DailyRevenueSnapshot.
/// </summary>
public sealed record PaymentCompletedEvent(
    Guid PaymentOrderId,
    Guid ServiceRequestId,
    Guid CitizenId,
    decimal Amount,
    string Currency,
    string? GatewayReference,
    DateTime PaidAt
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
