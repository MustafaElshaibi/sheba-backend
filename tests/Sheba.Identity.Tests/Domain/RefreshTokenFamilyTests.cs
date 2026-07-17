using FluentAssertions;
using Sheba.Identity.Domain.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Domain;

public sealed class RefreshTokenFamilyTests
{
    private static RefreshTokenFamily MakeFamily() =>
        RefreshTokenFamily.Create(
            Guid.NewGuid(), "sheba-portal", Guid.NewGuid(), DateTime.UtcNow.AddDays(30));

    [Fact]
    public void Create_StartsAtGenerationZeroAndNotRevoked()
    {
        var family = MakeFamily();

        family.Generation.Should().Be(0);
        family.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Rotate_AdvancesGeneration()
    {
        var family = MakeFamily();

        family.Rotate();
        family.Rotate();

        family.Generation.Should().Be(2);
    }

    [Fact]
    public void Rotate_WhenRevoked_Throws()
    {
        var family = MakeFamily();
        family.Revoke("test");

        var act = () => family.Rotate();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Revoke_SetsRevokedAtAndReason()
    {
        var family = MakeFamily();

        family.Revoke("stale generation presented");

        family.IsRevoked.Should().BeTrue();
        family.RevocationReason.Should().Be("stale generation presented");
    }

    [Fact]
    public void Revoke_CalledTwice_IsIdempotent()
    {
        var family = MakeFamily();
        family.Revoke("first reason");

        var act = () => family.Revoke("second reason");

        act.Should().NotThrow();
        family.RevocationReason.Should().Be("first reason"); // first revocation wins
    }

    [Fact]
    public void Revoke_ReasonLongerThanColumnWidth_TruncatesInsteadOfFailingLater()
    {
        // The revocation_reason column is varchar(50) (MiscConfigurations). Found live: this
        // exact call site ("Refresh token reuse detected: stale generation presented." — 57
        // chars) overflowed it and threw a DbUpdateException on the reuse-detection path itself,
        // meaning a real replay attempt left the family NOT revoked instead of revoked.
        var family = MakeFamily();
        var oversized = new string('x', 80);

        family.Revoke(oversized);

        family.RevocationReason.Should().HaveLength(50);
        family.RevocationReason.Should().Be(new string('x', 50));
    }
}
