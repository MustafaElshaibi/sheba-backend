using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ReinstateAccount;

/// <summary>
/// Admin lifts a security hold on a Suspended account (sheba.md §6.2).
///
/// API: POST /api/admin/accounts/{accountId}/reinstate
/// Auth: Requires admin role = IDENTITY_REVIEWER or SUPER_ADMIN
/// </summary>
public sealed record ReinstateAccountCommand(
    Guid AccountId
) : IRequest<Result<ReinstateAccountResponse>>;

public sealed record ReinstateAccountResponse(
    Guid AccountId,
    string Message
);
