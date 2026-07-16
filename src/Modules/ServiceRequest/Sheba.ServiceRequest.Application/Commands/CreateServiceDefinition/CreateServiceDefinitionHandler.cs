using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.CreateServiceDefinition;

public sealed class CreateServiceDefinitionHandler(
    IServiceDefinitionRepository repository,
    ILogger<CreateServiceDefinitionHandler> logger
) : IRequestHandler<CreateServiceDefinitionCommand, CreateServiceDefinitionResponse>
{
    public async Task<CreateServiceDefinitionResponse> Handle(
        CreateServiceDefinitionCommand request, CancellationToken ct)
    {
        // Guard: code must be unique
        var existing = await repository.GetServiceByCodeAsync(request.Code.Trim().ToUpperInvariant(), ct);
        if (existing is not null)
            throw new DomainException($"Service code '{request.Code}' is already in use.");

        // Guard: category must exist
        _ = await repository.GetCategoryByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("ServiceCategory", request.CategoryId);

        var service = ServiceDefinition.Create(
            request.CategoryId, request.MinistryId, request.Code,
            request.NameAr, request.NameEn, request.RequiredLoa,
            request.DescriptionAr, request.DescriptionEn, request.AverageDays);

        await repository.AddServiceAsync(service, ct);

        // If form schema is provided, create it
        if (!string.IsNullOrWhiteSpace(request.FormSchemaJson))
        {
            var schema = ServiceFormSchema.Create(
                service.Id, request.FormSchemaJson, request.UiSchemaJson);
            await repository.AddFormSchemaAsync(schema, ct);
        }

        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[CreateServiceDefinition] Created {Code} (Id={Id})", service.Code, service.Id);
        return new CreateServiceDefinitionResponse(service.Id, service.Code, service.NameEn);
    }
}
