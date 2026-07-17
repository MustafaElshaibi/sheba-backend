using Microsoft.AspNetCore.Http;
using Sheba.Shared.Kernel.Responses;
// Alias needed: this file's own namespace is also "Results", which shadows the unqualified
// Microsoft.AspNetCore.Http.Results static class name inside this namespace block.
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Sheba.Shared.Kernel.Results;

/// <summary>
/// Translates a <see cref="Result{T}"/> into the same JSend wire shape the exception middleware
/// already produces for the equivalent exception type (T-STD-1) — so switching a handler from
/// throwing to returning a Result never changes what an API consumer sees. Endpoints call this
/// once: <c>return result.ToHttpResult();</c> — no per-endpoint branching on success/failure.
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? HttpResults.Ok(result.Value) : ToFailureResult(result.Error!);

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? HttpResults.Ok() : ToFailureResult(result.Error!);

    private static IResult ToFailureResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return HttpResults.Json(JSend.Fail(error.Code, error.Message), statusCode: statusCode);
    }
}
