namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// One entry in the Identity:SigningCertificates / Identity:EncryptionCertificates config lists
/// (T-SEC-4). Supports either loading from an OS/container certificate store by thumbprint (no
/// secret in config — the preferred production shape) or from a mounted PFX file + password.
///
/// List order is rotation precedence: the loaded certificates are registered with OpenIddict in
/// array order, so during a rotation-by-overlap window the new certificate goes first and the
/// old one stays listed after it, so tokens it already signed keep validating until they expire.
/// </summary>
public sealed class SigningCertificateOptions
{
    public string? Thumbprint { get; set; }
    public string StoreName { get; set; } = "My";
    public string StoreLocation { get; set; } = "CurrentUser";

    public string? Path { get; set; }
    public string? Password { get; set; }
}
