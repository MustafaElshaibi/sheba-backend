using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.RegisterCitizen;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// Anti-enumeration tests for registration (BR-ON-3). The security property under test is that
/// an attacker probing the registration endpoint cannot distinguish *why* verification failed —
/// "this NID isn't in the registry" and "this NID already has a Sheba account" must be reported
/// with byte-identical messages, or registration becomes an oracle for account/registry existence.
/// </summary>
public sealed class RegisterCitizenEnumerationTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly INationalIdProvider _nid = Substitute.For<INationalIdProvider>();
    private readonly IOtpProvider _otp = Substitute.For<IOtpProvider>();
    private readonly IOtpHasher _otpHasher = Substitute.For<IOtpHasher>();
    private readonly RegisterCitizenHandler _sut;

    public RegisterCitizenEnumerationTests()
        => _sut = new RegisterCitizenHandler(
            _repo, _nid, _otp, _otpHasher, NullLogger<RegisterCitizenHandler>.Instance);

    private static NationalIdLookupResult NotFound(string nid) =>
        new(false, nid, "", "", "", default, "", NidStatus.NotFound);

    private static NationalIdLookupResult Valid(string nid) =>
        new(true, nid, "مواطن", "Citizen", "0777000001", new DateOnly(1990, 1, 1), "M", NidStatus.Valid);

    private async Task<string> CaptureMessageAsync(RegisterCitizenCommand command)
    {
        var result = await _sut.Handle(command, default);
        result.IsFailure.Should().BeTrue();
        return result.Error!.Message;
    }

    [Fact]
    public async Task Handle_NidNotFoundInRegistry_ThrowsGenericError()
    {
        var command = new RegisterCitizenCommand("1000000000", "0777000000");
        _nid.LookupAsync(command.NationalId, command.PhoneNumber, default).Returns(NotFound(command.NationalId));

        var message = await CaptureMessageAsync(command);

        message.Should().Contain("could not verify your identity");
    }

    [Fact]
    public async Task Handle_NidAlreadyRegistered_ThrowsGenericError()
    {
        var command = new RegisterCitizenCommand("1000000001", "0777000001");
        _nid.LookupAsync(command.NationalId, command.PhoneNumber, default).Returns(Valid(command.NationalId));
        _repo.FindAccountByNidAsync(command.NationalId, default)
             .Returns(Account.CreateFromNidCheck(command.NationalId, "0777000001", "مواطن", "Citizen"));

        var message = await CaptureMessageAsync(command);

        // Must not hint at "already registered", "login", or "recovery".
        message.Should().Contain("could not verify your identity");
        message.ToLowerInvariant().Should().NotContain("already");
        message.ToLowerInvariant().Should().NotContain("recovery");
    }

    [Fact]
    public async Task Handle_NotFoundAndAlreadyRegistered_ProduceIdenticalMessages()
    {
        // Not found
        var cmd1 = new RegisterCitizenCommand("1000000000", "0777000000");
        _nid.LookupAsync(cmd1.NationalId, cmd1.PhoneNumber, default).Returns(NotFound(cmd1.NationalId));
        var notFoundMessage = await CaptureMessageAsync(cmd1);

        // Already registered
        var cmd2 = new RegisterCitizenCommand("1000000001", "0777000001");
        _nid.LookupAsync(cmd2.NationalId, cmd2.PhoneNumber, default).Returns(Valid(cmd2.NationalId));
        _repo.FindAccountByNidAsync(cmd2.NationalId, default)
             .Returns(Account.CreateFromNidCheck(cmd2.NationalId, "0777000001", "مواطن", "Citizen"));
        var alreadyRegisteredMessage = await CaptureMessageAsync(cmd2);

        // The core anti-enumeration property: the two failure branches are indistinguishable.
        alreadyRegisteredMessage.Should().Be(notFoundMessage);
    }
}
