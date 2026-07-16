namespace Sheba.ServiceRequest.Domain.Enums;

/// <summary>Status of a workflow step execution.</summary>
public enum StepExecutionStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Skipped = 5
}
