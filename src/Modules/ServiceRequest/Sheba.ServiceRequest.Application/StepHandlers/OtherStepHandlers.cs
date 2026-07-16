using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.StepHandlers;

/// <summary>Handles WebhookWait steps — pauses workflow until ministry webhook arrives.</summary>
public sealed class WebhookWaitStepHandler(ILogger<WebhookWaitStepHandler> logger) : IWorkflowStepHandler
{
    public WorkflowStepType StepType => WorkflowStepType.WebhookWait;

    public Task<StepExecutionResult> ExecuteAsync(
        ServiceRequestEntity request, ServiceWorkflowStep stepDefinition,
        RequestStepExecution execution, CancellationToken ct = default)
    {
        request.MarkAwaitingMinistry();
        logger.LogInformation("[WebhookWaitStep] Request {Ref} paused waiting for ministry webhook", request.ReferenceNumber);
        return Task.FromResult(new StepExecutionResult(true, false, // do NOT advance — wait for webhook
            ResultJson: "{\"waitingForWebhook\":true}"));
    }
}

/// <summary>Handles AdminReview steps — pauses workflow until admin approves.</summary>
public sealed class AdminReviewStepHandler(ILogger<AdminReviewStepHandler> logger) : IWorkflowStepHandler
{
    public WorkflowStepType StepType => WorkflowStepType.AdminReview;

    public Task<StepExecutionResult> ExecuteAsync(
        ServiceRequestEntity request, ServiceWorkflowStep stepDefinition,
        RequestStepExecution execution, CancellationToken ct = default)
    {
        request.MarkUnderReview();
        logger.LogInformation("[AdminReviewStep] Request {Ref} awaiting admin review", request.ReferenceNumber);
        return Task.FromResult(new StepExecutionResult(true, false, // wait for admin action
            ResultJson: "{\"awaitingAdminReview\":true}"));
    }
}

/// <summary>Auto-completes Notification and DocumentIssue steps (placeholder for full impl).</summary>
public sealed class AutoCompleteStepHandler(ILogger<AutoCompleteStepHandler> logger) : IWorkflowStepHandler
{
    public WorkflowStepType StepType => WorkflowStepType.Notification; // also used for DocumentIssue, CitizenSubmit

    public Task<StepExecutionResult> ExecuteAsync(
        ServiceRequestEntity request, ServiceWorkflowStep stepDefinition,
        RequestStepExecution execution, CancellationToken ct = default)
    {
        logger.LogInformation("[AutoCompleteStep] Auto-completing {StepType} for request {Ref}",
            stepDefinition.StepType, request.ReferenceNumber);
        return Task.FromResult(new StepExecutionResult(true, true,
            ResultJson: $"{{\"autoCompleted\":true,\"stepType\":\"{stepDefinition.StepType}\"}}"));
    }
}
