using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.SuspendAccount;

/// <summary>
/// Admin places a security hold on an Approved account (sheba.md §6.2).
///
/// API: POST /api/admin/accounts/{accountId}/suspend
/// Auth: Requires admin role = IDENTITY_REVIEWER or SUPER_ADMIN
/// </summary>
public sealed record SuspendAccountCommand(
    Guid AccountId,
    string? Reason
) : IRequest<Result<SuspendAccountResponse>>;

public sealed record SuspendAccountResponse(
    Guid AccountId,
    string Message
);
