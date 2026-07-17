using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Sheba.Shared.Kernel.RateLimiting;
using Sheba.Shared.Kernel.Responses;
using StackExchange.Redis;

namespace Sheba.Api.RateLimiting;

/// <summary>
/// Wires ASP.NET Core's <c>RateLimiter</c> middleware for T-SEC-2. Named, Redis-backed sliding
/// windows on the auth-sensitive endpoints (register/login/verify-otp/connect-token — the ones
/// worth flooding to enumerate accounts, brute-force OTPs, or exhaust the token endpoint); an
/// in-memory global limiter as a sane default everywhere else. 429s render as JSend `fail` on
/// every route except <c>/connect/*</c>, which keeps speaking OAuth 2.0 error JSON per the same
/// exemption the exception middleware uses (docs/api-contract.md §4).
/// </summary>
public static class RateLimitingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddShebaRateLimiting(
        this IServiceCollection services, IConnectionMultiplexer redis)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Sane default for every endpoint that doesn't opt into a named policy below —
            // in-memory is fine here: it's a blanket safety net, not a security control, and
            // doesn't warrant a Redis round trip on every single request in the system.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            AddRedisPolicy(options, RateLimitPolicyNames.IdentityRegister, redis, permitLimit: 5, window: TimeSpan.FromMinutes(5));
            AddRedisPolicy(options, RateLimitPolicyNames.IdentityLogin, redis, permitLimit: 10, window: TimeSpan.FromMinutes(5));
            AddRedisPolicy(options, RateLimitPolicyNames.IdentityOtp, redis, permitLimit: 10, window: TimeSpan.FromMinutes(5));
            AddRedisPolicy(options, RateLimitPolicyNames.ConnectToken, redis, permitLimit: 30, window: TimeSpan.FromMinutes(1));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)retryAfter.TotalSeconds)
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

                if (context.HttpContext.Request.Path.StartsWithSegments("/connect"))
                {
                    await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(
                        new Dictionary<string, string>
                        {
                            ["error"] = "slow_down",
                            ["error_description"] = "Too many token requests. Please wait before retrying."
                        }, JsonOptions), cancellationToken);
                    return;
                }

                var envelope = JSend.Fail("rate_limit",
                    $"Too many requests. Try again in {retryAfterSeconds} seconds.");
                await JsonSerializer.SerializeAsync(
                    context.HttpContext.Response.Body, envelope, JsonOptions, cancellationToken);
            };
        });

        return services;
    }

    private static void AddRedisPolicy(
        RateLimiterOptions options, string policyName, IConnectionMultiplexer redis, int permitLimit, TimeSpan window)
    {
        options.AddPolicy(policyName, context => RateLimitPartition.Get(
            ClientKey(context),
            key => new RedisSlidingWindowRateLimiter(redis, $"ratelimit:{policyName}:{key}", permitLimit, window)));
    }

    /// <summary>Partition key: caller's IP. No proxy in front of this API yet, so no X-Forwarded-For trust needed.</summary>
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
