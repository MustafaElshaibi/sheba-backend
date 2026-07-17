using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Sheba.Identity.Infrastructure.Security;

namespace Sheba.Identity.Tests.Infrastructure.Security;

/// <summary>
/// SigningCertificateLoader (T-SEC-4): config-driven cert loading that underpins
/// rotation-by-overlap. Uses real throwaway self-signed PFX files on disk — no mocking of X509
/// APIs — so a passing test proves the actual file-loading code path works, not just the wiring.
/// </summary>
public sealed class SigningCertificateLoaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Load_MissingSection_ReturnsEmptyList()
    {
        var configuration = new ConfigurationBuilder().Build();

        var result = SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Load_EmptyArraySection_ReturnsEmptyList()
    {
        var configuration = BuildConfig(new Dictionary<string, string?>());

        var result = SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Load_PathWithoutPassword_Throws()
    {
        var configuration = BuildConfig(new Dictionary<string, string?>
        {
            ["Identity:SigningCertificates:0:Path"] = "irrelevant.pfx"
        });

        var act = () => SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Password*");
    }

    [Fact]
    public void Load_EntryWithNeitherPathNorThumbprint_Throws()
    {
        var configuration = BuildConfig(new Dictionary<string, string?>
        {
            ["Identity:SigningCertificates:0:StoreName"] = "My"
        });

        var act = () => SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Path*Thumbprint*");
    }

    [Fact]
    public void Load_UnknownThumbprint_Throws()
    {
        var configuration = BuildConfig(new Dictionary<string, string?>
        {
            ["Identity:SigningCertificates:0:Thumbprint"] = new string('F', 40)
        });

        var act = () => SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        act.Should().Throw<InvalidOperationException>().WithMessage("*no certificate*");
    }

    [Fact]
    public void Load_ValidPfxFile_ReturnsMatchingCertificate()
    {
        const string password = "test-pw-1";
        using var expected = CreateSelfSignedCertificate("sheba-test-signing");
        var path = WritePfx(expected, password);

        var configuration = BuildConfig(new Dictionary<string, string?>
        {
            ["Identity:SigningCertificates:0:Path"] = path,
            ["Identity:SigningCertificates:0:Password"] = password
        });

        var result = SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        result.Should().HaveCount(1);
        result[0].Thumbprint.Should().Be(expected.Thumbprint);
    }

    [Fact]
    public void Load_MultipleEntries_PreservesArrayOrderAsPrecedence()
    {
        using var first = CreateSelfSignedCertificate("sheba-test-new");
        using var second = CreateSelfSignedCertificate("sheba-test-old");
        var firstPath = WritePfx(first, "pw1");
        var secondPath = WritePfx(second, "pw2");

        var configuration = BuildConfig(new Dictionary<string, string?>
        {
            ["Identity:SigningCertificates:0:Path"] = firstPath,
            ["Identity:SigningCertificates:0:Password"] = "pw1",
            ["Identity:SigningCertificates:1:Path"] = secondPath,
            ["Identity:SigningCertificates:1:Password"] = "pw2"
        });

        var result = SigningCertificateLoader.Load(configuration, "Identity:SigningCertificates");

        result.Should().HaveCount(2);
        result[0].Thumbprint.Should().Be(first.Thumbprint);
        result[1].Thumbprint.Should().Be(second.Thumbprint);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
    }

    private string WritePfx(X509Certificate2 certificate, string password)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
