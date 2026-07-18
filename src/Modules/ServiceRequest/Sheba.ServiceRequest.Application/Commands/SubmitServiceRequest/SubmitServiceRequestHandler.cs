using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;

/// <summary>
/// BR-SR-2 submission gates (T-SRV-3): LoA ≥ required_loa and every mandatory document type
/// present, both enforced here as 422s before a request is ever created. The account-Approved
/// check is implicit — only an Approved citizen can hold a bearer token with a "loa" claim at all
/// (BR-ON-10), so reaching this handler already proves it.
/// </summary>
public sealed class SubmitServiceRequestHandler(
    IServiceDefinitionRepository definitionRepo,
    IServiceRequestRepository requestRepo,
    IDocumentPort documentPort,
    ILogger<SubmitServiceRequestHandler> logger
) : IRequestHandler<SubmitServiceRequestCommand, SubmitServiceRequestResponse>
{
    public async Task<SubmitServiceRequestResponse> Handle(
        SubmitServiceRequestCommand command, CancellationToken ct)
    {
        // 1. Load the service definition with workflow steps
        var service = await definitionRepo.GetServiceByIdAsync(command.ServiceId, ct)
            ?? throw new NotFoundException("ServiceDefinition", command.ServiceId);

        if (!service.IsActive)
            throw new DomainException("This service is currently unavailable.");

        if (command.CitizenLoa < service.RequiredLoa)
        {
            throw new DomainException(
                $"This service requires identity level {service.RequiredLoa}; your account is at level {command.CitizenLoa}.");
        }

        var mandatoryTypes = service.RequiredDocuments
            .Where(d => d.IsMandatory)
            .Select(d => d.DocumentType)
            .ToList();
        if (mandatoryTypes.Count > 0)
        {
            var ownedTypes = await documentPort.GetOwnerDocumentTypesAsync(command.CitizenId, ct);
            var missing = mandatoryTypes.Where(t => !ownedTypes.Contains(t)).ToList();
            if (missing.Count > 0)
            {
                throw new DomainException(
                    $"Missing required document(s): {string.Join(", ", missing)}.");
            }
        }

        // 2. Create the service request
        var request = ServiceRequestEntity.Create(
            command.ServiceId, command.CitizenId,
            command.FormDataJson, command.Priority,
            service.AverageDays);

        await requestRepo.AddAsync(request, ct);

        // 3. Step executions are NOT pre-created here (T-SRV-4): ExecuteNextStep is the single
        // source of truth, creating exactly one execution row per step as it actually runs. The
        // old pre-creation left every step sitting in Running at submit, making "the active step"
        // ambiguous.

        // 4. Domain event (ServiceRequestSubmittedEvent) is raised inside Create()
        await requestRepo.SaveChangesAsync(ct);

        logger.LogInformation(
            "[SubmitServiceRequest] Created {Ref} for Service={ServiceId}, Citizen={CitizenId}",
            request.ReferenceNumber, command.ServiceId, command.CitizenId);

        return new SubmitServiceRequestResponse(
            request.Id, request.ReferenceNumber,
            request.Status.ToString(), "Service request submitted successfully.");
    }
}
