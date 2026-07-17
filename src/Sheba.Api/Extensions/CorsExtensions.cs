namespace Sheba.Api.Extensions;

/// <summary>
/// Config-driven CORS policy (T-GW-1). Allowed origins come from <c>Cors:AllowedOrigins</c> —
/// never a wildcard, since bearer tokens ride in headers set by JS callers. An unlisted origin
/// gets no CORS headers at all, so the browser blocks the response; the policy is silently empty
/// (blocks everything cross-origin) if the section is missing, which is the safe default for an
/// environment nobody has configured yet.
/// </summary>
public static class CorsExtensions
{
    public const string PolicyName = "ShebaSpa";

    public static IServiceCollection AddShebaCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }
}
