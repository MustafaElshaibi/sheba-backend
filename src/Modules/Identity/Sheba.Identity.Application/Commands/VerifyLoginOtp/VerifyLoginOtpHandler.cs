using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.VerifyLoginOtp;

/// <summary>
/// Validates the login OTP (login step 2):
/// 1. Load account; must be Approved and not locked
/// 2. Load active LOGIN OTP; check expiry, attempt count, hash
/// 3. On success: mark OTP used, reset failed-login counters, record last login
/// 4. Return the verified subject + claims for token issuance
/// </summary>
public sealed class VerifyLoginOtpHandler(
    IIdentityRepository repository,
    IOtpHasher otpHasher,
    ILogger<VerifyLoginOtpHandler> logger
) : IRequestHandler<VerifyLoginOtpCommand, Result<VerifyLoginOtpResponse>>
{
    private const int MaxAttempts = 3;

    public async Task<Result<VerifyLoginOtpResponse>> Handle(
        VerifyLoginOtpCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Fail("resource", "Account not found.");

        if (account.Status != AccountStatus.Approved)
            return Fail("otp", "Account is not active.");

        if (account.IsLocked())
            return Fail("otp", "Account is temporarily locked. Please try again later.");

        var otpRecord = await repository.FindActiveOtpAsync(
            request.AccountId, OtpPurpose.Login, cancellationToken);

        if (otpRecord is null)
            return Fail("otp", "No active login code found. Please start the login again.");

        if (otpRecord.IsExpired())
            return Fail("otp", "The login code has expired. Please start the login again.");

        if (otpRecord.AttemptCount >= MaxAttempts)
            return Fail("otp", "Too many attempts. Please start the login again.");

        otpRecord.RecordAttempt();

        if (!otpHasher.Verify(request.Otp, otpRecord.CodeHash))
        {
            await repository.SaveChangesAsync(cancellationToken);
            int remaining = MaxAttempts - otpRecord.AttemptCount;
            logger.LogWarning(
                "[VerifyLoginOtp] Invalid login OTP for AccountId={AccountId}, Attempt={Attempt}",
                request.AccountId, otpRecord.AttemptCount);
            return Fail("otp", remaining > 0
                ? $"Invalid code. {remaining} attempt(s) remaining."
                : "Too many invalid attempts. Please start the login again.");
        }

        // ── Success ──────────────────────────────────────────────────────────
        otpRecord.MarkUsed();
        account.RecordSuccessfulLogin();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[VerifyLoginOtp] Login OTP verified for AccountId={AccountId}", account.Id);

        return Result.Success(new VerifyLoginOtpResponse(
            AccountId:     account.Id,
            NationalId:    account.NationalId,
            Username:      account.Username,
            Email:         account.Email,
            FullNameEn:    account.FullNameEn,
            IdentityLevel: account.IdentityLevel));
    }

    private static Result<VerifyLoginOtpResponse> Fail(string code, string message) =>
        Result.Failure<VerifyLoginOtpResponse>(code == "resource"
            ? Error.NotFound(code, message)
            : Error.Validation(code, message));
}
