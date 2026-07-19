using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Payment.Application.Commands.ConfirmPayment;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Enums;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Tests.Application;

public sealed class ConfirmPaymentHandlerTests
{
    private readonly IPaymentRepository _repo = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();

    private ConfirmPaymentHandler Build() => new(_repo, _gateway, NullLogger<ConfirmPaymentHandler>.Instance);

    private static PaymentOrder OrderFor(Guid citizenId, decimal amount = 500m) =>
        PaymentOrder.Create(Guid.NewGuid(), citizenId, amount, "YER");

    [Fact]
    public async Task Handle_NonOwner_ThrowsNotFound_NotForbidden()
    {
        var order = OrderFor(Guid.NewGuid());
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var sut = Build();
        var act = () => sut.Handle(new ConfirmPaymentCommand(order.Id, Guid.NewGuid(), IsAdmin: false), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Owner_GatewaySucceeds_MarksPaidAndReturnsCompletedDto()
    {
        var citizenId = Guid.NewGuid();
        var order = OrderFor(citizenId, 750m);
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _gateway.ChargeAsync(order.Id, 750m, "YER", Arg.Any<CancellationToken>())
            .Returns(new GatewayChargeResult(true, "GW-REF", "ok"));

        var sut = Build();
        var dto = await sut.Handle(new ConfirmPaymentCommand(order.Id, citizenId, IsAdmin: false), default);

        dto.Status.Should().Be(PaymentStatus.Completed.ToString());
        dto.GatewayReference.Should().Be("GW-REF");
        await _repo.Received(1).AddTransactionAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_IsIdempotent_DoesNotCallGatewayAgain()
    {
        var citizenId = Guid.NewGuid();
        var order = OrderFor(citizenId);
        order.MarkPaid("EXISTING-REF");
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var sut = Build();
        var dto = await sut.Handle(new ConfirmPaymentCommand(order.Id, citizenId, IsAdmin: false), default);

        dto.GatewayReference.Should().Be("EXISTING-REF");
        await _gateway.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GatewayDeclines_MarksFailedAndThrowsDomainException()
    {
        var citizenId = Guid.NewGuid();
        var order = OrderFor(citizenId);
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _gateway.ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayChargeResult(false, null, "declined"));

        var sut = Build();
        var act = () => sut.Handle(new ConfirmPaymentCommand(order.Id, citizenId, IsAdmin: false), default);

        await act.Should().ThrowAsync<DomainException>();
        order.Status.Should().Be(PaymentStatus.Failed);
    }
}
