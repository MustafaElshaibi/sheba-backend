using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.DeactivateAccount;

/// <summary>
/// Admin or citizen-requested closure of an Approved account (sheba.md §6.2). Terminal — there is
/// no transition out of Deactivated.
///
/// API: POST /api/admin/accounts/{accountId}/deactivate
/// Auth: Requires admin role = IDENTITY_REVIEWER or SUPER_ADMIN
/// </summary>
public sealed record DeactivateAccountCommand(
    Guid AccountId,
    string? Reason
) : IRequest<Result<DeactivateAccountResponse>>;

public sealed record DeactivateAccountResponse(
    Guid AccountId,
    string Message
);
