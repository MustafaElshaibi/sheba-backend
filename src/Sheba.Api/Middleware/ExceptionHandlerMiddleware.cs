using System.Text.Json;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Responses;
using ValidationException = Sheba.Shared.Kernel.Exceptions.ValidationException;

namespace Sheba.Api.Middleware;

/// <summary>
/// Global exception handler + JSend challenge-body writer (T-API-1).
///
/// Exceptions map to JSend envelopes per docs/api-contract.md §3–4:
///   ValidationException → 400 fail (field dictionary), NotFoundException → 404 fail,
///   DomainException → 422 fail, UnauthorizedAccessException → 403 fail,
///   anything else → 500 error with correlation id and no stack trace.
///
/// It also rewrites empty 401/403 challenge responses (produced by the authN/authZ middleware)
/// into JSend fail bodies so unauthenticated callers still get the envelope.
///
/// OIDC protocol routes (/connect/*, /.well-known/*) are exempt — they must speak the OAuth 2.0
/// wire format ({"error": "..."}), never JSend. Hangfire and Swagger UIs are also left alone.
/// </summary>
public sealed class ExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);

            // ── JSend challenge bodies: authN/authZ middleware emits bare 401/403 ────────────
            if (!context.Response.HasStarted
                && context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
                && !IsEnvelopeExempt(context.Request.Path))
            {
                var (key, message) = context.Response.StatusCode == StatusCodes.Status401Unauthorized
                    ? ("token", "Authentication is required to access this resource.")
                    : ("permissions", "You do not have permission to perform this action.");

                await WriteJSendAsync(context, context.Response.StatusCode, JSend.Fail(key, message));
            }
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("[400] Validation error on {Path}: {@Errors}", context.Request.Path, ex.Errors);
            // FluentValidation gives string[] per field; JSend fail wants one message per key.
            var data = ex.Errors.ToDictionary(kvp => kvp.Key, kvp => string.Join(" ", kvp.Value));
            await WriteExceptionAsync(context, StatusCodes.Status400BadRequest, JSend.Fail(data),
                oauthError: "invalid_request", oauthDescription: "One or more request parameters are invalid.");
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("[404] Not found: {Message}", ex.Message);
            await WriteExceptionAsync(context, StatusCodes.Status404NotFound, JSend.Fail("resource", ex.Message),
                oauthError: "invalid_request", oauthDescription: "The requested resource was not found.");
        }
        catch (DomainException ex)
        {
            logger.LogWarning("[422] Domain rule violation: {Message}", ex.Message);
            await WriteExceptionAsync(context, StatusCodes.Status422UnprocessableEntity, JSend.Fail("domain", ex.Message),
                oauthError: "invalid_grant", oauthDescription: ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("[403] Forbidden: {Message}", ex.Message);
            await WriteExceptionAsync(context, StatusCodes.Status403Forbidden,
                JSend.Fail("permissions", "You do not have permission to perform this action."),
                oauthError: "access_denied", oauthDescription: "The caller is not allowed to perform this action.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[500] Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var envelope = JSend.Error(
                "An unexpected error occurred while processing the request.",
                code: 5001,
                // snake_case key is contractual — see the worked example in api-contract.md §5.
                data: new Dictionary<string, string> { ["correlation_id"] = context.TraceIdentifier });

            await WriteExceptionAsync(context, StatusCodes.Status500InternalServerError, envelope,
                oauthError: "server_error", oauthDescription: "The authorization server encountered an unexpected error.");
        }
    }

    /// <summary>Routes that must not receive JSend envelopes (spec-mandated wire formats / UIs).</summary>
    private static bool IsEnvelopeExempt(PathString path) =>
        path.StartsWithSegments("/connect")
        || path.StartsWithSegments("/.well-known")
        || path.StartsWithSegments("/swagger")
        || path.StartsWithSegments("/hangfire");

    /// <summary>Speaks OAuth 2.0 error JSON on OIDC protocol routes only (not Swagger/Hangfire).</summary>
    private static bool IsOAuthRoute(PathString path) =>
        path.StartsWithSegments("/connect") || path.StartsWithSegments("/.well-known");

    private async Task WriteExceptionAsync(
        HttpContext context, int statusCode, IJSendEnvelope envelope,
        string oauthError, string oauthDescription)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Response already started; cannot write error body for {Path}.", context.Request.Path);
            return;
        }

        if (IsOAuthRoute(context.Request.Path))
        {
            context.Response.StatusCode = statusCode >= 500 ? statusCode : StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new Dictionary<string, string> { ["error"] = oauthError, ["error_description"] = oauthDescription },
                JsonOptions));
            return;
        }

        await WriteJSendAsync(context, statusCode, envelope);
    }

    private static async Task WriteJSendAsync(HttpContext context, int statusCode, IJSendEnvelope envelope)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(
            context.Response.Body, envelope, envelope.GetType(), JsonOptions, context.RequestAborted);
    }
}
