using MediatR;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Application.Queries.GetRequestById;

public sealed class GetRequestByIdHandler(IServiceRequestRepository repo)
    : IRequestHandler<GetRequestByIdQuery, RequestDetailDto?>
{
    public async Task<RequestDetailDto?> Handle(GetRequestByIdQuery request, CancellationToken ct)
    {
        var r = await repo.GetByIdAsync(request.RequestId, ct);
        if (r is null) return null;

        // Ownership check: a non-admin caller only ever sees their own requests. Returning null
        // (not a 403) keeps this from confirming that a request with this id exists at all.
        if (!request.IsAdmin && r.CitizenId != request.ActorId)
            return null;

        return new RequestDetailDto(
            r.Id, r.ReferenceNumber, r.ServiceId, r.CitizenId,
            r.Status.ToString(), r.CurrentStep, r.Priority,
            r.SubmittedAt, r.CompletedAt, r.DueDate,
            r.RejectionReason, r.FormDataJson,
            r.StepExecutions.OrderBy(s => s.StepOrder).Select(s => new StepExecutionDto(
                s.Id, s.StepOrder, s.Status.ToString(), s.StartedAt,
                s.CompletedAt, s.ActorType, s.ErrorMessage
            )).ToList());
    }
}
