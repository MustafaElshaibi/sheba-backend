using FluentAssertions;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Domain;

public sealed class AccountLifecycleTests
{
    private static Account ApprovedAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.CompleteRegistration("citizen1", "citizen1@example.com", "hash");
        account.MarkEmailVerified();
        account.Approve();
        return account;
    }

    // ── Suspend ──────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_ApprovedAccount_MovesToSuspended_AndStoresReason()
    {
        var account = ApprovedAccount();

        account.Suspend("Suspicious activity");

        account.Status.Should().Be(AccountStatus.Suspended);
        account.SuspensionReason.Should().Be("Suspicious activity");
    }

    [Fact]
    public void Suspend_ApprovedAccount_RaisesAccountSuspendedEvent()
    {
        var account = ApprovedAccount();

        account.Suspend("reason");

        account.DomainEvents.Should().ContainSingle(e => e is AccountSuspendedEvent);
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deactivated)]
    public void Suspend_NonApprovedAccount_Throws(AccountStatus status)
    {
        var account = AccountAt(status);

        var act = () => account.Suspend();

        act.Should().Throw<DomainException>();
    }

    // ── Reinstate ────────────────────────────────────────────────────────────

    [Fact]
    public void Reinstate_SuspendedAccount_MovesBackToApproved_AndClearsReason()
    {
        var account = ApprovedAccount();
        account.Suspend("reason");

        account.Reinstate();

        account.Status.Should().Be(AccountStatus.Approved);
        account.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void Reinstate_NotSuspended_Throws()
    {
        var account = ApprovedAccount();

        var act = () => account.Reinstate();

        act.Should().Throw<DomainException>();
    }

    // ── Deactivate ───────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ApprovedAccount_MovesToDeactivated_AndStoresReason()
    {
        var account = ApprovedAccount();

        account.Deactivate("Citizen requested closure");

        account.Status.Should().Be(AccountStatus.Deactivated);
        account.DeactivationReason.Should().Be("Citizen requested closure");
    }

    [Fact]
    public void Deactivate_SuspendedAccount_Throws()
    {
        // Only Approved -> Deactivated is a valid §6.2 transition; a suspended account must be
        // reinstated first.
        var account = ApprovedAccount();
        account.Suspend();

        var act = () => account.Deactivate();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deactivate_IsTerminal_NoTransitionOut()
    {
        var account = ApprovedAccount();
        account.Deactivate();

        var suspendAttempt = () => account.Suspend();
        var reinstateAttempt = () => account.Reinstate();

        suspendAttempt.Should().Throw<DomainException>();
        reinstateAttempt.Should().Throw<DomainException>();
    }

    // ── Reject reason persistence ────────────────────────────────────────────

    [Fact]
    public void Reject_PendingAdminApprovalAccount_StoresRejectionReason()
    {
        var account = Account.CreateFromNidCheck("1000000002", "0777000002", "مواطن", "Citizen");
        account.CompleteRegistration("citizen2", "citizen2@example.com", "hash");
        account.MarkEmailVerified();

        account.Reject("Document mismatch");

        account.Status.Should().Be(AccountStatus.Rejected);
        account.RejectionReason.Should().Be("Document mismatch");
    }

    // ── Re-application ───────────────────────────────────────────────────────

    [Fact]
    public void ReApply_RejectedAccount_MovesToPendingVerification_AndClearsRejectionReason()
    {
        var account = Account.CreateFromNidCheck("1000000003", "0777000003", "مواطن", "Citizen");
        account.CompleteRegistration("citizen3", "citizen3@example.com", "hash");
        account.MarkEmailVerified();
        account.Reject("Document mismatch");

        account.ReApply();

        account.Status.Should().Be(AccountStatus.PendingVerification);
        account.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void ReApply_NotRejected_Throws()
    {
        var account = ApprovedAccount();

        var act = () => account.ReApply();

        act.Should().Throw<DomainException>();
    }

    private static Account AccountAt(AccountStatus status)
    {
        var account = Account.CreateFromNidCheck("1000000009", "0777000009", "مواطن", "Citizen");
        if (status == AccountStatus.PendingVerification) return account;

        account.CompleteRegistration("citizen9", "citizen9@example.com", "hash");
        account.MarkEmailVerified();
        account.Approve();
        if (status == AccountStatus.Suspended)
        {
            account.Suspend();
            return account;
        }
        if (status == AccountStatus.Deactivated)
        {
            account.Deactivate();
            return account;
        }

        return account;
    }
}
