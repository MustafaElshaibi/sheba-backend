using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.CreateAdminUser;

public sealed class CreateAdminUserHandler(
    IIdentityRepository repository,
    IPasswordHasher passwordHasher,
    ILogger<CreateAdminUserHandler> logger
) : IRequestHandler<CreateAdminUserCommand, Result<CreateAdminUserResponse>>
{
    public async Task<Result<CreateAdminUserResponse>> Handle(CreateAdminUserCommand request, CancellationToken ct)
    {
        if (await repository.FindAdminByEmployeeIdAsync(request.EmployeeId, ct) is not null)
            return Result.Failure<CreateAdminUserResponse>(
                Error.Conflict("employeeId", "An admin with this employee ID already exists."));

        if (await repository.FindAdminByEmailAsync(request.Email, ct) is not null)
            return Result.Failure<CreateAdminUserResponse>(
                Error.Conflict("email", "An admin with this email already exists."));

        AdminUser admin;
        try
        {
            admin = AdminUser.Create(
                request.EmployeeId, request.Email, request.FullName, request.Role,
                passwordHasher.Hash(request.Password), request.Department, request.MinistryId);
        }
        catch (DomainException ex)
        {
            // MinistryManager-without-ministry / ministry-on-a-non-scoped-role — a caller input
            // problem, not a server error, so it renders as a 400 fail like any other bad request.
            return Result.Failure<CreateAdminUserResponse>(Error.Validation("ministryId", ex.Message));
        }

        await repository.AddAdminUserAsync(admin, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation(
            "[CreateAdminUser] Created AdminId={AdminId} Role={Role} MinistryId={MinistryId}",
            admin.Id, admin.Role, admin.MinistryId);

        return Result.Success(new CreateAdminUserResponse(admin.Id, admin.EmployeeId, admin.Role.ToString()));
    }
}
