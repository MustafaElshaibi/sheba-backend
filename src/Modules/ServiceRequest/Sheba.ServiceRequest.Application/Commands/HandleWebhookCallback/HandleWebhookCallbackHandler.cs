using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Application.Commands.HandleWebhookCallback;

/// <summary>
/// Handles incoming webhook callbacks from ministry systems.
///
/// SECURITY (§7.4 / BR-MI-5): the callback is verified — HMAC signature, timestamp window, and
/// delivery-id dedup — BEFORE anything is parsed or any state changes. An unverified callback can
/// advance a citizen's request through the workflow, so verification is the gate, not an
/// afterthought. Verification lives behind the Ministry-owned <see cref="IMinistryWebhookVerifier"/>
/// port (declared in Shared.Kernel, T-ARC-1) because the signing secret and its decryption belong
/// inside the Ministry boundary.
/// </summary>
public sealed class HandleWebhookCallbackHandler(
    IServiceRequestRepository requestRepo,
    IMinistryWebhookVerifier webhookVerifier,
    IMediator mediator,
    ILogger<HandleWebhookCallbackHandler> logger
) : IRequestHandler<HandleWebhookCallbackCommand, HandleWebhookCallbackResponse>
{
    public async Task<HandleWebhookCallbackResponse> Handle(
        HandleWebhookCallbackCommand command, CancellationToken ct)
    {
        // ── Verify signature + timestamp + delivery id before ANY processing ──
        var verification = await webhookVerifier.VerifyAsync(
            command.MinistryId, command.EventType, command.PayloadJson,
            command.Signature, command.Timestamp, command.DeliveryId, ct);

        if (!verification.IsValid)
        {
            logger.LogWarning(
                "[Webhook] Rejected callback from Ministry {MinistryId} ({EventType}): {Status}",
                command.MinistryId, command.EventType, verification.Status);
            // Deliberately vague to the caller — do not reveal which check failed.
            return new HandleWebhookCallbackResponse(false, "Webhook verification failed.");
        }

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
