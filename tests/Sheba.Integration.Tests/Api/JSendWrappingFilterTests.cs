using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Sheba.Shared.Kernel.Responses;

namespace Sheba.Integration.Tests.Api;

/// <summary>
/// T-API-1: the endpoint wrapping filter must turn every handler result into a JSend envelope
/// per docs/api-contract.md §3–4 — success/fail keyed by HTTP status, 204 flattened to 200,
/// Created's Location preserved, non-JSON results (files) passed through untouched.
/// </summary>
public sealed class JSendWrappingFilterTests
{
    private sealed record SampleDto(Guid Id, string Name);

    private static async Task<object?> RunFilterAsync(object? handlerResult)
    {
        var filter = new JSendWrappingFilter();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        return await filter.InvokeAsync(context, _ => ValueTask.FromResult(handlerResult));
    }

    /// <summary>Executes the produced IResult and parses the JSON body + status code.</summary>
    private static async Task<(int StatusCode, JsonDocument Body, string? Location)> ExecuteAsync(object? result)
    {
        result.Should().BeAssignableTo<IResult>("the filter must always return an IResult");
        var httpContext = new DefaultHttpContext();
        using var buffer = new MemoryStream();
        httpContext.Response.Body = buffer;

        await ((IResult)result!).ExecuteAsync(httpContext);

        buffer.Position = 0;
        var body = JsonDocument.Parse(buffer.ToArray());
        return (httpContext.Response.StatusCode, body, httpContext.Response.Headers.Location.FirstOrDefault());
    }

    [Fact]
    public async Task InvokeAsync_RawDto_WrapsInSuccessEnvelope()
    {
        var dto = new SampleDto(Guid.NewGuid(), "passport renewal");

        var wrapped = await RunFilterAsync(dto);
        var (status, body, _) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status200OK);
        body.RootElement.GetProperty("status").GetString().Should().Be("success");
        body.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("passport renewal");
    }

    [Fact]
    public async Task InvokeAsync_OkResult_UnwrapsValueIntoSuccessEnvelope()
    {
        var dto = new SampleDto(Guid.NewGuid(), "birth certificate");

        var wrapped = await RunFilterAsync(Results.Ok(dto));
        var (status, body, _) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status200OK);
        body.RootElement.GetProperty("status").GetString().Should().Be("success");
        body.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("birth certificate");
    }

    [Fact]
    public async Task InvokeAsync_CreatedResult_Preserves201AndLocation()
    {
        var dto = new SampleDto(Guid.NewGuid(), "new request");

        var wrapped = await RunFilterAsync(Results.Created($"/api/requests/{dto.Id}", dto));
        var (status, body, location) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status201Created);
        location.Should().Be($"/api/requests/{dto.Id}");
        body.RootElement.GetProperty("status").GetString().Should().Be("success");
        body.RootElement.GetProperty("data").GetProperty("id").GetGuid().Should().Be(dto.Id);
    }

    [Fact]
    public async Task InvokeAsync_NotFoundResult_BecomesFailEnvelope()
    {
        var wrapped = await RunFilterAsync(Results.NotFound());
        var (status, body, _) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status404NotFound);
        body.RootElement.GetProperty("status").GetString().Should().Be("fail");
        body.RootElement.GetProperty("data").GetProperty("resource").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_NoContentResult_Becomes200SuccessWithNullData()
    {
        var wrapped = await RunFilterAsync(Results.NoContent());
        var (status, body, _) = await ExecuteAsync(wrapped);

        // 204 → 200 with data: null so the envelope survives (api-contract.md §3).
        status.Should().Be(StatusCodes.Status200OK);
        body.RootElement.GetProperty("status").GetString().Should().Be("success");
        body.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeAsync_BadRequestWithValue_BecomesFailEnvelopeKeepingPayload()
    {
        var wrapped = await RunFilterAsync(Results.BadRequest(new { reason = "signature mismatch" }));
        var (status, body, _) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.RootElement.GetProperty("status").GetString().Should().Be("fail");
        body.RootElement.GetProperty("data").GetProperty("reason").GetString().Should().Be("signature mismatch");
    }

    [Fact]
    public async Task InvokeAsync_ConflictResult_BecomesFailEnvelope()
    {
        var wrapped = await RunFilterAsync(Results.Conflict());
        var (status, body, _) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status409Conflict);
        body.RootElement.GetProperty("status").GetString().Should().Be("fail");
    }

    [Fact]
    public async Task InvokeAsync_PrebuiltEnvelope_PassesThroughWithoutDoubleWrapping()
    {
        var envelope = JSend.Success(new SampleDto(Guid.NewGuid(), "already wrapped"));

        var wrapped = await RunFilterAsync(envelope);
        var (status, body, _) = await ExecuteAsync(wrapped);

        status.Should().Be(StatusCodes.Status200OK);
        body.RootElement.GetProperty("status").GetString().Should().Be("success");
        // A double-wrap would put another envelope under data.data.
        body.RootElement.GetProperty("data").TryGetProperty("status", out _).Should().BeFalse();
        body.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("already wrapped");
    }

    [Fact]
    public async Task InvokeAsync_FileResult_PassesThroughUntouched()
    {
        var fileResult = Results.File(new byte[] { 1, 2, 3 }, "application/pdf", "report.pdf");

        var result = await RunFilterAsync(fileResult);

        result.Should().BeSameAs(fileResult, "binary responses must not be wrapped in JSON envelopes");
    }
}
