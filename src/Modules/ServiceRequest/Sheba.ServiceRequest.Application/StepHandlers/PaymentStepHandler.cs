using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Application.StepHandlers;

/// <summary>
/// Handles the Payment workflow step:
/// 1. Calculates total fees from the service definition
/// 2. Creates a payment order via the Shared.Kernel port (T-ARC-1 — Payment.Domain never
///    referenced directly)
/// 3. Returns payment URL; workflow pauses until MarkPaymentComplete
/// </summary>
public sealed class PaymentStepHandler(
    IServiceDefinitionRepository definitionRepo,
    IPaymentOrderPort paymentOrders,
    ILogger<PaymentStepHandler> logger
) : IWorkflowStepHandler
{
    public WorkflowStepType StepType => WorkflowStepType.Payment;

    public async Task<StepExecutionResult> ExecuteAsync(
        ServiceRequestEntity request,
        ServiceWorkflowStep stepDefinition,
        RequestStepExecution execution,
        CancellationToken ct = default)
    {
        // Check if payment already exists for this request
        var existing = await paymentOrders.GetByServiceRequestIdAsync(request.Id, ct);
        if (existing is not null)
        {
            return new StepExecutionResult(true, false,
                PaymentUrl: existing.PaymentUrl,
                ResultJson: $"{{\"paymentOrderId\":\"{existing.Id}\",\"status\":\"{existing.Status}\"}}");
        }

        // Calculate total from service fees
        var fees = await definitionRepo.GetFeesByServiceAsync(request.ServiceId, ct);
        var mandatoryTotal = fees.Where(f => f.IsMandatory).Sum(f => f.Amount);

        if (mandatoryTotal <= 0)
        {
            // No fees — auto-complete the payment step
            logger.LogInformation("[PaymentStep] No fees for request {Ref} — auto-completing", request.ReferenceNumber);
            return new StepExecutionResult(true, true, ResultJson: "{\"noFeesRequired\":true}");
        }

        var currency = fees.FirstOrDefault()?.Currency ?? "YER";
        var order = await paymentOrders.CreateOrderAsync(
            request.Id, request.CitizenId, mandatoryTotal, currency,
            $"Payment for {request.ReferenceNumber}", ct);

        // Mark request as payment pending — workflow pauses here
        request.MarkPaymentPending();

        logger.LogInformation(
            "[PaymentStep] Created PaymentOrder {OrderNum} ({Amount} {Currency}) for request {Ref}",
            order.OrderNumber, mandatoryTotal, currency, request.ReferenceNumber);

        return new StepExecutionResult(
            true, false,   // do NOT advance — wait for MarkPaymentComplete
            ResultJson: $"{{\"paymentOrderId\":\"{order.Id}\",\"amount\":{mandatoryTotal},\"currency\":\"{currency}\"}}",
            PaymentUrl: order.PaymentUrl);
    }
}
