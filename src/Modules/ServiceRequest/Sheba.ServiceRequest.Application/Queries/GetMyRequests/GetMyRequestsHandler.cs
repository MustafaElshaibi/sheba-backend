using MediatR;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Queries.GetMyRequests;

public sealed class GetMyRequestsHandler(IServiceRequestRepository repo)
    : IRequestHandler<GetMyRequestsQuery, List<CitizenRequestDto>>
{
    public async Task<List<CitizenRequestDto>> Handle(GetMyRequestsQuery request, CancellationToken ct)
    {
        var requests = await repo.GetByCitizenAsync(request.CitizenId, ct);
        return requests.Select(r => new CitizenRequestDto(
            r.Id, r.ReferenceNumber, r.ServiceId,
            r.Status.ToString(), r.CurrentStep, r.Priority,
            r.SubmittedAt, r.CompletedAt, r.DueDate
        )).ToList();
    }
}
