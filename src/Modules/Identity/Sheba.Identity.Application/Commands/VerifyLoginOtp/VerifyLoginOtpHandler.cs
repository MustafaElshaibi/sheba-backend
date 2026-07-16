using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

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
) : IRequestHandler<VerifyLoginOtpCommand, VerifyLoginOtpResponse>
{
    private const int MaxAttempts = 3;

    public async Task<VerifyLoginOtpResponse> Handle(
        VerifyLoginOtpCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Account), request.AccountId);

        if (account.Status != AccountStatus.Approved)
            return Fail(account.Id, "Account is not active.");

        if (account.IsLocked())
            return Fail(account.Id, "Account is temporarily locked. Please try again later.");

        var otpRecord = await repository.FindActiveOtpAsync(
            request.AccountId, OtpPurpose.Login, cancellationToken);

        if (otpRecord is null)
            return Fail(account.Id, "No active login code found. Please start the login again.");

        if (otpRecord.IsExpired())
            return Fail(account.Id, "The login code has expired. Please start the login again.");

        if (otpRecord.AttemptCount >= MaxAttempts)
            return Fail(account.Id, "Too many attempts. Please start the login again.");

        otpRecord.RecordAttempt();

        if (!otpHasher.Verify(request.Otp, otpRecord.CodeHash))
        {
            await repository.SaveChangesAsync(cancellationToken);
            int remaining = MaxAttempts - otpRecord.AttemptCount;
            logger.LogWarning(
                "[VerifyLoginOtp] Invalid login OTP for AccountId={AccountId}, Attempt={Attempt}",
                request.AccountId, otpRecord.AttemptCount);
            return Fail(account.Id, remaining > 0
                ? $"Invalid code. {remaining} attempt(s) remaining."
                : "Too many invalid attempts. Please start the login again.");
        }

        // ── Success ──────────────────────────────────────────────────────────
        otpRecord.MarkUsed();
        account.RecordSuccessfulLogin();
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[VerifyLoginOtp] Login OTP verified for AccountId={AccountId}", account.Id);

        return new VerifyLoginOtpResponse(
            Succeeded:     true,
            AccountId:     account.Id,
            NationalId:    account.NationalId,
            Username:      account.Username,
            Email:         account.Email,
            FullNameEn:    account.FullNameEn,
            IdentityLevel: account.IdentityLevel,
            Message:       "Login successful.");
    }

    private static VerifyLoginOtpResponse Fail(Guid accountId, string message) =>
        new(false, accountId, null, null, null, null, 0, message);
}
