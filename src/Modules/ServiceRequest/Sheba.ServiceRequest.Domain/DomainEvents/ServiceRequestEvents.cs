using Sheba.Shared.Kernel.Events;

namespace Sheba.ServiceRequest.Domain.DomainEvents;

/// <summary>
/// Raised for each workflow step completion. Purely internal to ServiceRequest today — no other
/// module subscribes — so it stays here rather than in Shared.Kernel (T-ARC-1: only event
/// contracts with cross-module consumers move out of the producer's Domain assembly).
/// </summary>
public sealed record WorkflowStepCompletedEvent(
    Guid RequestId, Guid StepExecutionId, int StepOrder, string StepType
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
