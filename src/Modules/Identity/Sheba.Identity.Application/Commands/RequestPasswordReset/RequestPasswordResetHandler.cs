using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RequestPasswordReset;

/// <summary>
/// Sends a password-reset OTP to the registered phone of an approved account, if the supplied
/// identifier matches one. BR-ON-3: the response never reveals whether it did.
/// </summary>
public sealed class RequestPasswordResetHandler(
    IIdentityRepository repository,
    IOtpProvider otpProvider,
    IOtpHasher otpHasher,
    IOtpCodeGenerator otpCodeGenerator,
    ILogger<RequestPasswordResetHandler> logger
) : IRequestHandler<RequestPasswordResetCommand, Result<RequestPasswordResetResponse>>
{
    private const string GenericMessage =
        "If an account matches this identifier, a password reset code has been sent to its registered phone number.";

    public async Task<Result<RequestPasswordResetResponse>> Handle(
        RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByUsernameOrNidAsync(request.UsernameOrNid, cancellationToken);

        // Only an Approved account can reset a password (BR-ON-10: no other status can log in
        // anyway) — but whether we found nothing, found a non-Approved account, or found and
        // dispatched a code, the caller always sees the same message.
        if (account is not null && account.Status == AccountStatus.Approved)
        {
            await SendResetOtpAsync(account, cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "[RequestPasswordReset] No eligible account for identifier (generic response returned).");
        }

        return Result.Success(new RequestPasswordResetResponse(GenericMessage));
    }

    private async Task SendResetOtpAsync(Account account, CancellationToken cancellationToken)
    {
        await repository.InvalidatePreviousOtpsAsync(account.Id, OtpPurpose.PasswordReset, cancellationToken);

        var rawCode = otpCodeGenerator.GenerateNumericCode();
        var otpResult = await otpProvider.SendAsync(
            destination: account.PhoneNumber,
            code:        rawCode,
            purpose:     OtpPurpose.PasswordReset,
            channel:     OtpChannel.Sms,
            cancellationToken: cancellationToken);

        if (!otpResult.Succeeded)
        {
            // Logged internally only — the caller still gets the generic success message above,
            // same anti-enumeration/fail-quiet posture as every other identifier-based lookup.
            logger.LogError(
                "[RequestPasswordReset] OTP send failed for AccountId={AccountId}: {Error}",
                account.Id, otpResult.ErrorMessage);
            return;
        }

        var otpRecord = OtpRecord.Create(
            accountId:  account.Id,
            purpose:    OtpPurpose.PasswordReset,
            channel:    OtpChannel.Sms,
            codeHash:   otpHasher.Hash(rawCode),
            ttlMinutes: 5);

        await repository.AddOtpRecordAsync(otpRecord, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[RequestPasswordReset] Reset OTP dispatched for AccountId={AccountId}", account.Id);
    }
}
