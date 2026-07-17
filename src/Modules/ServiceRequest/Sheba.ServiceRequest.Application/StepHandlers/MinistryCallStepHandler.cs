using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Application.StepHandlers;

/// <summary>
/// Handles MinistryApiCall workflow steps by delegating the entire call — endpoint/auth lookup,
/// authentication, HTTP send — to the Shared.Kernel <see cref="IMinistryCallPort"/> (T-ARC-1):
/// decrypted ministry credentials never cross into ServiceRequest.
/// </summary>
public sealed class MinistryCallStepHandler(
    IMinistryCallPort ministryCall,
    ILogger<MinistryCallStepHandler> logger
) : IWorkflowStepHandler
{
    public WorkflowStepType StepType => WorkflowStepType.MinistryApiCall;

    public async Task<StepExecutionResult> ExecuteAsync(
        ServiceRequestEntity request,
        ServiceWorkflowStep stepDefinition,
        RequestStepExecution execution,
        CancellationToken ct = default)
    {
        if (!stepDefinition.MinistryEndpointId.HasValue)
            return new StepExecutionResult(false, true, ErrorMessage: "No ministry endpoint configured for this step.");

        request.MarkAwaitingMinistry();

        var result = await ministryCall.InvokeAsync(
            stepDefinition.MinistryEndpointId.Value, request.CitizenId, request.FormDataJson, ct);

        var body = result.ResponseBody ?? "";
        var bodyJson = body.StartsWith('{') || body.StartsWith('[') ? body : $"\"{body}\"";
        var resultJson =
            $"{{\"statusCode\":{(result.StatusCode.HasValue ? result.StatusCode.Value.ToString() : "null")},"
            + $"\"durationMs\":{result.DurationMs},\"body\":{bodyJson}}}";

        if (result.Success)
        {
            logger.LogInformation(
                "[MinistryCallStep] Endpoint {EndpointId} succeeded in {Ms}ms for request {Ref}",
                stepDefinition.MinistryEndpointId, result.DurationMs, request.ReferenceNumber);

            return new StepExecutionResult(true, true, ResultJson: resultJson);
        }

        logger.LogWarning(
            "[MinistryCallStep] Endpoint {EndpointId} failed for request {Ref}: {Error}",
            stepDefinition.MinistryEndpointId, request.ReferenceNumber, result.ErrorMessage);

        return new StepExecutionResult(false, true, ResultJson: resultJson, ErrorMessage: result.ErrorMessage);
    }
}
