using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.RequestPasswordReset;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// BR-ON-3 anti-enumeration: the response must be identical whether the identifier matches an
/// account or not, and whether that account is even Approved — only the internal side effect
/// (an OTP dispatched) differs.
/// </summary>
public sealed class RequestPasswordResetHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IOtpProvider _otp = Substitute.For<IOtpProvider>();
    private readonly IOtpHasher _otpHasher = new Argon2idOtpHasher();
    private readonly IOtpCodeGenerator _otpCodeGenerator = new CryptoOtpCodeGenerator();
    private readonly RequestPasswordResetHandler _sut;

    public RequestPasswordResetHandlerTests()
    {
        _sut = new RequestPasswordResetHandler(
            _repo, _otp, _otpHasher, _otpCodeGenerator, NullLogger<RequestPasswordResetHandler>.Instance);

        _otp.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OtpSendResult(true)));
    }

    private static Account ApprovedAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.CompleteRegistration("citizen1", "citizen1@example.com", "hash");
        account.MarkEmailVerified();
        account.Approve();
        return account;
    }

    [Fact]
    public async Task Handle_UnknownIdentifier_ReturnsGenericSuccess_AndSendsNoOtp()
    {
        _repo.FindAccountByUsernameOrNidAsync("ghost", default).Returns((Account?)null);

        var result = await _sut.Handle(new RequestPasswordResetCommand("ghost"), default);

        result.IsSuccess.Should().BeTrue();
        await _otp.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonApprovedAccount_ReturnsGenericSuccess_AndSendsNoOtp()
    {
        var account = Account.CreateFromNidCheck("1000000002", "0777000002", "مواطن", "Citizen");
        _repo.FindAccountByUsernameOrNidAsync("citizen2", default).Returns(account);

        var result = await _sut.Handle(new RequestPasswordResetCommand("citizen2"), default);

        result.IsSuccess.Should().BeTrue();
        await _otp.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApprovedAccount_ReturnsTheSameGenericMessage_ButSendsOtpToRegisteredPhone()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);

        var unknownResult = await _sut.Handle(new RequestPasswordResetCommand("ghost-identifier"), default);
        var knownResult = await _sut.Handle(new RequestPasswordResetCommand("citizen1"), default);

        knownResult.Value.Message.Should().Be(unknownResult.Value.Message);
        await _otp.Received(1).SendAsync(
            account.PhoneNumber, Arg.Any<string>(), OtpPurpose.PasswordReset, OtpChannel.Sms, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApprovedAccount_InvalidatesPreviousResetOtps_BeforeIssuingANewOne()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);

        await _sut.Handle(new RequestPasswordResetCommand("citizen1"), default);

        await _repo.Received(1).InvalidatePreviousOtpsAsync(account.Id, OtpPurpose.PasswordReset, Arg.Any<CancellationToken>());
    }
}
