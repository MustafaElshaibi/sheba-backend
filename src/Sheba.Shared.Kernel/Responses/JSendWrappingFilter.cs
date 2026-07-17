using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Sheba.Shared.Kernel.Responses;

/// <summary>
/// Endpoint filter that wraps every handler result in a JSend envelope (T-API-1). Registered on
/// each module route group via <c>group.AddEndpointFilter&lt;JSendWrappingFilter&gt;()</c> —
/// endpoints keep returning raw DTOs / <c>Results.*</c> and never build envelopes by hand.
/// OIDC groups (<c>/connect/*</c>, <c>/.well-known/*</c>) simply don't register the filter.
/// </summary>
public sealed class JSendWrappingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        return result switch
        {
            // Handler built its own envelope (rare, but sanctioned via the JSend factories).
            IJSendEnvelope envelope => new JSendHttpResult(envelope, StatusCodes.Status200OK),
            IResult httpResult      => WrapHttpResult(httpResult),
            // Raw DTO (or null) returned straight from the handler.
            _                       => new JSendHttpResult(JSend.Success(result), StatusCodes.Status200OK),
        };
    }

    private static object WrapHttpResult(IResult httpResult)
    {
        // Already an envelope inside an IResult — pass through untouched.
        if (httpResult is IValueHttpResult { Value: IJSendEnvelope })
            return httpResult;

        // Results that carry a JSON value: Ok(x), Created(uri, x), BadRequest(x), Conflict(x)…
        if (httpResult is IValueHttpResult valueResult)
        {
            var status = (httpResult as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
            // Created<T> exposes Location; preserve it through the rewrap.
            var location = httpResult.GetType().GetProperty("Location")?.GetValue(httpResult) as string;
            return new JSendHttpResult(BuildEnvelope(status, valueResult.Value), status, location);
        }

        // Value-less status results: NotFound(), NoContent(), Conflict(), Unauthorized()…
        // Content/file/redirect/challenge results fall through untouched — they are not JSON DTOs.
        if (httpResult is IStatusCodeHttpResult { StatusCode: { } statusCode }
            && httpResult is not (IContentTypeHttpResult or IFileHttpResult))
        {
            // 204 → 200 with data: null so the envelope survives (api-contract.md §3).
            var effective = statusCode == StatusCodes.Status204NoContent ? StatusCodes.Status200OK : statusCode;
            return new JSendHttpResult(BuildEnvelope(effective, data: null), effective);
        }

        return httpResult;
    }

    private static IJSendEnvelope BuildEnvelope(int statusCode, object? data) => statusCode switch
    {
        < 400 => JSend.Success(data),
        // A value-carrying 4xx keeps its payload as the fail data; a bare one gets a generic,
        // non-enumerating message keyed per api-contract.md §2 ("descriptive key").
        400 => new JSendResponse<object>("fail", data ?? Generic("request", "The request could not be processed.")),
        401 => new JSendResponse<object>("fail", data ?? Generic("token", "Authentication is required to access this resource.")),
        403 => new JSendResponse<object>("fail", data ?? Generic("permissions", "You do not have permission to perform this action.")),
        404 => new JSendResponse<object>("fail", data ?? Generic("resource", "The requested resource was not found.")),
        409 => new JSendResponse<object>("fail", data ?? Generic("conflict", "The request conflicts with the current state of the resource.")),
        < 500 => new JSendResponse<object>("fail", data ?? Generic("request", "The request could not be processed.")),
        _ => JSend.Error("An unexpected error occurred while processing the request.", code: 5001, data),
    };

    private static Dictionary<string, string> Generic(string key, string message) => new() { [key] = message };
}

/// <summary>
/// Minimal IResult that writes a JSend envelope with an explicit HTTP status (and optional
/// Location header, preserved from Created results).
/// </summary>
public sealed class JSendHttpResult(IJSendEnvelope envelope, int statusCode, string? location = null) : IResult
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = statusCode;
        if (location is not null)
            httpContext.Response.Headers.Location = location;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body, envelope, envelope.GetType(), JsonOptions, httpContext.RequestAborted);
    }
}
