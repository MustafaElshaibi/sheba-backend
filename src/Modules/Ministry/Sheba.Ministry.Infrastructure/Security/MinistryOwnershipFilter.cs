using Microsoft.AspNetCore.Http;
using Sheba.Shared.Kernel.Responses;
using Sheba.Shared.Kernel.Security;

namespace Sheba.Ministry.Infrastructure.Security;

/// <summary>
/// Enforces per-ministry ownership (T-AUTH-1) on any route shaped .../{id:guid}/... where the
/// route's "id" IS the ministry being acted on. A MinistryManager's token carries a ministry_id
/// claim (set at admin-account creation, T-AUTH-1); a request whose route id doesn't match gets
/// 403 before the handler runs. SuperAdmin tokens carry no ministry_id claim, so
/// GetMinistryId() returns null and every ministry is reachable — the intended "sees everything"
/// behavior, not a bypass.
///
/// Only applies to routes with an "id" route value; GET / (list) and POST / (create) have none
/// and are deliberately unrestricted here — listing ministry names isn't the sensitive-data risk
/// the known-issues gap described, and who may CREATE a ministry is a role question (already
/// gated by the MinistryManager policy), not an ownership one.
/// </summary>
public sealed class MinistryOwnershipFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var scopedMinistryId = context.HttpContext.User.GetMinistryId();
        if (scopedMinistryId is { } scoped)
        {
            var routeMinistryId = context.HttpContext.Request.RouteValues.TryGetValue("id", out var raw)
                && Guid.TryParse(raw?.ToString(), out var parsed)
                ? parsed
                : (Guid?)null;

            if (routeMinistryId != scoped)
            {
                // JSendHttpResult, not Results.Json — writes the body directly with a static
                // JsonSerializerOptions (no HttpContext.RequestServices lookup), the same
                // DI-independent path ExceptionHandlerMiddleware and JSendWrappingFilter itself
                // already use for every other JSend error response in the app.
                return new JSendHttpResult(
                    JSend.Fail("permissions", "You do not have permission to access this ministry."),
                    StatusCodes.Status403Forbidden);
            }
        }

        return await next(context);
    }
}
