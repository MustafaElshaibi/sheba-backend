namespace Sheba.Shared.Kernel.RateLimiting;

/// <summary>
/// Named rate-limiting policy identifiers shared between endpoint mappings (in each module's
/// Infrastructure project) and the actual policy registration in <c>Sheba.Api</c> (T-SEC-2).
/// Lives here — not in <c>Sheba.Api</c> — because module projects may reference only
/// Sheba.Shared.Kernel; the host owns the Redis-backed implementation, modules only need the
/// policy name string to tag their endpoints with <c>.RequireRateLimiting(...)</c>.
/// </summary>
public static class RateLimitPolicyNames
{
    public const string IdentityRegister = "identity_register";
    public const string IdentityLogin = "identity_login";
    public const string IdentityOtp = "identity_otp";
    public const string ConnectToken = "connect_token";
}
