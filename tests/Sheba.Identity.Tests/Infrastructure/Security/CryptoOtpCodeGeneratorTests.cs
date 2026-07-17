using FluentAssertions;
using Sheba.Identity.Infrastructure.Security;

namespace Sheba.Identity.Tests.Infrastructure.Security;

public class CryptoOtpCodeGeneratorTests
{
    private readonly CryptoOtpCodeGenerator _sut = new();

    [Fact]
    public void GenerateNumericCode_DefaultLength_Returns6Digits()
    {
        var code = _sut.GenerateNumericCode();

        code.Should().HaveLength(6);
        code.Should().MatchRegex("^[0-9]{6}$");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public void GenerateNumericCode_CustomLength_ReturnsThatManyDigits(int length)
    {
        var code = _sut.GenerateNumericCode(length);

        code.Should().HaveLength(length);
        code.Should().MatchRegex($"^[0-9]{{{length}}}$");
    }

    [Fact]
    public void GenerateNumericCode_CalledRepeatedly_ProducesVariedOutput()
    {
        // Not a statistical randomness test — just a sanity check that this isn't a constant
        // or trivially-cyclical generator.
        var codes = Enumerable.Range(0, 50).Select(_ => _sut.GenerateNumericCode()).ToHashSet();

        codes.Count.Should().BeGreaterThan(40);
    }
}
