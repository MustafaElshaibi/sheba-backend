using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.LoginAdmin;

/// <summary>
/// Verifies admin credentials. Ordering mirrors LoginCitizenHandler's anti-enumeration fix
/// (password proven before anything account-specific is disclosed) — the audience here is
/// internal, but the same probing risk applies to any password endpoint reachable over HTTP.
/// </summary>
public sealed class LoginAdminHandler(
    IIdentityRepository repository,
    IPasswordHasher passwordHasher,
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

        admin.RecordLogin();
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[LoginAdmin] AdminId={AdminId} authenticated.", admin.Id);

        return Result.Success(new LoginAdminResponse(
            AdminId: admin.Id,
            Role: admin.Role.ToString(),
            FullName: admin.FullName,
            Email: admin.Email));
    }

    private static Result<LoginAdminResponse> Fail() =>
        Result.Failure<LoginAdminResponse>(Error.Validation("credentials", GenericCredentialError));
}
