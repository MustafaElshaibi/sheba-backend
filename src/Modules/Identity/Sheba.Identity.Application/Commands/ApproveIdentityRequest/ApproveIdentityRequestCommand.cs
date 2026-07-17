using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ApproveIdentityRequest;

/// <summary>
/// Admin approves a citizen identity request — account becomes Approved and active.
///
/// API: POST /api/admin/identity-requests/{requestId}/approve
/// Auth: Requires admin role = IDENTITY_REVIEWER or SUPER_ADMIN
/// </summary>
public sealed record ApproveIdentityRequestCommand(
    Guid RequestId,
    Guid ReviewedByAdminId,
    string? Notes = null
) : IRequest<Result<ApproveIdentityRequestResponse>>;

public sealed record ApproveIdentityRequestResponse(
    Guid RequestId,
    Guid AccountId,
    string Message
);
