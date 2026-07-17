using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// Loads 0..N X509Certificate2 instances from a configuration section (T-SEC-4). Returns an
/// empty list when the section is absent or empty — IdentityModule falls back to the ephemeral
/// development certificate in that case, so an unconfigured deployment (every environment today)
/// behaves exactly as it did before this feature existed.
/// </summary>
public static class SigningCertificateLoader
{
    public static IReadOnlyList<X509Certificate2> Load(IConfiguration configuration, string sectionKey)
    {
        var entries = configuration.GetSection(sectionKey).Get<List<SigningCertificateOptions>>();
        if (entries is null || entries.Count == 0)
            return Array.Empty<X509Certificate2>();

        return entries.Select(entry => LoadOne(entry, sectionKey)).ToList();
    }

    private static X509Certificate2 LoadOne(SigningCertificateOptions entry, string sectionKey)
    {
        if (!string.IsNullOrWhiteSpace(entry.Path))
        {
            if (entry.Password is null)
                throw new InvalidOperationException(
                    $"{sectionKey}: entry with Path '{entry.Path}' needs a Password (PKCS#12 " +
                    "requires one, even if empty) — set it via an environment-injected secret, " +
                    "never a literal in appsettings*.json.");

            return X509CertificateLoader.LoadPkcs12FromFile(entry.Path, entry.Password);
        }

        if (!string.IsNullOrWhiteSpace(entry.Thumbprint))
        {
            var storeName = Enum.Parse<StoreName>(entry.StoreName, ignoreCase: true);
            var storeLocation = Enum.Parse<StoreLocation>(entry.StoreLocation, ignoreCase: true);

            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            var matches = store.Certificates.Find(X509FindType.FindByThumbprint, entry.Thumbprint, validOnly: false);
            if (matches.Count == 0)
                throw new InvalidOperationException(
                    $"{sectionKey}: no certificate with thumbprint '{entry.Thumbprint}' found in " +
                    $"{storeLocation}/{storeName}.");

            return matches[0];
        }

        throw new InvalidOperationException(
            $"{sectionKey}: each entry needs either Path (+ Password) or Thumbprint.");
    }
}
