using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.Commands.LoginCitizen;

/// <summary>
/// Handles login credential validation + OTP dispatch:
/// 1. Look up account by NID or username
/// 2. Verify account is Approved (return specific, safe messages for other statuses)
/// 3. Verify not locked (failed_login_count ≥ 5 → lock with exponential backoff)
/// 4. Verify password (Argon2id via IPasswordHasher)
/// 5. Invalidate old LOGIN OTPs, send fresh OTP
/// 6. Return masked phone so the frontend shows "Enter OTP sent to ***456"
/// </summary>
public sealed class LoginCitizenHandler(
    IIdentityRepository repository,
    IOtpProvider otpProvider,
    IPasswordHasher passwordHasher,
    IOtpHasher otpHasher,
    ILogger<LoginCitizenHandler> logger
) : IRequestHandler<LoginCitizenCommand, LoginCitizenResponse>
{
    private const int MaxFailedAttempts = 5;

    public async Task<LoginCitizenResponse> Handle(
        LoginCitizenCommand request,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Look up account ───────────────────────────────────────────
        var account = await repository.FindAccountByUsernameOrNidAsync(
            request.UsernameOrNid, cancellationToken);

        // Generic error — do not reveal whether the account exists
        if (account is null)
        {
            logger.LogWarning("[LoginCitizen] Account not found: {Input}", MaskInput(request.UsernameOrNid));
            return Fail("Invalid credentials. Please check your National ID/username and password.");
        }

        // ── Step 2: Status check ──────────────────────────────────────────────
        var statusMessage = account.Status switch
        {
            AccountStatus.PendingVerification       => "Your phone number is not yet verified. Please complete registration.",
            AccountStatus.PendingEmailVerification  => "Your email is not yet verified. Please click the link we sent.",
            AccountStatus.PendingAdminApproval      => "Your account is pending admin approval. You will be notified by email.",
            AccountStatus.Rejected                  => "Your account was not approved. Please contact support.",
            AccountStatus.Suspended                 => "Your account is suspended. Please contact support.",
            AccountStatus.Deactivated               => "This account has been deactivated.",
            AccountStatus.Approved                  => null, // OK to proceed
            _                                       => "Account status is invalid. Please contact support."
        };

        if (statusMessage is not null)
        {
            return Fail(statusMessage);
        }

        // ── Step 3: Lock check ────────────────────────────────────────────────
        if (account.IsLocked())
        {
            var lockedUntil = account.LockedUntil!.Value;
            return Fail($"Account is temporarily locked due to too many failed attempts. Try again after {lockedUntil:HH:mm UTC}.");
        }

        // ── Step 4: Password verification (Argon2id via IPasswordHasher) ────────
        bool passwordValid = passwordHasher.Verify(request.Password, account.PasswordHash);

        if (!passwordValid)
        {
            account.RecordFailedLogin();
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "[LoginCitizen] Invalid password for AccountId={AccountId}, FailedCount={Count}",
                account.Id, account.FailedLoginCount);

            return Fail("Invalid credentials. Please check your National ID/username and password.");
        }

        // ── Step 5: Invalidate old OTPs and send new one ──────────────────────
        await repository.InvalidatePreviousOtpsAsync(account.Id, OtpPurpose.Login, cancellationToken);

        var (otpResult, rawCode) = await otpProvider.SendAsync(
            destination: account.PhoneNumber,
            purpose:     OtpPurpose.Login,
            channel:     OtpChannel.Sms,
            cancellationToken: cancellationToken);

        if (!otpResult.Succeeded)
        {
            logger.LogError("[LoginCitizen] OTP send failed for AccountId={AccountId}: {Error}",
                account.Id, otpResult.ErrorMessage);
            return Fail("Could not send verification SMS. Please try again shortly.");
        }

        var otpRecord = Domain.Entities.OtpRecord.Create(
            accountId:  account.Id,
            purpose:    OtpPurpose.Login,
            channel:    OtpChannel.Sms,
            codeHash:   otpHasher.Hash(rawCode),
            ttlMinutes: 5);

        await repository.AddOtpRecordAsync(otpRecord, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[LoginCitizen] OTP dispatched for AccountId={AccountId}", account.Id);

        return new LoginCitizenResponse(
            OtpSent:    true,
            AccountId:  account.Id,
            MaskedPhone: MaskPhone(account.PhoneNumber));
    }

    private static LoginCitizenResponse Fail(string reason) =>
        new(OtpSent: false, AccountId: Guid.Empty, MaskedPhone: "", FailureReason: reason);

    private static string MaskInput(string? input) =>
        !string.IsNullOrEmpty(input) && input.Length > 3 ? input[..3] + "***" : "***";

    private static string MaskPhone(string phone) =>
        phone.Length < 7 ? "***" : phone[..^6] + "***" + phone[^3..];
}
