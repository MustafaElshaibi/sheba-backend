using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.ServiceRequest.Application.Commands.CancelServiceRequest;

public sealed class CancelServiceRequestHandler(
    IServiceRequestRepository requestRepo,
    ILogger<CancelServiceRequestHandler> logger
) : IRequestHandler<CancelServiceRequestCommand, CancelServiceRequestResponse>
{
    public async Task<CancelServiceRequestResponse> Handle(CancelServiceRequestCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct)
            ?? throw new NotFoundException("ServiceRequest", command.RequestId);

        // 404, not 403, on a cross-owner attempt — same anti-enumeration shape used elsewhere
        // (Ministry ownership, T-AUTH-1): a citizen probing other requests' ids learns nothing.
        if (request.CitizenId != command.CitizenId)
            throw new NotFoundException("ServiceRequest", command.RequestId);

        request.Cancel();
        await requestRepo.SaveChangesAsync(ct);

        logger.LogInformation("[CancelServiceRequest] {Ref} cancelled by citizen {CitizenId}",
            request.ReferenceNumber, command.CitizenId);

        return new CancelServiceRequestResponse(request.Id, request.Status.ToString(), "Request cancelled.");
    }
}
