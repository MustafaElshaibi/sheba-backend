using FluentAssertions;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Domain;

public sealed class AccountResetPasswordTests
{
    private static Account ApprovedAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.CompleteRegistration("citizen1", "citizen1@example.com", "old-hash");
        account.MarkEmailVerified();
        account.Approve();
        return account;
    }

    [Fact]
    public void ResetPassword_ApprovedAccount_SetsNewHash()
    {
        var account = ApprovedAccount();

        account.ResetPassword("new-hash");

        account.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public void ResetPassword_ApprovedAccount_ClearsLockoutState()
    {
        var account = ApprovedAccount();
        for (var i = 0; i < 5; i++) account.RecordFailedLogin(); // locks the account
        account.IsLocked().Should().BeTrue();

        account.ResetPassword("new-hash");

        account.FailedLoginCount.Should().Be(0);
        account.IsLocked().Should().BeFalse();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.PendingEmailVerification)]
    [InlineData(AccountStatus.PendingAdminApproval)]
    [InlineData(AccountStatus.Rejected)]
    public void ResetPassword_NonApprovedAccount_Throws(AccountStatus status)
    {
        // Only Approved accounts can log in at all (BR-ON-10), so resetting a password on any
        // other status account is never a legitimate recovery step.
        var account = AccountAt(status);

        var act = () => account.ResetPassword("new-hash");

        act.Should().Throw<DomainException>();
    }

    private static Account AccountAt(AccountStatus status)
    {
        var account = Account.CreateFromNidCheck("1000000002", "0777000002", "مواطن", "Citizen");
        if (status == AccountStatus.PendingVerification) return account;

        account.CompleteRegistration("citizen2", "citizen2@example.com", "old-hash");
        if (status == AccountStatus.PendingEmailVerification) return account;

        account.MarkEmailVerified();
        if (status == AccountStatus.PendingAdminApproval) return account;

        if (status == AccountStatus.Rejected)
        {
            account.Reject("test rejection");
            return account;
        }

        throw new ArgumentOutOfRangeException(nameof(status), status, "Unhandled status in test helper.");
    }
}
