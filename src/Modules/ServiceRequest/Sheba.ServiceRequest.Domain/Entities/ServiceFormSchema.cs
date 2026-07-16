using Sheba.Shared.Kernel.Entities;

namespace Sheba.ServiceRequest.Domain.Entities;

/// <summary>
/// JSON Schema defining what the citizen must fill in for a service.
/// Stored as JSONB in PostgreSQL.
/// </summary>
public sealed class ServiceFormSchema : BaseEntity
{
    public Guid ServiceId { get; private set; }
    public string SchemaVersion { get; private set; } = "1.0";
    public string FormSchemaJson { get; private set; } = "{}";    // JSON Schema
    public string? UiSchemaJson { get; private set; }              // UI rendering hints
    public string? ValidationRulesJson { get; private set; }       // additional server-side rules

    private ServiceFormSchema() { }

    public static ServiceFormSchema Create(
        Guid serviceId,
        string formSchemaJson,
        string? uiSchemaJson = null,
        string? validationRulesJson = null,
        string schemaVersion = "1.0")
    {
        return new ServiceFormSchema
        {
            ServiceId = serviceId,
            FormSchemaJson = formSchemaJson,
            UiSchemaJson = uiSchemaJson,
            ValidationRulesJson = validationRulesJson,
            SchemaVersion = schemaVersion
        };
    }

    public void Update(string formSchemaJson, string? uiSchemaJson, string? validationRulesJson, string version)
    {
        FormSchemaJson = formSchemaJson;
        UiSchemaJson = uiSchemaJson;
        ValidationRulesJson = validationRulesJson;
        SchemaVersion = version;
        Touch();
    }
}
