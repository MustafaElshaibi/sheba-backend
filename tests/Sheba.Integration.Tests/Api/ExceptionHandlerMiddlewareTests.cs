using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Sheba.Api.Middleware;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Integration.Tests.Api;

/// <summary>
/// T-API-1: the global exception middleware maps kernel exceptions to JSend fail/error envelopes
/// (docs/api-contract.md §4), writes JSend bodies for bare 401/403 challenges, never leaks stack
/// traces, and leaves OIDC protocol routes (/connect/*) speaking the OAuth wire format.
/// </summary>
public sealed class ExceptionHandlerMiddlewareTests
{
    private static async Task<(int StatusCode, JsonDocument? Body)> RunAsync(
        RequestDelegate next, string path = "/api/requests")
    {
        var middleware = new ExceptionHandlerMiddleware(next, NullLogger<ExceptionHandlerMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await middleware.InvokeAsync(context);

        var bytes = buffer.ToArray();
        return (context.Response.StatusCode, bytes.Length == 0 ? null : JsonDocument.Parse(bytes));
    }

    // ── Exception → envelope mapping ──────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400FailWithFieldKeys()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["nationalId"] = ["National ID must be exactly 10 digits."],
            ["phoneNumber"] = ["Phone number is required.", "Phone number must be in +967 format."],
        };

        var (status, body) = await RunAsync(_ => throw new ValidationException(errors));

        status.Should().Be(StatusCodes.Status400BadRequest);
        body!.RootElement.GetProperty("status").GetString().Should().Be("fail");
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("nationalId").GetString().Should().Be("National ID must be exactly 10 digits.");
        // Multiple messages per field are joined into the single JSend value.
        data.GetProperty("phoneNumber").GetString().Should().Contain("+967");
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404Fail()
    {
        var (status, body) = await RunAsync(_ => throw new NotFoundException("ServiceRequest", Guid.Empty));

        status.Should().Be(StatusCodes.Status404NotFound);
        body!.RootElement.GetProperty("status").GetString().Should().Be("fail");
        body.RootElement.GetProperty("data").GetProperty("resource").GetString().Should().Contain("ServiceRequest");
    }

    [Fact]
    public async Task InvokeAsync_DomainException_Returns422Fail()
    {
        var (status, body) = await RunAsync(_ => throw new DomainException("Account is not approved."));

        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        body!.RootElement.GetProperty("status").GetString().Should().Be("fail");
        body.RootElement.GetProperty("data").GetProperty("domain").GetString().Should().Be("Account is not approved.");
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns403Fail()
    {
        var (status, body) = await RunAsync(_ => throw new UnauthorizedAccessException("secret internals"));

        status.Should().Be(StatusCodes.Status403Forbidden);
        body!.RootElement.GetProperty("status").GetString().Should().Be("fail");
        // The body must not echo the exception message — it may describe internals.
        body.RootElement.GetProperty("data").GetProperty("permissions").GetString().Should().NotContain("secret");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500ErrorWithCorrelationIdAndNoStackTrace()
    {
        var (status, body) = await RunAsync(_ => throw new InvalidOperationException("db password is hunter2"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        var root = body!.RootElement;
        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("code").GetInt32().Should().Be(5001);
        root.GetProperty("data").GetProperty("correlation_id").GetString().Should().NotBeNullOrEmpty();

        // No internals may leak: not the exception message, type, or stack frames.
        var raw = root.GetRawText();
        raw.Should().NotContain("hunter2").And.NotContain("InvalidOperationException").And.NotContain("   at ");
        root.GetProperty("message").GetString().Should().Be("An unexpected error occurred while processing the request.");
    }

    // ── Bare challenge responses get JSend bodies ─────────────────────────────

    [Fact]
    public async Task InvokeAsync_Bare401Challenge_GetsJSendFailBody()
    {
        var (status, body) = await RunAsync(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });

        status.Should().Be(StatusCodes.Status401Unauthorized);
        body!.RootElement.GetProperty("status").GetString().Should().Be("fail");
        body.RootElement.GetProperty("data").GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_Bare403Challenge_GetsJSendFailBody()
    {
        var (status, body) = await RunAsync(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        });

        status.Should().Be(StatusCodes.Status403Forbidden);
        body!.RootElement.GetProperty("status").GetString().Should().Be("fail");
        body.RootElement.GetProperty("data").GetProperty("permissions").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulResponse_IsLeftUntouched()
    {
        var (status, body) = await RunAsync(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        status.Should().Be(StatusCodes.Status200OK);
        body.Should().BeNull("the middleware must not write anything into successful responses");
    }

    // ── OIDC protocol routes are exempt (OAuth wire format, never JSend) ──────

    [Fact]
    public async Task InvokeAsync_ConnectRoute_UnhandledException_SpeaksOAuthErrorShape()
    {
        var (status, body) = await RunAsync(
            _ => throw new InvalidOperationException("boom"), path: "/connect/token");

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body!.RootElement.GetProperty("error").GetString().Should().Be("server_error");
        body.RootElement.TryGetProperty("status", out _).Should().BeFalse("OIDC routes must not get JSend envelopes");
    }

    [Fact]
    public async Task InvokeAsync_ConnectRoute_Bare401_IsNotRewritten()
    {
        var (status, body) = await RunAsync(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }, path: "/connect/userinfo");

        status.Should().Be(StatusCodes.Status401Unauthorized);
        body.Should().BeNull("OIDC challenges carry WWW-Authenticate semantics, not JSend bodies");
    }
}
