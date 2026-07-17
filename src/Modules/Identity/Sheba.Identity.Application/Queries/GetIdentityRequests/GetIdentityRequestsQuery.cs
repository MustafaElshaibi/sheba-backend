using MediatR;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Queries.GetIdentityRequests;

/// <summary>
/// Admin query: get paginated list of identity requests filtered by status.
///
/// API: GET /api/admin/identity-requests?status=Pending&page=1&pageSize=20
/// Auth: IDENTITY_REVIEWER or SUPER_ADMIN
/// </summary>
public sealed record GetIdentityRequestsQuery(
    RequestStatus? Status    = null,
    int            Page      = 1,
    int            PageSize  = 20
) : IRequest<Result<GetIdentityRequestsResponse>>;

public sealed record IdentityRequestSummary(
    Guid          RequestId,
    Guid          AccountId,
    string        FullNameAr,
    string        FullNameEn,
    string        MaskedNid,
    RequestStatus Status,
    RequestType   RequestType,
    DateTime      SubmittedAt,
    DateTime?     ReviewedAt
);

public sealed record GetIdentityRequestsResponse(
    IReadOnlyList<IdentityRequestSummary> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
