using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.StepHandlers;

/// <summary>
/// Handles MinistryApiCall workflow steps:
/// 1. Loads the ministry endpoint + auth config from the Ministry module repository
/// 2. Authenticates via the correct adapter (ApiKey, OIDC, Bearer, BasicAuth)
/// 3. Calls the ministry API with Polly retry + timeout
/// 4. Stores the response and advances the workflow
/// </summary>
public sealed class MinistryCallStepHandler(
    IMinistryRepository ministryRepo,
    IEnumerable<IMinistryAuthAdapter> authAdapters,
    IHttpClientFactory httpClientFactory,
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

        // 1. Load endpoint from Ministry module
        var endpoint = await ministryRepo.GetEndpointByIdAsync(stepDefinition.MinistryEndpointId.Value, ct);
        if (endpoint is null)
            return new StepExecutionResult(false, true, ErrorMessage: $"Ministry endpoint {stepDefinition.MinistryEndpointId} not found.");

        // 2. Load auth config + credential
        Ministry.Domain.Entities.MinistryAuthConfig? authConfig = null;
        Ministry.Domain.Entities.MinistryAuthCredential? credential = null;
        if (endpoint.AuthConfigId.HasValue)
        {
            authConfig = await ministryRepo.GetAuthConfigByIdAsync(endpoint.AuthConfigId.Value, ct);
            if (authConfig is not null)
                credential = authConfig.Credential ?? await ministryRepo.GetCredentialByAuthConfigIdAsync(authConfig.Id, ct);
        }

        // 3. Build HTTP request
        var baseUrl = authConfig?.BaseUrl ?? "";
        var path = endpoint.PathTemplate;
        // Substitute {citizenId} in path template if present
        path = path.Replace("{citizenId}", request.CitizenId.ToString(), StringComparison.OrdinalIgnoreCase);

        var httpMethod = new HttpMethod(endpoint.HttpMethod);
        var httpRequest = new HttpRequestMessage(httpMethod, $"{baseUrl}{path}");

        // If POST/PUT, include form data as body
        if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
        {
            httpRequest.Content = new StringContent(
                request.FormDataJson ?? "{}", Encoding.UTF8, "application/json");
        }

        // 4. Authenticate
        if (authConfig is not null && credential is not null)
        {
            var adapter = authAdapters.FirstOrDefault(a =>
                string.Equals(a.AdapterType, authConfig.AuthType.ToString(), StringComparison.OrdinalIgnoreCase));
            if (adapter is not null)
                await adapter.AuthenticateRequestAsync(httpRequest, authConfig, credential, ct);
        }

        // 5. Execute with timeout
        var sw = Stopwatch.StartNew();
        try
        {
            request.MarkAwaitingMinistry();

            var client = httpClientFactory.CreateClient("MinistryClient");
            client.Timeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds);

            var response = await client.SendAsync(httpRequest, ct);
            sw.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var resultJson = $"{{\"statusCode\":{(int)response.StatusCode},\"durationMs\":{sw.ElapsedMilliseconds},\"body\":{(responseBody.StartsWith('{') || responseBody.StartsWith('[') ? responseBody : $"\"{responseBody}\"")}}}";

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "[MinistryCallStep] {Method} {Path} returned {Status} in {Ms}ms for request {Ref}",
                    endpoint.HttpMethod, path, (int)response.StatusCode, sw.ElapsedMilliseconds, request.ReferenceNumber);

                return new StepExecutionResult(true, true, ResultJson: resultJson);
            }
            else
            {
                logger.LogWarning(
                    "[MinistryCallStep] {Method} {Path} failed with {Status} for request {Ref}",
                    endpoint.HttpMethod, path, (int)response.StatusCode, request.ReferenceNumber);

                return new StepExecutionResult(false, true,
                    ResultJson: resultJson,
                    ErrorMessage: $"Ministry API returned {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[MinistryCallStep] Exception calling {Path} for request {Ref}",
                path, request.ReferenceNumber);

            return new StepExecutionResult(false, true,
                ErrorMessage: ex.Message,
                ResultJson: $"{{\"exception\":\"{ex.GetType().Name}\",\"durationMs\":{sw.ElapsedMilliseconds}}}");
        }
    }
}
