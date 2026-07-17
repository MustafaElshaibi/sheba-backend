using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.LoginAdmin;

/// <summary>
/// Verifies admin credentials, then — for admins who have completed TOTP enrollment (T-SEC-1) —
/// a second factor. Ordering mirrors LoginCitizenHandler's anti-enumeration fix (password proven
/// before anything account-specific is disclosed); the MFA step only runs after that, so
/// revealing "a code is required" here never discloses more than the caller already proved by
/// supplying a correct password.
/// </summary>
public sealed class LoginAdminHandler(
    IIdentityRepository repository,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    IMfaSecretEncryptor mfaSecretEncryptor,
    ILogger<LoginAdminHandler> logger
) : IRequestHandler<LoginAdminCommand, Result<LoginAdminResponse>>
{
    private const string GenericCredentialError = "Invalid credentials.";
    private static string? _dummyHash;

    public async Task<Result<LoginAdminResponse>> Handle(LoginAdminCommand request, CancellationToken ct)
    {
        var admin = await repository.FindAdminByEmployeeIdAsync(request.EmployeeIdOrEmail, ct)
                    ?? await repository.FindAdminByEmailAsync(request.EmployeeIdOrEmail, ct);

        if (admin is null)
        {
            var dummy = _dummyHash ??= passwordHasher.Hash("sheba-admin-login-timing-equalizer");
            _ = passwordHasher.Verify(request.Password, dummy);
            logger.LogWarning("[LoginAdmin] Unknown admin identifier attempted login.");
            return Fail();
        }

        if (!passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            logger.LogWarning("[LoginAdmin] Invalid password for AdminId={AdminId}", admin.Id);
            return Fail();
        }

        if (admin.Status != "ACTIVE")
        {
            logger.LogWarning("[LoginAdmin] Login refused for non-active AdminId={AdminId} Status={Status}",
                admin.Id, admin.Status);
            return Fail();
        }

        if (admin.MfaEnabled)
        {
            var mfaFailure = await VerifyMfaAsync(admin, request.MfaCode, ct);
            if (mfaFailure is not null)
                return mfaFailure;
        }

        admin.RecordLogin();
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[LoginAdmin] AdminId={AdminId} authenticated.", admin.Id);

        return Result.Success(new LoginAdminResponse(
            AdminId: admin.Id,
            Role: admin.Role.ToString(),
            FullName: admin.FullName,
            Email: admin.Email,
            MinistryId: admin.MinistryId));
    }

    /// <summary>Returns null when the second factor checks out; otherwise the failure to return.</summary>
    private async Task<Result<LoginAdminResponse>?> VerifyMfaAsync(
        AdminUser admin, string? mfaCode, CancellationToken ct)
    {
        if (admin.IsMfaLocked())
        {
            logger.LogWarning("[LoginAdmin] MFA locked for AdminId={AdminId}", admin.Id);
            return FailMfa("mfa", "Too many invalid codes. Please try again later.");
        }

        if (string.IsNullOrWhiteSpace(mfaCode))
            return FailMfa("mfa_required", "A verification code is required.");

        var normalized = AdminRecoveryCode.Normalize(mfaCode);
        var isTotpShaped = normalized.Length == 6 && normalized.All(char.IsDigit);

        var verified = isTotpShaped
            ? totpService.VerifyCode(mfaSecretEncryptor.Decrypt(admin.MfaSecret!), normalized)
            : await TryConsumeRecoveryCodeAsync(admin.Id, normalized, ct);

        if (!verified)
        {
            admin.RecordFailedMfaAttempt();
            await repository.SaveChangesAsync(ct);
            logger.LogWarning("[LoginAdmin] Invalid MFA code for AdminId={AdminId}", admin.Id);
            return FailMfa("mfa", "Invalid verification code.");
        }

        admin.ResetMfaFailures();
        return null;
    }

    private async Task<bool> TryConsumeRecoveryCodeAsync(Guid adminId, string normalizedCode, CancellationToken ct)
    {
        var unused = await repository.GetUnusedAdminRecoveryCodesAsync(adminId, ct);
        var match = unused.FirstOrDefault(c => passwordHasher.Verify(normalizedCode, c.CodeHash));
        if (match is null)
            return false;

        match.MarkUsed();
        logger.LogWarning("[LoginAdmin] Recovery code consumed for AdminId={AdminId}", adminId);
        return true;
    }

    private static Result<LoginAdminResponse> Fail() =>
        Result.Failure<LoginAdminResponse>(Error.Validation("credentials", GenericCredentialError));

    private static Result<LoginAdminResponse> FailMfa(string code, string message) =>
        Result.Failure<LoginAdminResponse>(Error.Validation(code, message));
}
