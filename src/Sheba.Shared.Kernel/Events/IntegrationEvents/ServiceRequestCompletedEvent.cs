namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when a service request reaches the Completed lifecycle status. Declared in
/// Shared.Kernel (not ServiceRequest.Domain) because Admin subscribes to it — see T-ARC-1.
///
/// Handlers:
///   - Admin module: increments today's DailyServiceRequestSnapshot completed count and updates
///     the running average processing time
/// </summary>
public sealed record ServiceRequestCompletedEvent(
    Guid RequestId, Guid ServiceId, Guid CitizenId,
    DateTime SubmittedAt, DateTime CompletedAt
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
