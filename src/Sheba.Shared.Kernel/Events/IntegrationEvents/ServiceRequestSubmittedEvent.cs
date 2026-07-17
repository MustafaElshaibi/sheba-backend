namespace Sheba.Shared.Kernel.Events.IntegrationEvents;

/// <summary>
/// Raised when a citizen submits a service request. Declared in Shared.Kernel (not
/// ServiceRequest.Domain) because Admin subscribes to it — see T-ARC-1.
///
/// Handlers:
///   - Admin module: increments today's DailyServiceRequestSnapshot submitted count
/// </summary>
public sealed record ServiceRequestSubmittedEvent(
    Guid RequestId, Guid ServiceId, Guid CitizenId, string ReferenceNumber
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
