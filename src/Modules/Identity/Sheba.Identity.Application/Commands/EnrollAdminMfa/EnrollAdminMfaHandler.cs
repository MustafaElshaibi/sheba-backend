using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.EnrollAdminMfa;

public sealed class EnrollAdminMfaHandler(
    IIdentityRepository repository,
    ITotpService totpService,
    IMfaSecretEncryptor mfaSecretEncryptor,
    ILogger<EnrollAdminMfaHandler> logger
) : IRequestHandler<EnrollAdminMfaCommand, Result<EnrollAdminMfaResponse>>
{
    public async Task<Result<EnrollAdminMfaResponse>> Handle(EnrollAdminMfaCommand request, CancellationToken ct)
    {
        var admin = await repository.FindAdminByIdAsync(request.AdminId, ct);
        if (admin is null)
            return Result.Failure<EnrollAdminMfaResponse>(Error.NotFound("resource", "Admin not found."));

        if (admin.MfaEnabled)
            return Result.Failure<EnrollAdminMfaResponse>(
                Error.Conflict("mfa", "MFA is already enabled on this account."));

        var secret = totpService.GenerateSecret();
        admin.SetMfaSecret(mfaSecretEncryptor.Encrypt(secret));
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[EnrollAdminMfa] MFA enrollment started for AdminId={AdminId}", admin.Id);

        return Result.Success(new EnrollAdminMfaResponse(
            Secret: secret,
            ProvisioningUri: totpService.BuildProvisioningUri(secret, admin.Email)));
    }
}
