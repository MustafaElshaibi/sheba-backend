using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Wallet.Domain.Entities;

/// <summary>
/// Credential schema registry — defines what types of Verifiable Credentials exist.
/// Each schema maps to a specific type of credential (identity, birth cert, driver's license, etc.)
/// </summary>
public sealed class CredentialSchema : BaseEntity
{
    public string SchemaUri { get; private set; } = string.Empty;      // e.g. https://sheba.gov.ye/schemas/v1/identity
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string IssuerDid { get; private set; } = string.Empty;
    public string SchemaDefinitionJson { get; private set; } = "{}";   // JSON Schema
    public bool IsActive { get; private set; } = true;

    private CredentialSchema() { }

    public static CredentialSchema Create(
        string schemaUri,
        string name,
        string version,
        string issuerDid,
        string schemaDefinitionJson)
    {
        if (string.IsNullOrWhiteSpace(schemaUri)) throw new DomainException("Schema URI is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Schema name is required.");

        return new CredentialSchema
        {
            SchemaUri = schemaUri,
            Name = name,
            Version = version,
            IssuerDid = issuerDid,
            SchemaDefinitionJson = schemaDefinitionJson
        };
    }
}
