using MediatR;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.CreateAdminUser;

/// <summary>
/// Provisions a new admin account (SuperAdmin-only). Needed as a minimal prerequisite for
/// T-AUTH-1: without any way to create a MinistryManager scoped to a specific ministry, the
/// ministry_id ownership model has nothing to attach to. Password reset for the new account is
/// out of scope here — the created admin signs in with the password provided at creation time,
/// same as the seeded SuperAdmin.
/// </summary>
public sealed record CreateAdminUserCommand(
    string EmployeeId,
    string Email,
    string FullName,
    AdminRole Role,
    string Password,
    string? Department,
    Guid? MinistryId
) : IRequest<Result<CreateAdminUserResponse>>;

public sealed record CreateAdminUserResponse(Guid AdminId, string EmployeeId, string Role);
