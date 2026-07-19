using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Application.EventHandlers;

/// <summary>
/// Resumes a workflow paused at a Payment step once Payment.Application confirms the order
/// (T-PAY-1). Replaces the old <c>MarkPaymentCompleteCommand</c>, which called directly into the
/// Payment port and advanced the workflow inline from inside ServiceRequest's own command —
/// PaymentCompletedEvent is now the only coupling between the two modules.
///
/// Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise
/// double-advance the workflow.
/// </summary>
public sealed class AdvanceWorkflowOnPaymentCompletedHandler(
    IServiceRequestRepository requestRepo,
    IMediator mediator,
    IInboxGuard inboxGuard,
    ILogger<AdvanceWorkflowOnPaymentCompletedHandler> logger
) : INotificationHandler<PaymentCompletedEvent>
{
    private const string ConsumerName = nameof(AdvanceWorkflowOnPaymentCompletedHandler);

    public async Task Handle(PaymentCompletedEvent notification, CancellationToken ct)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        var request = await requestRepo.GetByIdAsync(notification.ServiceRequestId, ct);
        if (request is null)
        {
            logger.LogWarning(
                "[AdvanceWorkflowOnPaymentCompleted] ServiceRequest {RequestId} not found for paid order {OrderId}",
                notification.ServiceRequestId, notification.PaymentOrderId);
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);
            return;
        }

        var activeStep = await requestRepo.GetActiveStepForRequestAsync(request.Id, ct);
        activeStep?.MarkCompleted(
            $"{{\"paymentOrderId\":\"{notification.PaymentOrderId}\",\"gatewayRef\":\"{notification.GatewayReference}\"}}");

        request.AdvanceToStep(request.CurrentStep + 1, RequestLifecycleStatus.Processing);
        await requestRepo.SaveChangesAsync(ct);
        await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);

        await mediator.Send(new ExecuteNextStepCommand(request.Id), ct);

        logger.LogInformation(
            "[AdvanceWorkflowOnPaymentCompleted] Payment {OrderId} completed for request {RequestId} — workflow advanced",
            notification.PaymentOrderId, request.Id);
    }
}
