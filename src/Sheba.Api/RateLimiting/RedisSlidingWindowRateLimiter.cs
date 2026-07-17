using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Sheba.Api.RateLimiting;

/// <summary>
/// Distributed sliding-window-log rate limiter backed by Redis (T-SEC-2). Each partition (one per
/// client IP) is a Redis sorted set: members are per-request tokens scored by their arrival time
/// in epoch milliseconds. A single Lua script does the trim-count-add atomically, so concurrent
/// requests across the process (or, once horizontally scaled, across instances) can't race past
/// the limit — the whole point of "Redis-backed counters" over an in-memory limiter.
/// </summary>
public sealed class RedisSlidingWindowRateLimiter(
    IConnectionMultiplexer redis, string redisKey, int permitLimit, TimeSpan window) : RateLimiter
{
    // LuaScript.Prepare rewrites @tokens into KEYS[n]/ARGV[n] itself based on the parameter's
    // .NET type (RedisKey → KEYS, everything else → ARGV) — do not hand-write KEYS[]/ARGV[]
    // alongside it, the two addressing schemes don't compose.
    private static readonly LuaScript Script = LuaScript.Prepare(
        """
        redis.call('ZREMRANGEBYSCORE', @key, '-inf', tonumber(@now) - tonumber(@window))
        local count = redis.call('ZCARD', @key)
        if count < tonumber(@limit) then
            redis.call('ZADD', @key, @now, @member)
            redis.call('PEXPIRE', @key, @window)
            return 1
        end
        return 0
        """);

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
        => AcquireAsyncCore(permitCount, CancellationToken.None).AsTask().GetAwaiter().GetResult();

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var allowed = (int)await db.ScriptEvaluateAsync(
            Script,
            new
            {
                key = (RedisKey)redisKey,
                now = nowMs,
                window = (long)window.TotalMilliseconds,
                limit = permitLimit,
                member = $"{nowMs}-{Guid.NewGuid():N}"
            }) == 1;

        return allowed ? SucceededLease : new SimpleLease(false, window);
    }

    private static readonly RateLimitLease SucceededLease = new SimpleLease(true, null);

    private sealed class SimpleLease(bool acquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name && retryAfter is not null)
            {
                metadata = retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
