using Sheba.ServiceRequest.Domain.Enums;
using Sheba.Shared.Kernel.Entities;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// Tracks the execution of a single workflow step for a service request.
/// Provides an audit trail of workflow progress.
/// </summary>
public sealed class RequestStepExecution : BaseEntity
{
    public Guid RequestId { get; private set; }
    public Guid StepId { get; private set; }           // FK to ServiceWorkflowStep
    public int StepOrder { get; private set; }
    public StepExecutionStatus Status { get; private set; } = StepExecutionStatus.Pending;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? ActorType { get; private set; }     // CITIZEN, SYSTEM, MINISTRY, ADMIN
    public string? ResultJson { get; private set; }     // ministry API response, decision, etc.
    public string? ErrorMessage { get; private set; }

    private RequestStepExecution() { }

    public static RequestStepExecution Create(
        Guid requestId, Guid stepId, int stepOrder,
        Guid? actorId = null, string? actorType = null)
    {
        return new RequestStepExecution
        {
            RequestId = requestId,
            StepId = stepId,
            StepOrder = stepOrder,
            ActorId = actorId,
            ActorType = actorType,
            Status = StepExecutionStatus.Running
        };
    }

    public void MarkCompleted(string? resultJson = null)
    {
        Status = StepExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ResultJson = resultJson;
        Touch();
    }

    public void MarkFailed(string errorMessage, string? resultJson = null)
    {
        Status = StepExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        ResultJson = resultJson;
        Touch();
    }

    public void MarkSkipped()
    {
        Status = StepExecutionStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }
}
