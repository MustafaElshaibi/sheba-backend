using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Adapters;

namespace Sheba.Identity.Tests.Infrastructure.Adapters;

public sealed class FailoverOtpProviderTests
{
    private readonly IOtpSpendAlarm _alarm = Substitute.For<IOtpSpendAlarm>();

    private static IOtpProvider ProviderReturning(bool succeeds)
    {
        var p = Substitute.For<IOtpProvider>();
        p.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns(new OtpSendResult(succeeds));
        return p;
    }

    private FailoverOtpProvider Build(IReadOnlyList<string> order, params (string key, IOtpProvider provider)[] providers)
    {
        var services = new ServiceCollection();
        foreach (var (key, provider) in providers)
            services.AddKeyedSingleton(key, provider);
        var sp = services.BuildServiceProvider();
        return new FailoverOtpProvider(sp, _alarm, NullLogger<FailoverOtpProvider>.Instance, order);
    }

    [Fact]
    public async Task SendAsync_FirstProviderSucceeds_DoesNotTrySecond()
    {
        var primary = ProviderReturning(true);
        var secondary = ProviderReturning(true);
        var sut = Build(["Primary", "Secondary"], ("Primary", primary), ("Secondary", secondary));

        var result = await sut.SendAsync("0777", "123456", OtpPurpose.Login, OtpChannel.Sms);

        result.Succeeded.Should().BeTrue();
        await secondary.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_FirstFails_FailsOverToSecond()
    {
        var primary = ProviderReturning(false);
        var secondary = ProviderReturning(true);
        var sut = Build(["Primary", "Secondary"], ("Primary", primary), ("Secondary", secondary));

        var result = await sut.SendAsync("0777", "123456", OtpPurpose.Login, OtpChannel.Sms);

        result.Succeeded.Should().BeTrue();
        await secondary.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_AllFail_ReturnsFailure_AndRaisesExhaustedAlarm()
    {
        var sut = Build(["Primary", "Secondary"], ("Primary", ProviderReturning(false)), ("Secondary", ProviderReturning(false)));

        var result = await sut.SendAsync("0777", "123456", OtpPurpose.Login, OtpChannel.Sms);

        result.Succeeded.Should().BeFalse();
        await _alarm.Received(1).RecordExhaustedAsync(OtpChannel.Sms, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ProviderThrows_TreatedAsFailure_AndContinues()
    {
        var throwing = Substitute.For<IOtpProvider>();
        throwing.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns<OtpSendResult>(_ => throw new InvalidOperationException("gateway down"));
        var backup = ProviderReturning(true);
        var sut = Build(["Throwing", "Backup"], ("Throwing", throwing), ("Backup", backup));

        var result = await sut.SendAsync("0777", "123456", OtpPurpose.Login, OtpChannel.Sms);

        result.Succeeded.Should().BeTrue();
    }
}
