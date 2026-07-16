using MediatR;
using Sheba.ServiceRequest.Domain.Enums;

namespace Sheba.ServiceRequest.Application.Queries.GetAllRequests;

public sealed record GetAllRequestsQuery(
    RequestLifecycleStatus? Status = null,
    Guid? ServiceId = null,
    Guid? MinistryId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<GetAllRequestsResponse>;

public sealed record GetAllRequestsResponse(
    List<AdminRequestDto> Items,
    int TotalCount, int Page, int PageSize, int TotalPages);

public sealed record AdminRequestDto(
    Guid Id, string ReferenceNumber, Guid ServiceId, Guid CitizenId,
    string Status, int CurrentStep, string Priority,
    DateTime SubmittedAt, DateTime? CompletedAt);
