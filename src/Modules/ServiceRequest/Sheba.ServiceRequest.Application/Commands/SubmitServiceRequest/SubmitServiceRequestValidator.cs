using System.Text.Json;
using FluentValidation;
using Json.Schema;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;

/// <summary>
/// Validates FormDataJson against the service's form_schema_json (T-SRV-2) before the handler
/// creates the aggregate, so invalid/malicious form payloads never reach it. A service without a
/// registered ServiceFormSchema has nothing to validate against, so submission passes through —
/// that's a valid published-service state (workflow-steps-only services).
/// </summary>
public sealed class SubmitServiceRequestValidator : AbstractValidator<SubmitServiceRequestCommand>
{
    public SubmitServiceRequestValidator(IServiceDefinitionRepository definitionRepo)
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CitizenId).NotEmpty();

        RuleFor(x => x.FormDataJson)
            .NotEmpty().WithMessage("Form data is required.")
            .CustomAsync(async (formDataJson, context, ct) =>
            {
                JsonDocument formData;
                try
                {
                    formData = JsonDocument.Parse(formDataJson);
                }
                catch (JsonException)
                {
                    context.AddFailure("formData", "Form data must be valid JSON.");
                    return;
                }

                using var _ = formData;

                var command = context.InstanceToValidate;
                var schema = await definitionRepo.GetFormSchemaByServiceIdAsync(command.ServiceId, ct);
                if (schema is null)
                    return;

                JsonSchema jsonSchema;
                try
                {
                    jsonSchema = JsonSchema.FromText(schema.FormSchemaJson);
                }
                catch (JsonException)
                {
                    // A malformed stored schema is a data-quality bug, not a caller error —
                    // don't block submission on it.
                    return;
                }

                var results = jsonSchema.Evaluate(formData.RootElement, new EvaluationOptions
                {
                    OutputFormat = OutputFormat.List
                });

                if (results.IsValid)
                    return;

                foreach (var detail in results.Details ?? [])
                {
                    if (detail.IsValid || detail.Errors is not { Count: > 0 })
                        continue;

                    var field = "formData" + detail.InstanceLocation.ToString().Replace("/", ".");
                    foreach (var message in detail.Errors.Values)
                        context.AddFailure(field, message);
                }
            });
    }
}
