using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Payment.Domain.Interfaces;
using Sheba.ServiceRequest.Application.Commands.ExecuteNextStep;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.MarkPaymentComplete;

/// <summary>
/// Called when a payment is completed (mock or real gateway callback).
/// Marks the payment as paid, advances the workflow past the Payment step.
/// </summary>
public sealed class MarkPaymentCompleteHandler(
    IPaymentRepository paymentRepo,
    IServiceRequestRepository requestRepo,
    IMediator mediator,
    ILogger<MarkPaymentCompleteHandler> logger
) : IRequestHandler<MarkPaymentCompleteCommand, MarkPaymentCompleteResponse>
{
    public async Task<MarkPaymentCompleteResponse> Handle(
        MarkPaymentCompleteCommand command, CancellationToken ct)
    {
        var order = await paymentRepo.GetByIdAsync(command.PaymentOrderId, ct)
            ?? throw new NotFoundException("PaymentOrder", command.PaymentOrderId);

        // NotFoundException (not Forbidden) for a non-owner — consistent with the rest of the
        // codebase's anti-enumeration posture: don't confirm another citizen's order exists.
        if (!command.IsAdmin && order.CitizenId != command.ActorId)
            throw new NotFoundException("PaymentOrder", command.PaymentOrderId);

        if (order.Status == Payment.Domain.Enums.PaymentStatus.Completed)
            return new MarkPaymentCompleteResponse(order.ServiceRequestId, "Payment already completed.");

        order.MarkPaid(command.GatewayReference);
        await paymentRepo.SaveChangesAsync(ct);

        // Complete the active payment step execution
        var activeStep = await requestRepo.GetActiveStepForRequestAsync(order.ServiceRequestId, ct);
        if (activeStep is not null)
            activeStep.MarkCompleted($"{{\"paymentOrderId\":\"{order.Id}\",\"gatewayRef\":\"{order.GatewayReference}\"}}");

        // Advance the request past the payment step
        var request = await requestRepo.GetByIdAsync(order.ServiceRequestId, ct);
        if (request is not null)
        {
            // Move to next step
            var steps = await requestRepo.GetStepExecutionsByRequestAsync(request.Id, ct);
            var nextStepOrder = request.CurrentStep + 1;
            request.AdvanceToStep(nextStepOrder, RequestLifecycleStatus.Processing);
            await requestRepo.SaveChangesAsync(ct);

            // Trigger the next step execution
            await mediator.Send(new ExecuteNextStepCommand(request.Id), ct);
        }

        logger.LogInformation("[MarkPaymentComplete] Payment {OrderId} completed for request {RequestId}",
            command.PaymentOrderId, order.ServiceRequestId);

        return new MarkPaymentCompleteResponse(order.ServiceRequestId, "Payment confirmed. Workflow advanced.");
    }
}
