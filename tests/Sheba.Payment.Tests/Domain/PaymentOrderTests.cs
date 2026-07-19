using FluentAssertions;
using Sheba.Payment.Domain.Entities;
using Sheba.Payment.Domain.Enums;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Payment.Tests.Domain;

public sealed class PaymentOrderTests
{
    private static PaymentOrder NewOrder(decimal amount = 500m) =>
        PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), amount, "YER", "Test fee");

    [Fact]
    public void Create_NonPositiveAmount_Throws()
    {
        var act = () => PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), 0m, "YER");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkPaid_FromPending_CompletesAndRaisesPaymentCompletedEvent()
    {
        var order = NewOrder(1200m);

        order.MarkPaid("GATEWAY-REF-1");

        order.Status.Should().Be(PaymentStatus.Completed);
        order.GatewayReference.Should().Be("GATEWAY-REF-1");
        order.PaidAt.Should().NotBeNull();
        order.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PaymentCompletedEvent>();

        var evt = (PaymentCompletedEvent)order.DomainEvents.Single();
        evt.PaymentOrderId.Should().Be(order.Id);
        evt.Amount.Should().Be(1200m);
        evt.Currency.Should().Be("YER");
    }

    [Fact]
    public void MarkPaid_AlreadyCompleted_Throws()
    {
        var order = NewOrder();
        order.MarkPaid();

        var act = () => order.MarkPaid();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Refund_FromCompleted_SetsRefundedStatus()
    {
        var order = NewOrder();
        order.MarkPaid();

        order.Refund("REFUND-1");

        order.Status.Should().Be(PaymentStatus.Refunded);
        order.RefundReference.Should().Be("REFUND-1");
        order.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public void Refund_FromPending_Throws()
    {
        var order = NewOrder();

        var act = () => order.Refund();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Refund_AlreadyRefunded_Throws()
    {
        var order = NewOrder();
        order.MarkPaid();
        order.Refund();

        var act = () => order.Refund();

        act.Should().Throw<DomainException>();
    }
}
