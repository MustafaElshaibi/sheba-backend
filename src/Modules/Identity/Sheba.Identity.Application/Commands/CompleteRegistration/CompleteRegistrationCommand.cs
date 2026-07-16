using MediatR;

namespace Sheba.Identity.Application.Commands.CompleteRegistration;

/// <summary>
/// Step 3 of citizen registration: citizen sets username, email, and password.
/// This moves the account to PendingAdminApproval and submits the IdentityRequest for review.
///
/// API: POST /api/identity/complete-registration
/// Body: { accountId, username, email, password, confirmPassword }
/// </summary>
public sealed record CompleteRegistrationCommand(
    Guid   AccountId,
    string Username,
    string Email,
    string Password,
    string ConfirmPassword
) : IRequest<CompleteRegistrationResponse>;

public sealed record CompleteRegistrationResponse(
    Guid AccountId,
    Guid IdentityRequestId,
    string Message
);
