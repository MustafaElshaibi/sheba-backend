using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.UpdateServiceDefinition;

public sealed class UpdateServiceDefinitionHandler(
    IServiceDefinitionRepository repository,
    ILogger<UpdateServiceDefinitionHandler> logger
) : IRequestHandler<UpdateServiceDefinitionCommand, UpdateServiceDefinitionResponse>
{
    public async Task<UpdateServiceDefinitionResponse> Handle(
        UpdateServiceDefinitionCommand request, CancellationToken ct)
    {
        var service = await repository.GetServiceByIdAsync(request.ServiceId, ct)
            ?? throw new NotFoundException("ServiceDefinition", request.ServiceId);

        // T-AUTH-1: a MinistryManager may only update services their own ministry owns.
        // NotFoundException, not UnauthorizedAccessException — same anti-enumeration shape used
        // elsewhere for ownership checks (BR-DO-1) so a scoped admin can't probe which service
        // IDs exist outside their ministry by distinguishing 403 from 404.
        if (request.ActorMinistryId is { } actorMinistryId && service.MinistryId != actorMinistryId)
            throw new NotFoundException("ServiceDefinition", request.ServiceId);

        service.Update(
            request.NameAr, request.NameEn,
            request.DescriptionAr, request.DescriptionEn,
            request.RequiredLoa, request.RequiresAppointment,
            request.IsOnline, request.AverageDays, request.DisplayOrder);

        if (request.Publish == true) service.Publish();
        else if (request.Publish == false) service.Depublish();

        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[UpdateServiceDefinition] Updated {Code} (Id={Id})", service.Code, service.Id);
        return new UpdateServiceDefinitionResponse(service.Id, "Service updated successfully.");
    }
}
