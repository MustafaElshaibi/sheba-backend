using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Admin.Application.EventHandlers;
using Sheba.Admin.Application.Interfaces;
using Sheba.Admin.Domain.Entities;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Tests.Application.EventHandlers;

/// <summary>T-PAY-1: Admin subscribes to PaymentCompletedEvent for the revenue snapshot.</summary>
public sealed class OnPaymentCompletedHandlerTests
{
    private readonly IAdminAnalyticsRepository _analyticsRepo = Substitute.For<IAdminAnalyticsRepository>();
    private readonly IInboxGuard _inboxGuard = Substitute.For<IInboxGuard>();

    private OnPaymentCompletedHandler Build() =>
        new(_analyticsRepo, _inboxGuard, NullLogger<OnPaymentCompletedHandler>.Instance);

    private static PaymentCompletedEvent Event(decimal amount = 500m, string currency = "YER") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, currency, "GW-REF", DateTime.UtcNow);

    [Fact]
    public async Task Handle_AlreadyProcessed_SkipsEntirely()
    {
        var evt = Event();
        _inboxGuard.IsProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        await Build().Handle(evt, default);

        await _analyticsRepo.DidNotReceive().GetOrCreateRevenueSnapshotAsync(
            Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewPayment_RecordsAmountOnTodaysSnapshot()
    {
        var evt = Event(1500m, "YER");
        _inboxGuard.IsProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var snapshot = DailyRevenueSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), "YER");
        _analyticsRepo.GetOrCreateRevenueSnapshotAsync(Arg.Any<DateOnly>(), "YER", Arg.Any<CancellationToken>())
            .Returns(snapshot);

        await Build().Handle(evt, default);

        snapshot.TotalAmount.Should().Be(1500m);
        snapshot.PaymentsCompleted.Should().Be(1);
        await _analyticsRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _inboxGuard.Received(1).MarkProcessedAsync(evt.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
