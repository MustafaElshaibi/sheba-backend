using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.StepHandlers;

/// <summary>
/// Result of executing a workflow step.
/// </summary>
public sealed record StepExecutionResult(
    bool Success,
    bool AdvanceWorkflow,         // true = move to next step; false = wait (e.g., payment pending)
    string? ResultJson = null,
    string? ErrorMessage = null,
    string? PaymentUrl = null);   // only for Payment steps

/// <summary>
/// Interface for workflow step handlers. Each WorkflowStepType has a handler.
/// </summary>
public interface IWorkflowStepHandler
{
    WorkflowStepType StepType { get; }

    Task<StepExecutionResult> ExecuteAsync(
        ServiceRequestEntity request,
        ServiceWorkflowStep stepDefinition,
        RequestStepExecution execution,
        CancellationToken ct = default);
}
