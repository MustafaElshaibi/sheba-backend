namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// CSPRNG-backed OTP code generation, kept in the Application layer's control per §6.6:
/// OTP providers only deliver a code they're handed, they never generate or choose one
/// themselves — so swapping providers can't silently change the code-generation policy
/// (length, entropy source).
///
/// Implementation lives in Identity.Infrastructure. The Application layer only sees this
/// interface (Clean Architecture: Application → Kernel only).
/// </summary>
public interface IOtpCodeGenerator
{
    /// <summary>Generates a cryptographically random numeric code of the given length (default 6).</summary>
    string GenerateNumericCode(int length = 6);
}
