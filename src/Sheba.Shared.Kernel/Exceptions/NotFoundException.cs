namespace Sheba.Shared.Kernel.Exceptions;

/// <summary>
/// Thrown when an entity or aggregate cannot be located.
/// Maps to HTTP 404 Not Found at the API layer.
/// </summary>
public class NotFoundException : Exception
{
    public string EntityName { get; }
    public object Key { get; }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public NotFoundException(string message)
        : base(message)
    {
        EntityName = string.Empty;
        Key = string.Empty;
    }
}
