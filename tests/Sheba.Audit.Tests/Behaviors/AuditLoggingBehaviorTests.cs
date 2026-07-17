using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sheba.Audit.Application.Interfaces;
using Sheba.Audit.Domain.Entities;
using Sheba.Audit.Infrastructure.Behaviors;

namespace Sheba.Audit.Tests.Behaviors;

// Top-level (not nested) so Castle DynamicProxy can build ILogger<AuditLoggingBehavior<T,_>>
// proxies against them — a private nested type isn't visible to the dynamically generated
// proxy assembly and NSubstitute.Substitute.For throws ArgumentException at test time.
public sealed record LoginCitizenCommand(string UsernameOrNid, string Password);

public sealed record TestQuery(Guid AccountId);

public class AuditLoggingBehaviorTests
{
    private static IHttpContextAccessor HttpContextAccessorWithSub(Guid actorId)
    {
        var identity = new ClaimsIdentity([new Claim("sub", actorId.ToString())]);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return accessor;
    }

    [Fact]
    public async Task Handle_LoginCitizenCommandSuccess_AuditRowHasActorFromSubAndNoPassword()
    {
        var actorId = Guid.NewGuid();
        var auditRepo = Substitute.For<IAuditRepository>();
        var behavior = new AuditLoggingBehavior<LoginCitizenCommand, string>(
            auditRepo, HttpContextAccessorWithSub(actorId), Substitute.For<ILogger<AuditLoggingBehavior<LoginCitizenCommand, string>>>());

        var command = new LoginCitizenCommand("01019001234", "hunter2-secret");

        await behavior.Handle(command, _ => Task.FromResult("ok"), CancellationToken.None);

        await auditRepo.Received(1).AddAsync(
            Arg.Is<AuditEvent>(e =>
                e.ActorId == actorId &&
                e.Succeeded &&
                e.RequestSnapshot != null &&
                !e.RequestSnapshot.Contains("hunter2-secret") &&
                !e.RequestSnapshot.Contains("01019001234")),
            Arg.Any<CancellationToken>());
        await auditRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Query_NeverWritesAuditRow()
    {
        var auditRepo = Substitute.For<IAuditRepository>();
        var behavior = new AuditLoggingBehavior<TestQuery, string>(
            auditRepo, HttpContextAccessorWithSub(Guid.NewGuid()), Substitute.For<ILogger<AuditLoggingBehavior<TestQuery, string>>>());

        await behavior.Handle(new TestQuery(Guid.NewGuid()), _ => Task.FromResult("ok"), CancellationToken.None);

        await auditRepo.DidNotReceive().AddAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_HandlerThrows_WritesFailureRowAndRethrows()
    {
        var actorId = Guid.NewGuid();
        var auditRepo = Substitute.For<IAuditRepository>();
        var behavior = new AuditLoggingBehavior<LoginCitizenCommand, string>(
            auditRepo, HttpContextAccessorWithSub(actorId), Substitute.For<ILogger<AuditLoggingBehavior<LoginCitizenCommand, string>>>());

        var command = new LoginCitizenCommand("01019001234", "hunter2-secret");

        Func<Task> act = () => behavior.Handle(
            command,
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        await auditRepo.Received(1).AddAsync(
            Arg.Is<AuditEvent>(e => e.ActorId == actorId && !e.Succeeded && e.ErrorMessage == "boom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoAuthenticatedUser_ActorIdIsEmptyGuid()
    {
        var auditRepo = Substitute.For<IAuditRepository>();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var behavior = new AuditLoggingBehavior<LoginCitizenCommand, string>(
            auditRepo, accessor, Substitute.For<ILogger<AuditLoggingBehavior<LoginCitizenCommand, string>>>());

        await behavior.Handle(new LoginCitizenCommand("x", "y"), _ => Task.FromResult("ok"), CancellationToken.None);

        await auditRepo.Received(1).AddAsync(
            Arg.Is<AuditEvent>(e => e.ActorId == Guid.Empty), Arg.Any<CancellationToken>());
    }
}
