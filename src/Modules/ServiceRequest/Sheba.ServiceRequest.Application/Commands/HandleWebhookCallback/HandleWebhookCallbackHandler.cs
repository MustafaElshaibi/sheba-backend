using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Commands.HandleWebhookCallback;

/// <summary>
/// Handles incoming webhook callbacks from ministry systems.
/// Finds the waiting workflow step, stores the callback data, and advances the workflow.
/// </summary>
public sealed class HandleWebhookCallbackHandler(
    IServiceRequestRepository requestRepo,
    IMediator mediator,
    ILogger<HandleWebhookCallbackHandler> logger
) : IRequestHandler<HandleWebhookCallbackCommand, HandleWebhookCallbackResponse>
{
    public async Task<HandleWebhookCallbackResponse> Handle(
        HandleWebhookCallbackCommand command, CancellationToken ct)
    {
        // Parse the payload to find the request reference
        Guid? requestId = null;
        try
        {
            using var doc = JsonDocument.Parse(command.PayloadJson);
            if (doc.RootElement.TryGetProperty("requestId", out var reqIdProp))
                requestId = reqIdProp.GetGuid();
            else if (doc.RootElement.TryGetProperty("referenceNumber", out var refProp))
            {
                var refNum = refProp.GetString();
                if (!string.IsNullOrEmpty(refNum))
                {
                    var req = await requestRepo.GetByReferenceAsync(refNum, ct);
                    requestId = req?.Id;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Webhook] Failed to parse webhook payload");
        }

        if (!requestId.HasValue)
            return new HandleWebhookCallbackResponse(false, "Could not identify the service request from webhook payload.");

        var request = await requestRepo.GetByIdAsync(requestId.Value, ct);
        if (request is null)
            return new HandleWebhookCallbackResponse(false, $"Service request {requestId} not found.");

        if (request.Status != RequestLifecycleStatus.AwaitingMinistry)
            return new HandleWebhookCallbackResponse(false, $"Request is not awaiting ministry response (status: {request.Status}).");

        // Complete the active step with the webhook data
        var activeStep = await requestRepo.GetActiveStepForRequestAsync(request.Id, ct);
        if (activeStep is not null)
            activeStep.MarkCompleted(command.PayloadJson);

        // Advance the workflow
        var nextStepOrder = request.CurrentStep + 1;
        request.AdvanceToStep(nextStepOrder, RequestLifecycleStatus.Processing);
        await requestRepo.SaveChangesAsync(ct);

        // Continue workflow execution
        await mediator.Send(new ExecuteNextStepCommand(request.Id), ct);

        logger.LogInformation(
            "[Webhook] Processed {EventType} from Ministry {MinistryId} for request {RequestId}",
            command.EventType, command.MinistryId, requestId);

        return new HandleWebhookCallbackResponse(true, "Webhook processed. Workflow advanced.");
    }
}
