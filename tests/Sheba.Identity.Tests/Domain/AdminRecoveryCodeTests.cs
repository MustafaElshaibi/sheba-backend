using FluentAssertions;
using Sheba.Identity.Domain.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Domain;

public sealed class AdminRecoveryCodeTests
{
    [Theory]
    [InlineData("ab3d9-k7q2m", "AB3D9K7Q2M")]
    [InlineData("AB3D9K7Q2M", "AB3D9K7Q2M")]
    [InlineData(" ab3d9 k7q2m ", "AB3D9K7Q2M")]
    public void Normalize_StripsFormattingAndUppercases(string raw, string expected)
    {
        AdminRecoveryCode.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void MarkUsed_WhenUnused_SetsUsedAt()
    {
        var code = AdminRecoveryCode.Create(Guid.NewGuid(), "hash");

        code.MarkUsed();

        code.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_WhenAlreadyUsed_Throws()
    {
        var code = AdminRecoveryCode.Create(Guid.NewGuid(), "hash");
        code.MarkUsed();

        var act = () => code.MarkUsed();

        act.Should().Throw<DomainException>();
    }
}
