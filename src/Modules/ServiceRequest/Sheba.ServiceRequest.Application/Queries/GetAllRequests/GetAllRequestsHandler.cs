using MediatR;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Queries.GetAllRequests;

public sealed class GetAllRequestsHandler(IServiceRequestRepository repo)
    : IRequestHandler<GetAllRequestsQuery, GetAllRequestsResponse>
{
    public async Task<GetAllRequestsResponse> Handle(GetAllRequestsQuery q, CancellationToken ct)
    {
        var total = await repo.CountAllAsync(q.Status, q.ServiceId, q.MinistryId, q.FromDate, q.ToDate, ct);
        var items = await repo.GetAllAsync(q.Status, q.ServiceId, q.MinistryId, q.FromDate, q.ToDate, q.Page, q.PageSize, ct);
        var totalPages = (int)Math.Ceiling(total / (double)q.PageSize);

        return new GetAllRequestsResponse(
            items.Select(r => new AdminRequestDto(
                r.Id, r.ReferenceNumber, r.ServiceId, r.CitizenId,
                r.Status.ToString(), r.CurrentStep, r.Priority,
                r.SubmittedAt, r.CompletedAt
            )).ToList(),
            total, q.Page, q.PageSize, totalPages);
    }
}
