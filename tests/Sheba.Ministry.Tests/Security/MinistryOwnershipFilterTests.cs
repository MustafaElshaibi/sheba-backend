using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sheba.Ministry.Infrastructure.Security;

namespace Sheba.Ministry.Tests.Security;

/// <summary>
/// T-AUTH-1: MinistryOwnershipFilter gates any .../{id:guid}/... route where "id" is the ministry
/// being acted on. SuperAdmin (no ministry_id claim) is unrestricted; a MinistryManager's token
/// must carry a ministry_id matching the route.
/// </summary>
public sealed class MinistryOwnershipFilterTests
{
    private readonly MinistryOwnershipFilter _sut = new();

    private static EndpointFilterInvocationContext MakeContext(Guid? tokenMinistryId, Guid? routeId)
    {
        var httpContext = new DefaultHttpContext();

        var claims = new List<Claim>();
        if (tokenMinistryId is { } mid)
            claims.Add(new Claim("ministry_id", mid.ToString()));
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        if (routeId is { } rid)
            httpContext.Request.RouteValues = new RouteValueDictionary { ["id"] = rid.ToString() };

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_SuperAdmin_NoMinistryClaim_AllowsAnyRoute()
    {
        var context = MakeContext(tokenMinistryId: null, routeId: Guid.NewGuid());

        var result = await _sut.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ok"));

        result.Should().Be("ok");
    }

    [Fact]
    public async Task InvokeAsync_MinistryManager_MatchingRoute_Allows()
    {
        var ministryId = Guid.NewGuid();
        var context = MakeContext(tokenMinistryId: ministryId, routeId: ministryId);

        var result = await _sut.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ok"));

        result.Should().Be("ok");
    }

    [Fact]
    public async Task InvokeAsync_MinistryManager_DifferentRoute_Forbids()
    {
        var context = MakeContext(tokenMinistryId: Guid.NewGuid(), routeId: Guid.NewGuid());

        var result = await _sut.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ok"));

        result.Should().BeAssignableTo<IResult>();
        var (statusCode, _) = await ExecuteAsync((IResult)result!);
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_MinistryManager_NoRouteId_Forbids()
    {
        // GET/POST / (list, create) never reach this filter in production (it's only applied to
        // {id:guid} routes) — this proves the filter fails closed if it ever were applied there.
        var context = MakeContext(tokenMinistryId: Guid.NewGuid(), routeId: null);

        var result = await _sut.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ok"));

        result.Should().BeAssignableTo<IResult>();
        var (statusCode, _) = await ExecuteAsync((IResult)result!);
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        using var buffer = new MemoryStream();
        httpContext.Response.Body = buffer;

        await result.ExecuteAsync(httpContext);

        buffer.Position = 0;
        return (httpContext.Response.StatusCode, JsonDocument.Parse(buffer.ToArray()));
    }
}
