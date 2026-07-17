using Sheba.Shared.Kernel.Events;

namespace Sheba.ServiceRequest.Domain.DomainEvents;

public sealed record ServiceRequestSubmittedEvent(
    Guid RequestId, Guid ServiceId, Guid CitizenId, string ReferenceNumber
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public sealed record WorkflowStepCompletedEvent(
    Guid RequestId, Guid StepExecutionId, int StepOrder, string StepType
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when a service request reaches the Completed lifecycle status.
/// Handlers:
///   - Admin module: updates daily completion analytics snapshot
///   - Notification module: sends completion notification to citizen
/// </summary>
public sealed record ServiceRequestCompletedEvent(
    Guid RequestId, Guid ServiceId, Guid CitizenId,
    DateTime SubmittedAt, DateTime CompletedAt
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
