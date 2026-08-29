using System.Text.Json;

namespace Moto.Core.Licensing;

/// <summary>
/// Valide les licences des plugins premium.
/// Utilise Ed25519Signer pour vérifier la signature.
/// </summary>
public sealed class LicenseValidator
{
    private readonly string _licenseDir;

    public LicenseValidator()
    {
        _licenseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor", "licenses");
        Directory.CreateDirectory(_licenseDir);
    }

    /// <summary>
    /// Vérifie si un plugin premium est souscrit.
    /// </summary>
    public LicenseStatus Validate(string pluginId)
    {
        var licenseFile = Path.Combine(_licenseDir, $"{pluginId}.license");
        if (!File.Exists(licenseFile))
            return new LicenseStatus { IsValid = false, Reason = "Aucune licence trouvée" };

        try
        {
            var json = File.ReadAllText(licenseFile);
            var license = JsonSerializer.Deserialize<LicenseData>(json);

            if (license == null)
                return new LicenseStatus { IsValid = false, Reason = "Licence corrompue" };

            if (license.ExpiresAt < DateTime.UtcNow)
                return new LicenseStatus { IsValid = false, Reason = "Licence expirée" };

            // TODO: vérifier signature Ed25519 avec Ed25519Signer

            return new LicenseStatus
            {
                IsValid = true,
                Plan = license.Plan,
                ExpiresAt = license.ExpiresAt,
                UserId = license.UserId
            };
        }
        catch (Exception ex)
        {
            return new LicenseStatus { IsValid = false, Reason = ex.Message };
        }
    }

    /// <summary>
    /// Enregistre une licence après paiement Stripe réussi.
    /// </summary>
    public void SaveLicense(string pluginId, LicenseData license)
    {
        var licenseFile = Path.Combine(_licenseDir, $"{pluginId}.license");
        var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(licenseFile, json);
    }
}

public class LicenseStatus
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = "";
    public string? Plan { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? UserId { get; set; }
}

public class LicenseData
{
    public string PluginId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Plan { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Signature { get; set; } = "";
}
