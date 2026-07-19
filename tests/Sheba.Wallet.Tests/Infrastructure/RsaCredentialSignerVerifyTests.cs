using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sheba.Wallet.Domain.Interfaces;
using Sheba.Wallet.Infrastructure.Credentials;

namespace Sheba.Wallet.Tests.Infrastructure;

/// <summary>T-WAL-2: signature verification must accept genuine Sheba-issued JWTs and reject
/// anything else — forged, tampered, or malformed.</summary>
public sealed class RsaCredentialSignerVerifyTests
{
    private readonly RsaCredentialSigner _sut = new(
        new ConfigurationBuilder().Build(),
        NullLogger<RsaCredentialSigner>.Instance);

    private static readonly IdentityCredentialClaims Claims = new(
        Guid.NewGuid(), "****1234", "Ahmed Al-Yemeni", "أحمد اليمني", 2);

    [Fact]
    public void VerifyIssuerSignature_GenuineJwt_ReturnsTrue()
    {
        var signed = _sut.SignIdentityCredential(Claims, TimeSpan.FromDays(1));

        _sut.VerifyIssuerSignature(signed.Jwt).Should().BeTrue();
    }

    [Fact]
    public void VerifyIssuerSignature_TamperedPayload_ReturnsFalse()
    {
        var signed = _sut.SignIdentityCredential(Claims, TimeSpan.FromDays(1));
        var parts = signed.Jwt.Split('.');
        // Flip the payload to a different (still base64url-valid) segment — signature no longer matches.
        var tampered = $"{parts[0]}.{parts[0]}.{parts[2]}";

        _sut.VerifyIssuerSignature(tampered).Should().BeFalse();
    }

    [Fact]
    public void VerifyIssuerSignature_ForeignJwt_ReturnsFalse()
    {
        var foreignSigner = new RsaCredentialSigner(new ConfigurationBuilder().Build(), NullLogger<RsaCredentialSigner>.Instance); // different RSA key
        var signed = foreignSigner.SignIdentityCredential(Claims, TimeSpan.FromDays(1));

        _sut.VerifyIssuerSignature(signed.Jwt).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    public void VerifyIssuerSignature_MalformedJwt_ReturnsFalse(string malformed)
    {
        _sut.VerifyIssuerSignature(malformed).Should().BeFalse();
    }
}
