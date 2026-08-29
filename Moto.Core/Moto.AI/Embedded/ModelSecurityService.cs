// Mise à jour partielle de Moto.Core/AI/Embedded/ModelSecurityService.cs
using Moto.Core.Security; // Ajout du namespace

public sealed class ModelSecurityService
{
    private readonly Ed25519Signer _signer;
    // ... (reste du code existant)

    public ModelSecurityService(Ed25519Signer signer, string? modelsDirectory = null)
    {
        _signer = signer;
        // ...
    }

    /// <summary>
    /// Enregistre et SIGNE le manifeste après vérification SHA256.
    /// </summary>
    public void RegisterAndSignManifest(string fileName, string sha256, ModelTier tier, long sizeBytes)
    {
        _manifest ??= new ModelManifest();
        _manifest.Models[fileName] = new ModelEntry
        {
            FileName = fileName,
            Sha256 = sha256,
            Tier = tier.ToString(),
            SizeBytes = sizeBytes,
            VerifiedAt = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(_manifest);
        var signature = _signer.Sign(json); // Utilisation du service v30

        var signedManifest = new { Data = json, Signature = signature };
        File.WriteAllText(ModelPaths.GetManifestPath(), System.Text.Json.JsonSerializer.Serialize(signedManifest));
    }

    /// <summary>
    /// Vérifie l'intégrité ET la signature du manifeste.
    /// </summary>
    public bool IsManifestTrusted()
    {
        if (!File.Exists(ModelPaths.GetManifestPath())) return false;
        try
        {
            var json = File.ReadAllText(ModelPaths.GetManifestPath());
            var signed = System.Text.Json.JsonSerializer.Deserialize<SignedManifest>(json);
            if (signed == null) return false;

            // Vérifie la signature Ed25519
            if (!_signer.Verify(signed.Data, signed.Signature))
                return false;

            _manifest = System.Text.Json.JsonSerializer.Deserialize<ModelManifest>(signed.Data);
            return true;
        }
        catch { return false; }
    }
}

internal class SignedManifest { public string Data { get; set; } = ""; public string Signature { get; set; } = ""; }
