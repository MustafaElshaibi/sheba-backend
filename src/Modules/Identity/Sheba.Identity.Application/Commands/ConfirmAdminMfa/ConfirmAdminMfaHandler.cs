using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ConfirmAdminMfa;

public sealed class ConfirmAdminMfaHandler(
    IIdentityRepository repository,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    IMfaSecretEncryptor mfaSecretEncryptor,
    ILogger<ConfirmAdminMfaHandler> logger
) : IRequestHandler<ConfirmAdminMfaCommand, Result<ConfirmAdminMfaResponse>>
{
    private const int RecoveryCodeCount = 10;

    // Uppercase alphanumeric, excluding O/0/I/1 — avoids ambiguous glyphs when an admin
    // transcribes a code by hand from a printed/downloaded list.
    private const string RecoveryCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<Result<ConfirmAdminMfaResponse>> Handle(ConfirmAdminMfaCommand request, CancellationToken ct)
    {
        var admin = await repository.FindAdminByIdAsync(request.AdminId, ct);
        if (admin is null)
            return Result.Failure<ConfirmAdminMfaResponse>(Error.NotFound("resource", "Admin not found."));

        if (admin.MfaEnabled)
            return Result.Failure<ConfirmAdminMfaResponse>(
                Error.Conflict("mfa", "MFA is already enabled on this account."));

        if (admin.MfaSecret is null)
            return Result.Failure<ConfirmAdminMfaResponse>(
                Error.Validation("mfa", "No MFA enrollment is in progress. Call enroll first."));

        var secret = mfaSecretEncryptor.Decrypt(admin.MfaSecret);
        if (!totpService.VerifyCode(secret, request.TotpCode))
        {
            logger.LogWarning("[ConfirmAdminMfa] Invalid confirmation code for AdminId={AdminId}", admin.Id);
            return Result.Failure<ConfirmAdminMfaResponse>(Error.Validation("mfa", "Invalid verification code."));
        }

        admin.ConfirmMfaEnrollment();

        var rawCodes = GenerateRecoveryCodes();
        var codeEntities = rawCodes.Select(code =>
            AdminRecoveryCode.Create(admin.Id, passwordHasher.Hash(AdminRecoveryCode.Normalize(code))));

        await repository.AddAdminRecoveryCodesAsync(codeEntities, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[ConfirmAdminMfa] MFA enrollment confirmed for AdminId={AdminId}", admin.Id);

        return Result.Success(new ConfirmAdminMfaResponse(rawCodes));
    }

    private static IReadOnlyList<string> GenerateRecoveryCodes()
    {
        var codes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var raw = RandomNumberGenerator.GetString(RecoveryCodeAlphabet, 10);
            codes.Add($"{raw[..5]}-{raw[5..]}");
        }
        return codes;
    }
}
