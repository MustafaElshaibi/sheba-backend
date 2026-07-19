using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Payment.Application.Commands.RefundPayment;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Enums;
using Sheba.Payment.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Tests.Application;

public sealed class RefundPaymentHandlerTests
{
    private readonly IPaymentRepository _repo = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();

    private RefundPaymentHandler Build() => new(_repo, _gateway, NullLogger<RefundPaymentHandler>.Instance);

    private static PaymentOrder CompletedOrder(decimal amount = 500m)
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), amount, "YER");
        order.MarkPaid("GW-REF");
        return order;
    }

    [Fact]
    public async Task Handle_NonCompletedOrder_ThrowsDomainException_NeverCallsGateway()
    {
        var order = PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), 500m, "YER"); // still Pending
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var sut = Build();
        var act = () => sut.Handle(new RefundPaymentCommand(order.Id), default);

        await act.Should().ThrowAsync<DomainException>();
        await _gateway.DidNotReceive().RefundAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CompletedOrder_GatewaySucceeds_SetsRefundedStatus()
    {
        var order = CompletedOrder(900m);
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _gateway.RefundAsync(order.Id, "GW-REF", 900m, "YER", Arg.Any<CancellationToken>())
            .Returns(new GatewayRefundResult(true, "REFUND-1", "ok"));

        var sut = Build();
        var dto = await sut.Handle(new RefundPaymentCommand(order.Id), default);

        dto.Status.Should().Be(PaymentStatus.Refunded.ToString());
        dto.RefundReference.Should().Be("REFUND-1");
    }

    [Fact]
    public async Task Handle_GatewayDeclinesRefund_ThrowsDomainException_OrderStaysCompleted()
    {
        var order = CompletedOrder();
        _repo.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _gateway.RefundAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GatewayRefundResult(false, null, "declined"));

        var sut = Build();
        var act = () => sut.Handle(new RefundPaymentCommand(order.Id), default);

        await act.Should().ThrowAsync<DomainException>();
        order.Status.Should().Be(PaymentStatus.Completed);
    }
}
