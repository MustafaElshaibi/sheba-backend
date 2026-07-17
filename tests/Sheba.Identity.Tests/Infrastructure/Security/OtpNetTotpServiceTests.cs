using FluentAssertions;
using OtpNet;
using Sheba.Identity.Infrastructure.Security;

namespace Sheba.Identity.Tests.Infrastructure.Security;

public sealed class OtpNetTotpServiceTests
{
    private readonly OtpNetTotpService _sut = new();

    [Fact]
    public void GenerateSecret_ReturnsValidBase32()
    {
        var secret = _sut.GenerateSecret();

        secret.Should().NotBeNullOrEmpty();
        var act = () => Base32Encoding.ToBytes(secret);
        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyCode_CurrentCode_ReturnsTrue()
    {
        var secret = _sut.GenerateSecret();
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        _sut.VerifyCode(secret, code).Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_WrongCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecret();

        _sut.VerifyCode(secret, "000000").Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_EmptyCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecret();

        _sut.VerifyCode(secret, "").Should().BeFalse();
    }

    [Fact]
    public void BuildProvisioningUri_ContainsSecretAndAccountLabel()
    {
        var uri = _sut.BuildProvisioningUri("JBSWY3DPEHPK3PXP", "admin@sheba.gov");

        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain("secret=JBSWY3DPEHPK3PXP");
        uri.Should().Contain("Sheba");
    }
}
