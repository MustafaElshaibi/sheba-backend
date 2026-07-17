using Serilog.Context;

namespace Sheba.Api.Middleware;

/// <summary>
/// Assigns a correlation id to every request (T-GW-1): reuses an inbound <c>X-Correlation-Id</c>
/// header if the caller supplied one, otherwise mints one from <see cref="HttpContext.TraceIdentifier"/>.
/// Pushed into the Serilog scope so every log line for the request carries it, and echoed back on
/// the response per docs/api-contract.md §2 ("every response echoes X-Correlation-Id").
///
/// Registered right after <see cref="ExceptionHandlerMiddleware"/> so the id is available to the
/// 500 handler via <see cref="GetCorrelationId"/> even when the exception originates deep in the
/// pipeline.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemsKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : context.TraceIdentifier;

        context.Items[ItemsKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    /// <summary>Reads the id this middleware assigned to the current request, falling back to the
    /// ASP.NET Core trace identifier if this middleware hasn't run yet (e.g. in a unit test).</summary>
    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemsKey, out var id) && id is string s ? s : context.TraceIdentifier;
}
