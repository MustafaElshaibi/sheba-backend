using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.ApproveIdentityRequest;
using Sheba.Identity.Application.Commands.RejectIdentityRequest;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// Unit tests for ApproveIdentityRequestHandler and RejectIdentityRequestHandler.
/// Verifies that:
///   - Status transitions are correct
///   - Domain events are raised (and will be dispatched by IdentityRepository)
///   - Invalid state transitions throw DomainException
/// </summary>
public sealed class IdentityRequestDecisionHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();

    private static Account MakeAccount()
    {
        // CreateFromNidCheck → PendingVerification
        var acc = Account.CreateFromNidCheck("12345678901234", "+201001234567", "مواطن", "Citizen");
        // Advance to PendingAdminApproval so that Approve/Reject guards are satisfied
        acc.SetCredentials("citizen01", "citizen@sheba.test", "argon2id-hash-placeholder");
        return acc;
    }

    private static IdentityRequest MakeRequest(Guid accountId, RequestStatus status = RequestStatus.UnderReview)
    {
        var req = IdentityRequest.Submit(accountId, RequestType.OpenAccount, new { });
        if (status == RequestStatus.UnderReview)
            req.MarkUnderReview();
        return req;
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_UnderReviewRequest_SetsApprovedStatus()
    {
        // Arrange
        var account = MakeAccount();
        var request = MakeRequest(account.Id);

        _repo.FindRequestByIdAsync(request.Id, default).Returns(request);
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);

        var adminId = Guid.NewGuid();
        var command = new ApproveIdentityRequestCommand(request.Id, adminId, "Looks good");
        var handler = new ApproveIdentityRequestHandler(_repo, NullLogger<ApproveIdentityRequestHandler>.Instance);

        // Act
        var result = await handler.Handle(command, default);

        // Assert
        result.RequestId.Should().Be(request.Id);
        request.Status.Should().Be(RequestStatus.Approved);
        request.DomainEvents.Should().ContainSingle(e => e.GetType().Name.Contains("Decided"));
        await _repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Approve_AlreadyApprovedRequest_ThrowsDomainException()
    {
        // Arrange
        var account = MakeAccount();
        var request = MakeRequest(account.Id);
        request.Approve(Guid.NewGuid()); // pre-approve

        _repo.FindRequestByIdAsync(request.Id, default).Returns(request);
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);

        var command = new ApproveIdentityRequestCommand(request.Id, Guid.NewGuid(), null);
        var handler = new ApproveIdentityRequestHandler(_repo, NullLogger<ApproveIdentityRequestHandler>.Instance);

        // Act
        Func<Task> act = () => handler.Handle(command, default);

        // Assert — should throw because status is already Approved
        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_UnderReviewRequest_SetsRejectedStatus()
    {
        // Arrange
        var account = MakeAccount();
        var request = MakeRequest(account.Id);

        _repo.FindRequestByIdAsync(request.Id, default).Returns(request);
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);

        var adminId = Guid.NewGuid();
        var command = new RejectIdentityRequestCommand(request.Id, adminId, "Documents unclear", null);
        var handler = new RejectIdentityRequestHandler(_repo, NullLogger<RejectIdentityRequestHandler>.Instance);

        // Act
        var result = await handler.Handle(command, default);

        // Assert
        result.RequestId.Should().Be(request.Id);
        request.Status.Should().Be(RequestStatus.Rejected);
        request.RejectionReason.Should().Be("Documents unclear");
        request.DomainEvents.Should().ContainSingle(e => e.GetType().Name.Contains("Decided"));
        await _repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Reject_WithoutReason_ThrowsDomainException()
    {
        // Arrange
        var account = MakeAccount();
        var request = MakeRequest(account.Id);

        _repo.FindRequestByIdAsync(request.Id, default).Returns(request);
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);

        var command = new RejectIdentityRequestCommand(request.Id, Guid.NewGuid(), "", null); // empty reason
        var handler = new RejectIdentityRequestHandler(_repo, NullLogger<RejectIdentityRequestHandler>.Instance);

        // Act
        Func<Task> act = () => handler.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
                          .WithMessage("*Rejection reason*");
    }
}
