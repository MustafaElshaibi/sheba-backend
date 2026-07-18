using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.DeactivateAccount;
using Sheba.Identity.Application.Commands.ReinstateAccount;
using Sheba.Identity.Application.Commands.SuspendAccount;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Tests.Application.Commands;

public sealed class AccountLifecycleHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();

    private static Account ApprovedAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.CompleteRegistration("citizen1", "citizen1@example.com", "hash");
        account.MarkEmailVerified();
        account.Approve();
        return account;
    }

    [Fact]
    public async Task Suspend_UnknownAccount_ReturnsNotFound()
    {
        _repo.FindAccountByIdAsync(Arg.Any<Guid>(), default).Returns((Account?)null);
        var sut = new SuspendAccountHandler(_repo, NullLogger<SuspendAccountHandler>.Instance);

        var result = await sut.Handle(new SuspendAccountCommand(Guid.NewGuid(), "reason"), default);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Suspend_ApprovedAccount_Suspends_AndSaves()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        var sut = new SuspendAccountHandler(_repo, NullLogger<SuspendAccountHandler>.Instance);

        var result = await sut.Handle(new SuspendAccountCommand(account.Id, "Security hold"), default);

        result.IsSuccess.Should().BeTrue();
        account.Status.Should().Be(AccountStatus.Suspended);
        await _repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Suspend_NonApprovedAccount_ReturnsDomainFailure_AndDoesNotSave()
    {
        var account = Account.CreateFromNidCheck("1000000002", "0777000002", "مواطن", "Citizen");
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        var sut = new SuspendAccountHandler(_repo, NullLogger<SuspendAccountHandler>.Instance);

        var result = await sut.Handle(new SuspendAccountCommand(account.Id, null), default);

        result.IsFailure.Should().BeTrue();
        await _repo.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Reinstate_SuspendedAccount_ReturnsToApproved()
    {
        var account = ApprovedAccount();
        account.Suspend("reason");
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        var sut = new ReinstateAccountHandler(_repo, NullLogger<ReinstateAccountHandler>.Instance);

        var result = await sut.Handle(new ReinstateAccountCommand(account.Id), default);

        result.IsSuccess.Should().BeTrue();
        account.Status.Should().Be(AccountStatus.Approved);
    }

    [Fact]
    public async Task Deactivate_ApprovedAccount_Deactivates()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        var sut = new DeactivateAccountHandler(_repo, NullLogger<DeactivateAccountHandler>.Instance);

        var result = await sut.Handle(new DeactivateAccountCommand(account.Id, "Closed by admin"), default);

        result.IsSuccess.Should().BeTrue();
        account.Status.Should().Be(AccountStatus.Deactivated);
        account.DeactivationReason.Should().Be("Closed by admin");
    }
}
