using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Application.StepHandlers;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;

/// <summary>
/// Dispatches the current workflow step to the correct handler based on step type.
/// If the step completes and auto-advances, moves to the next step recursively.
/// </summary>
public sealed class ExecuteNextStepHandler(
    IServiceRequestRepository requestRepo,
    IServiceDefinitionRepository definitionRepo,
    IEnumerable<IWorkflowStepHandler> stepHandlers,
    ILogger<ExecuteNextStepHandler> logger
) : IRequestHandler<ExecuteNextStepCommand, ExecuteNextStepResponse>
{
    public async Task<ExecuteNextStepResponse> Handle(
        ExecuteNextStepCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct)
            ?? throw new NotFoundException("ServiceRequest", command.RequestId);

        if (request.Status is RequestLifecycleStatus.Completed or RequestLifecycleStatus.Rejected or RequestLifecycleStatus.Cancelled)
            return new ExecuteNextStepResponse(true, request.CurrentStep, request.Status.ToString(),
                Message: "Request is already in a terminal state.");

        // Load workflow steps for this service
        var workflowSteps = await definitionRepo.GetWorkflowStepsByServiceAsync(request.ServiceId, ct);
        if (workflowSteps.Count == 0)
        {
            // No workflow — auto-complete
            request.Complete();
            await requestRepo.SaveChangesAsync(ct);
            return new ExecuteNextStepResponse(true, request.CurrentStep, "Completed",
                Message: "No workflow steps defined — request completed.");
        }

        var currentStepDef = workflowSteps.FirstOrDefault(s => s.StepOrder == request.CurrentStep);
        if (currentStepDef is null)
        {
            // Past the last step — complete
            request.Complete();
            await requestRepo.SaveChangesAsync(ct);
            return new ExecuteNextStepResponse(true, request.CurrentStep, "Completed",
                Message: "All workflow steps completed.");
        }

        // Find the handler for this step type
        var handler = stepHandlers.FirstOrDefault(h => h.StepType == currentStepDef.StepType);
        if (handler is null)
        {
            // Use auto-complete for unhandled step types
            handler = stepHandlers.FirstOrDefault(h => h.StepType == WorkflowStepType.Notification);
            if (handler is null)
            {
                logger.LogWarning("[ExecuteNextStep] No handler for step type {Type}", currentStepDef.StepType);
                return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                    Message: $"No handler for step type {currentStepDef.StepType}.");
            }
        }

        // Create or reuse step execution
        var execution = await requestRepo.GetActiveStepForRequestAsync(request.Id, ct);
        if (execution is null)
        {
            execution = RequestStepExecution.Create(
                request.Id, currentStepDef.Id, currentStepDef.StepOrder,
                actorType: currentStepDef.Actor.ToString());
            await requestRepo.AddStepExecutionAsync(execution, ct);
        }

        // Execute the step
        request.MarkProcessing();
        var result = await handler.ExecuteAsync(request, currentStepDef, execution, ct);

        if (result.Success)
        {
            if (result.AdvanceWorkflow)
            {
                execution.MarkCompleted(result.ResultJson);

                request.RaiseStepCompleted(execution.Id, execution.StepOrder, currentStepDef.StepType.ToString());

                // Move to next step
                var nextStep = workflowSteps
                    .Where(s => s.StepOrder > currentStepDef.StepOrder)
                    .OrderBy(s => s.StepOrder)
                    .FirstOrDefault();

                if (nextStep is null)
                {
                    request.Complete();
                    await requestRepo.SaveChangesAsync(ct);
                    return new ExecuteNextStepResponse(true, request.CurrentStep, "Completed",
                        Message: "All workflow steps completed.");
                }

                request.AdvanceToStep(nextStep.StepOrder, RequestLifecycleStatus.Processing);
                await requestRepo.SaveChangesAsync(ct);

                // Recursively execute next step if automated
                if (nextStep.IsAutomated)
                    return await Handle(new ExecuteNextStepCommand(request.Id), ct);

                return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                    Message: $"Advanced to step {nextStep.StepOrder}: {nextStep.NameEn}");
            }
            else
            {
                // Step paused (payment pending, webhook wait, admin review)
                await requestRepo.SaveChangesAsync(ct);
                return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                    PaymentUrl: result.PaymentUrl,
                    Message: "Step is waiting for external action.");
            }
        }
        else
        {
            execution.MarkFailed(result.ErrorMessage ?? "Unknown error", result.ResultJson);
            await requestRepo.SaveChangesAsync(ct);
            return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                Message: $"Step failed: {result.ErrorMessage}");
        }
    }
}
