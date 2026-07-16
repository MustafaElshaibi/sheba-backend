namespace Sheba.Ministry.Domain.Enums;

/// <summary>
/// How Sheba authenticates TO the ministry's external API system.
/// Each ministry may require a different authentication mechanism.
/// </summary>
public enum MinistryAuthType
{
    /// <summary>OIDC client_credentials against ministry's OIDC server.</summary>
    Oidc = 1,

    /// <summary>OAuth 2.0 client_credentials flow.</summary>
    OAuth2 = 2,

    /// <summary>Static API key in header or query parameter.</summary>
    ApiKey = 3,

    /// <summary>Static bearer token in Authorization header.</summary>
    BearerToken = 4,

    /// <summary>HTTP Basic auth (username + password).</summary>
    BasicAuth = 5,

    /// <summary>SAML service provider integration.</summary>
    Saml = 6,

    /// <summary>Custom plugin adapter.</summary>
    Custom = 7,

    /// <summary>Public API; no auth required.</summary>
    None = 8
}

/// <summary>
/// The type of API endpoint a ministry exposes.
/// </summary>
public enum EndpointType
{
    /// <summary>Read-only data fetch (citizen lookup, record check).</summary>
    DataQuery = 1,

    /// <summary>Triggers an action in ministry system (issue doc, register).</summary>
    ServiceAction = 2,

    /// <summary>Registers Sheba as webhook receiver.</summary>
    WebhookRegister = 3,

    /// <summary>Poll for async operation result.</summary>
    StatusCheck = 4,

    /// <summary>Fetch a document/file.</summary>
    DocumentFetch = 5,

    /// <summary>Health probe.</summary>
    Health = 6
}

/// <summary>
/// Where the API key is placed in the HTTP request.
/// </summary>
public enum ApiKeyPlacement
{
    Header = 1,
    Query = 2,
    Cookie = 3
}
