namespace Sheba.Shared.Kernel.Exceptions;

/// <summary>
/// Thrown by ValidationBehavior when FluentValidation finds errors.
/// Maps to HTTP 400 Bad Request with a structured error dictionary at the API layer.
/// </summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : base($"Validation error on '{field}': {message}")
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };
    }
}
