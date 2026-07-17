namespace Sheba.Shared.Kernel.Results;

/// <summary>
/// How an <see cref="Error"/> should be rendered at the transport boundary — mirrors the
/// exception-to-HTTP-status mapping in <c>ExceptionHandlerMiddleware</c> so a handler's choice
/// between throwing and returning a <see cref="Result{T}"/> never changes the wire contract.
/// </summary>
public enum ErrorType
{
    /// <summary>Expected, caller-fixable input problem. → 400.</summary>
    Validation,

    /// <summary>Referenced resource does not exist. → 404.</summary>
    NotFound,

    /// <summary>Request is well-formed but violates a business/domain rule given current state. → 422.</summary>
    Conflict,

    /// <summary>Caller is authenticated but not allowed to perform this action. → 403.</summary>
    Unauthorized,

    /// <summary>Anything else expected-but-not-otherwise-categorized. → 400.</summary>
    Failure
}

/// <summary>
/// An expected failure (T-STD-1) — validation problems, not-found, and business-rule violations
/// that a handler wants to *return* rather than throw. <c>Code</c> becomes the JSend `fail` data
/// key (e.g. <c>"otp"</c>, <c>"domain"</c>, <c>"resource"</c>); <c>Message</c> is the human-facing
/// text. Reserve exceptions (<c>DomainException</c> et al.) for paths that still throw — a module
/// should pick one style and not mix them (see docs/sheba.md §15).
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
