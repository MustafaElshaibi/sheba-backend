using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Entities;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.SubmitServiceRequest;

public sealed class SubmitServiceRequestHandler(
    IServiceDefinitionRepository definitionRepo,
    IServiceRequestRepository requestRepo,
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

        // 2. Create the service request
        var request = ServiceRequestEntity.Create(
            command.ServiceId, command.CitizenId,
            command.FormDataJson, command.Priority,
            service.AverageDays);

        await requestRepo.AddAsync(request, ct);

        // 3. Create step executions from the service's workflow definition
        var steps = service.WorkflowSteps.OrderBy(s => s.StepOrder).ToList();
        if (steps.Count > 0)
        {
            // Create a pending execution for each workflow step
            foreach (var step in steps)
            {
                var execution = RequestStepExecution.Create(
                    request.Id, step.Id, step.StepOrder, actorType: step.Actor.ToString());
                await requestRepo.AddStepExecutionAsync(execution, ct);
            }
        }

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
