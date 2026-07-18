using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.RegisterCitizen;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// sheba.md §6.2: Rejected → PendingVerification re-application. A Rejected citizen submitting
/// RegisterCitizen again with the same NID must succeed (not hit the already-registered guard),
/// reusing the existing Account row rather than creating a duplicate.
/// </summary>
public sealed class RegisterCitizenReApplicationTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly INationalIdProvider _nid = Substitute.For<INationalIdProvider>();
    private readonly IOtpProvider _otp = Substitute.For<IOtpProvider>();
    private readonly IOtpHasher _otpHasher = Substitute.For<IOtpHasher>();
    private readonly IOtpCodeGenerator _otpCodeGenerator = Substitute.For<IOtpCodeGenerator>();
    private readonly RegisterCitizenHandler _sut;

    public RegisterCitizenReApplicationTests()
    {
        _sut = new RegisterCitizenHandler(
            _repo, _nid, _otp, _otpHasher, _otpCodeGenerator, NullLogger<RegisterCitizenHandler>.Instance);
        _otpCodeGenerator.GenerateNumericCode(Arg.Any<int>()).Returns("123456");
        _otpHasher.Hash(Arg.Any<string>()).Returns("hashed");
        _otp.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OtpSendResult(true)));
    }

    private static Account RejectedAccount(string nid)
    {
        var account = Account.CreateFromNidCheck(nid, "0777000001", "مواطن", "Citizen");
        account.CompleteRegistration("citizen1", "citizen1@example.com", "hash");
        account.MarkEmailVerified();
        account.Reject("Document mismatch");
        return account;
    }

    [Fact]
    public async Task Handle_RejectedAccountReApplying_Succeeds()
    {
        var command = new RegisterCitizenCommand("1000000001", "0777000001");
        _nid.LookupAsync(command.NationalId, command.PhoneNumber, default)
            .Returns(new NationalIdLookupResult(true, command.NationalId, "مواطن", "Citizen", "0777000001",
                new DateOnly(1990, 1, 1), "M", NidStatus.Valid));
        var existing = RejectedAccount(command.NationalId);
        _repo.FindAccountByNidAsync(command.NationalId, default).Returns(existing);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be(existing.Id);
        existing.Status.Should().Be(AccountStatus.PendingVerification);
    }

    [Fact]
    public async Task Handle_RejectedAccountReApplying_DoesNotCallAddAccount()
    {
        // The existing row is reused, not re-inserted — AddAccountAsync would throw a duplicate
        // key error against the already-tracked/existing row.
        var command = new RegisterCitizenCommand("1000000001", "0777000001");
        _nid.LookupAsync(command.NationalId, command.PhoneNumber, default)
            .Returns(new NationalIdLookupResult(true, command.NationalId, "مواطن", "Citizen", "0777000001",
                new DateOnly(1990, 1, 1), "M", NidStatus.Valid));
        var existing = RejectedAccount(command.NationalId);
        _repo.FindAccountByNidAsync(command.NationalId, default).Returns(existing);

        await _sut.Handle(command, default);

        await _repo.DidNotReceive().AddAccountAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).AddIdentityRequestAsync(Arg.Any<IdentityRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonRejectedExistingAccount_StillReturnsGenericFailure()
    {
        var command = new RegisterCitizenCommand("1000000002", "0777000002");
        _nid.LookupAsync(command.NationalId, command.PhoneNumber, default)
            .Returns(new NationalIdLookupResult(true, command.NationalId, "مواطن", "Citizen", "0777000002",
                new DateOnly(1990, 1, 1), "M", NidStatus.Valid));
        _repo.FindAccountByNidAsync(command.NationalId, default)
             .Returns(Account.CreateFromNidCheck(command.NationalId, "0777000002", "مواطن", "Citizen"));

        var result = await _sut.Handle(command, default);

        result.IsFailure.Should().BeTrue();
    }
}
