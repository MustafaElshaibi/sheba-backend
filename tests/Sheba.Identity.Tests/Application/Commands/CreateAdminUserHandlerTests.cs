using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.CreateAdminUser;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Infrastructure.Security;

namespace Sheba.Identity.Tests.Application.Commands;

public sealed class CreateAdminUserHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly CreateAdminUserHandler _sut;

    public CreateAdminUserHandlerTests()
        => _sut = new CreateAdminUserHandler(_repo, new Argon2idPasswordHasher(), NullLogger<CreateAdminUserHandler>.Instance);

    private static CreateAdminUserCommand MakeCommand(AdminRole role = AdminRole.Support, Guid? ministryId = null) =>
        new("EMP001", "new-admin@sheba.gov", "New Admin", role, "Correct-Horse-1", "Ops", ministryId);

    [Fact]
    public async Task Handle_ValidRequest_CreatesAdmin()
    {
        _repo.FindAdminByEmployeeIdAsync("EMP001", default).Returns((AdminUser?)null);
        _repo.FindAdminByEmailAsync("new-admin@sheba.gov", default).Returns((AdminUser?)null);

        var result = await _sut.Handle(MakeCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.EmployeeId.Should().Be("EMP001");
        await _repo.Received(1).AddAdminUserAsync(Arg.Any<AdminUser>(), default);
    }

    [Fact]
    public async Task Handle_DuplicateEmployeeId_ReturnsConflict()
    {
        _repo.FindAdminByEmployeeIdAsync("EMP001", default)
             .Returns(AdminUser.Create("EMP001", "existing@sheba.gov", "Existing", AdminRole.Support, "hash"));

        var result = await _sut.Handle(MakeCommand(), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("employeeId");
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsConflict()
    {
        _repo.FindAdminByEmployeeIdAsync("EMP001", default).Returns((AdminUser?)null);
        _repo.FindAdminByEmailAsync("new-admin@sheba.gov", default)
             .Returns(AdminUser.Create("EMP999", "new-admin@sheba.gov", "Existing", AdminRole.Support, "hash"));

        var result = await _sut.Handle(MakeCommand(), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("email");
    }

    [Fact]
    public async Task Handle_MinistryManagerWithoutMinistryId_ReturnsValidationFailure()
    {
        _repo.FindAdminByEmployeeIdAsync("EMP001", default).Returns((AdminUser?)null);
        _repo.FindAdminByEmailAsync("new-admin@sheba.gov", default).Returns((AdminUser?)null);

        var result = await _sut.Handle(MakeCommand(role: AdminRole.MinistryManager, ministryId: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ministryId");
    }

    [Fact]
    public async Task Handle_MinistryManagerWithMinistryId_Succeeds()
    {
        _repo.FindAdminByEmployeeIdAsync("EMP001", default).Returns((AdminUser?)null);
        _repo.FindAdminByEmailAsync("new-admin@sheba.gov", default).Returns((AdminUser?)null);
        var ministryId = Guid.NewGuid();

        var result = await _sut.Handle(MakeCommand(role: AdminRole.MinistryManager, ministryId: ministryId), default);

        result.IsSuccess.Should().BeTrue();
    }
}
