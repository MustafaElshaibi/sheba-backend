using FluentAssertions;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Domain;

/// <summary>
/// AdminUser.Create's ministry-scoping invariant (T-AUTH-1): MinistryManager must have a
/// ministry; every other role must not.
/// </summary>
public sealed class AdminUserMinistryScopingTests
{
    [Fact]
    public void Create_MinistryManagerWithMinistryId_Succeeds()
    {
        var ministryId = Guid.NewGuid();

        var admin = AdminUser.Create(
            "MIN001", "manager@sheba.gov", "Ministry Manager", AdminRole.MinistryManager, "hash",
            ministryId: ministryId);

        admin.MinistryId.Should().Be(ministryId);
    }

    [Fact]
    public void Create_MinistryManagerWithoutMinistryId_Throws()
    {
        var act = () => AdminUser.Create(
            "MIN001", "manager@sheba.gov", "Ministry Manager", AdminRole.MinistryManager, "hash");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(AdminRole.SuperAdmin)]
    [InlineData(AdminRole.IdentityReviewer)]
    [InlineData(AdminRole.Auditor)]
    [InlineData(AdminRole.Support)]
    public void Create_NonMinistryManagerWithMinistryId_Throws(AdminRole role)
    {
        var act = () => AdminUser.Create(
            "ADM001", "admin@sheba.gov", "Some Admin", role, "hash", ministryId: Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_SuperAdminWithoutMinistryId_Succeeds()
    {
        var admin = AdminUser.Create(
            "ADM001", "admin@sheba.gov", "Super Admin", AdminRole.SuperAdmin, "hash");

        admin.MinistryId.Should().BeNull();
    }
}
