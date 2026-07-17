using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.VerifyOtp;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// Unit tests for VerifyOtpHandler.
/// Uses NSubstitute to mock IIdentityRepository and the real Argon2idOtpHasher
/// so hash/verify round-trips exactly as in production.
/// No EF Core or infrastructure DB dependencies — pure unit tests.
/// </summary>
public sealed class VerifyOtpHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IOtpHasher          _otpHasher = new Argon2idOtpHasher();
    private readonly VerifyOtpHandler    _sut;

    public VerifyOtpHandlerTests()
        => _sut = new VerifyOtpHandler(_repo, _otpHasher, NullLogger<VerifyOtpHandler>.Instance);

    // ── helpers ──────────────────────────────────────────────────────────────

    private string HashOtp(string raw) => _otpHasher.Hash(raw);

    private static Account MakePendingAccount()
    {
        // CreateFromNidCheck → status = PendingVerification
        var acc = Account.CreateFromNidCheck("12345678901234", "+201001234567", "مواطن", "Citizen");
        return acc;
    }

    private OtpRecord MakeOtpRecord(Guid accountId, string rawCode, bool expired = false)
    {
        var ttl = expired ? -1 : 10;
        var rec = OtpRecord.Create(accountId, OtpPurpose.Registration, OtpChannel.Sms, HashOtp(rawCode), ttl);
        return rec;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCode_ReturnsSuccess()
    {
        // Arrange
        var account    = MakePendingAccount();
        const string raw = "123456";
        var otp        = MakeOtpRecord(account.Id, raw);

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.Registration, default).Returns(otp);

        var command = new VerifyOtpCommand(account.Id, raw);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WrongCode_ReturnsFailure()
    {
        // Arrange
        var account = MakePendingAccount();
        var otp     = MakeOtpRecord(account.Id, "999999");

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.Registration, default).Returns(otp);

        var command = new VerifyOtpCommand(account.Id, "000000"); // wrong code

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Invalid code");
    }

    [Fact]
    public async Task Handle_ExpiredOtp_ReturnsExpiredMessage()
    {
        // Arrange
        var account = MakePendingAccount();
        var otp     = MakeOtpRecord(account.Id, "123456", expired: true);

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.Registration, default).Returns(otp);

        var command = new VerifyOtpCommand(account.Id, "123456");

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_NoActiveOtp_ReturnsNoOtpMessage()
    {
        // Arrange
        var account = MakePendingAccount();

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.Registration, default).Returns((OtpRecord?)null);

        var command = new VerifyOtpCommand(account.Id, "123456");

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("No active OTP");
    }
}
