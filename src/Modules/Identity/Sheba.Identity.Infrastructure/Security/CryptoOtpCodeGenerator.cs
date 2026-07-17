using System.Security.Cryptography;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// Generates numeric OTP codes using <see cref="RandomNumberGenerator.GetInt32(int, int)"/> —
/// unbiased (rejection-sampled), unlike <c>System.Random</c> or a naive
/// <c>RandomNumberGenerator.Fill</c> + modulo, which both skew toward lower digits.
/// </summary>
public sealed class CryptoOtpCodeGenerator : IOtpCodeGenerator
{
    public string GenerateNumericCode(int length = 6)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));

        return new string(chars);
    }
}
