using System.Text.Json.Serialization;

namespace Sheba.Shared.Kernel.Responses;

/// <summary>
/// Non-generic marker so middleware/filters can detect an already-built JSend envelope
/// without knowing its <c>T</c>.
/// </summary>
public interface IJSendEnvelope;

/// <summary>
/// The JSend envelope (https://github.com/omniti-labs/jsend) every REST endpoint returns.
/// <c>Status</c> is <c>success</c> / <c>fail</c> / <c>error</c>; the HTTP status code carries the
/// transport-level outcome independently. See docs/api-contract.md §2–3.
/// </summary>
public sealed record JSendResponse<T>(
    [property: JsonPropertyOrder(0)] string Status,
    [property: JsonPropertyOrder(3)] T? Data,
    [property: JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null,
    [property: JsonPropertyOrder(2), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Code = null)
    : IJSendEnvelope;

/// <summary>
/// Factories — the only sanctioned way to build envelopes. Endpoints normally never call these
/// (the wrapping filter and exception middleware do); they exist for the rare handler that must
/// shape its envelope explicitly.
/// </summary>
public static class JSend
{
    public static JSendResponse<T> Success<T>(T? data) => new("success", data);

    public static JSendResponse<IDictionary<string, string>> Fail(IDictionary<string, string> data)
        => new("fail", data);

    /// <summary>Single-field convenience overload: <c>JSend.Fail("resource", "…not found.")</c>.</summary>
    public static JSendResponse<IDictionary<string, string>> Fail(string key, string message)
        => new("fail", new Dictionary<string, string> { [key] = message });

    public static JSendResponse<object> Error(string message, int? code = null, object? data = null)
        => new("error", data, message, code);
}
