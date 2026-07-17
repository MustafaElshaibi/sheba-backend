using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RejectIdentityRequest;

/// <summary>
/// Admin rejects a citizen identity request with a reason.
///
/// API: POST /api/admin/identity-requests/{requestId}/reject
/// Auth: Requires admin role = IDENTITY_REVIEWER or SUPER_ADMIN
/// </summary>
public sealed record RejectIdentityRequestCommand(
    Guid   RequestId,
    Guid   ReviewedByAdminId,
    string RejectionReason,
    string? Notes = null
) : IRequest<Result<RejectIdentityRequestResponse>>;

public sealed record RejectIdentityRequestResponse(
    Guid RequestId,
    Guid AccountId,
    string Message
);
