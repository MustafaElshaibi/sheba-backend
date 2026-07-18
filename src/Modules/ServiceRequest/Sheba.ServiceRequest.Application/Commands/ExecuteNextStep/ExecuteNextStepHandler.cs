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

        // Create or reuse this step's single execution row (T-SRV-4: one row per executed step).
        var execution = await requestRepo.GetStepExecutionForStepAsync(request.Id, currentStepDef.StepOrder, ct);
        if (execution is null)
        {
            execution = RequestStepExecution.Create(
                request.Id, currentStepDef.Id, currentStepDef.StepOrder,
                actorType: currentStepDef.Actor.ToString());
            await requestRepo.AddStepExecutionAsync(execution, ct);
        }

        // Find the handler for this step type. T-SRV-4: an unhandled step type fails LOUDLY —
        // marking the step Failed and the request ActionRequired — instead of silently routing to
        // the Notification handler and auto-completing work that never actually ran.
        var handler = stepHandlers.FirstOrDefault(h => h.StepType == currentStepDef.StepType);
        if (handler is null)
        {
            logger.LogError("[ExecuteNextStep] No handler for step type {Type} — flagging ActionRequired", currentStepDef.StepType);
            execution.MarkFailed($"No handler registered for step type {currentStepDef.StepType}.");
            request.MarkActionRequired();
            await requestRepo.SaveChangesAsync(ct);
            return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                Message: $"No handler for step type {currentStepDef.StepType} — request needs attention.");
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

            // T-SRV-4: honor on_failure_step. If the step defines a failure route, jump there
            // (and re-execute if that step is automated); otherwise the request needs attention.
            var failureStep = currentStepDef.OnFailureStep is int fs
                ? workflowSteps.FirstOrDefault(s => s.StepOrder == fs)
                : null;

            if (failureStep is not null)
            {
                request.AdvanceToStep(failureStep.StepOrder, RequestLifecycleStatus.Processing);
                await requestRepo.SaveChangesAsync(ct);

                logger.LogWarning(
                    "[ExecuteNextStep] Step {Order} failed — routing to on_failure_step {Failure} for request {Ref}",
                    currentStepDef.StepOrder, failureStep.StepOrder, request.ReferenceNumber);

                if (failureStep.IsAutomated)
                    return await Handle(new ExecuteNextStepCommand(request.Id), ct);

                return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                    Message: $"Step failed — routed to recovery step {failureStep.StepOrder}.");
            }

            request.MarkActionRequired();
            await requestRepo.SaveChangesAsync(ct);
            return new ExecuteNextStepResponse(false, request.CurrentStep, request.Status.ToString(),
                Message: $"Step failed: {result.ErrorMessage}");
        }
    }
}
