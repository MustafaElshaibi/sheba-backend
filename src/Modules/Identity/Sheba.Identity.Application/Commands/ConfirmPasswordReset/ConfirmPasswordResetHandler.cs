using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ConfirmPasswordReset;

public sealed class ConfirmPasswordResetHandler(
    IIdentityRepository repository,
    IPasswordHasher passwordHasher,
    IOtpHasher otpHasher,
    ILogger<ConfirmPasswordResetHandler> logger
) : IRequestHandler<ConfirmPasswordResetCommand, Result<ConfirmPasswordResetResponse>>
{
    private const int MaxAttempts = 3;

    // One identical message for every "this reset attempt doesn't work" outcome an unauthenticated
    // caller can reach — unknown identifier, no active code, expired code, too many attempts, or a
    // wrong code — so the confirm step can't be used to probe which identifiers have accounts or
    // active reset attempts, mirroring LoginCitizenHandler's GenericCredentialError.
    private const string GenericError = "Invalid or expired reset code. Please request a new one.";

    public async Task<Result<ConfirmPasswordResetResponse>> Handle(
        ConfirmPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByUsernameOrNidAsync(request.UsernameOrNid, cancellationToken);
        // Status re-checked here (not just at request time): if it changed between the request
        // and confirm steps (e.g. suspended in between), fail the same generic way rather than
        // let Account.ResetPassword's DomainException escape with a differently-shaped error.
        if (account is null || account.Status != AccountStatus.Approved)
            return Fail();

        var otpRecord = await repository.FindActiveOtpAsync(account.Id, OtpPurpose.PasswordReset, cancellationToken);
        if (otpRecord is null || otpRecord.IsExpired() || otpRecord.HasExceededAttempts())
            return Fail();

        otpRecord.RecordAttempt();

        if (!otpHasher.Verify(request.Otp, otpRecord.CodeHash))
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "[ConfirmPasswordReset] Invalid code for AccountId={AccountId}, Attempt={Attempt}",
                account.Id, otpRecord.AttemptCount);
            return Fail();
        }

        otpRecord.MarkUsed();
        account.ResetPassword(passwordHasher.Hash(request.NewPassword));

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[ConfirmPasswordReset] Password reset for AccountId={AccountId}", account.Id);

        return Result.Success(new ConfirmPasswordResetResponse(
            "Password reset successfully. You can now log in with your new password."));
    }

    private static Result<ConfirmPasswordResetResponse> Fail() =>
        Result.Failure<ConfirmPasswordResetResponse>(Error.Validation("otp", GenericError));
}
