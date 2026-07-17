using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.RotateRefreshTokenFamily;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// Core T-SEC-9 reuse-detection logic (RFC 9700): presenting the family's current generation
/// rotates it; presenting a stale one kills the whole family, not just that one request.
/// </summary>
public sealed class RotateRefreshTokenFamilyHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly RotateRefreshTokenFamilyHandler _sut;

    public RotateRefreshTokenFamilyHandlerTests()
        => _sut = new RotateRefreshTokenFamilyHandler(_repo, NullLogger<RotateRefreshTokenFamilyHandler>.Instance);

    private static RefreshTokenFamily MakeFamily() =>
        RefreshTokenFamily.Create(Guid.NewGuid(), "sheba-portal", Guid.NewGuid(), DateTime.UtcNow.AddDays(30));

    private void Seed(RefreshTokenFamily family) =>
        _repo.FindRefreshTokenFamilyByFamilyIdAsync(family.FamilyId, default).Returns(family);

    [Fact]
    public async Task Handle_UnknownFamilyId_DefersToOpenIddictAndReturnsPresentedGeneration()
    {
        _repo.FindRefreshTokenFamilyByFamilyIdAsync(Arg.Any<Guid>(), default).Returns((RefreshTokenFamily?)null);

        var result = await _sut.Handle(new RotateRefreshTokenFamilyCommand(Guid.NewGuid(), 3), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
    }

    [Fact]
    public async Task Handle_MatchingGeneration_RotatesAndReturnsIncrementedGeneration()
    {
        var family = MakeFamily(); // generation 0
        Seed(family);

        var result = await _sut.Handle(new RotateRefreshTokenFamilyCommand(family.FamilyId, 0), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        family.Generation.Should().Be(1);
        family.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StaleGeneration_RevokesWholeFamilyAndFails()
    {
        var family = MakeFamily();
        family.Rotate(); // legitimate refresh already happened — family is now at generation 1
        Seed(family);

        // Attacker replays the token from generation 0.
        var result = await _sut.Handle(new RotateRefreshTokenFamilyCommand(family.FamilyId, 0), default);

        result.IsFailure.Should().BeTrue();
        family.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AfterReuseDetected_EvenTheCurrentGenerationIsRejected()
    {
        var family = MakeFamily();
        family.Rotate(); // now at generation 1
        Seed(family);

        // Attacker replay triggers revocation.
        await _sut.Handle(new RotateRefreshTokenFamilyCommand(family.FamilyId, 0), default);

        // The legitimate holder of the CURRENT (generation 1) token tries next — also rejected,
        // because reuse anywhere in the family means the whole family is compromised.
        var legitimateRetry = await _sut.Handle(new RotateRefreshTokenFamilyCommand(family.FamilyId, 1), default);

        legitimateRetry.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyRevokedFamily_FailsWithoutComparingGeneration()
    {
        var family = MakeFamily();
        family.Revoke("prior incident");
        Seed(family);

        var result = await _sut.Handle(new RotateRefreshTokenFamilyCommand(family.FamilyId, 0), default);

        result.IsFailure.Should().BeTrue();
    }
}
