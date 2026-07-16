namespace Sheba.Shared.Kernel.Exceptions;

/// <summary>
/// Thrown when a domain rule is violated.
/// Maps to HTTP 422 Unprocessable Entity at the API layer.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
