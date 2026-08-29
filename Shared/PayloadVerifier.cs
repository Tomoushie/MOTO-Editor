using System;
using System.IO;
using System.Text.Json;

namespace Moto.Shared;

/// <summary>Vérifie hash SHA256 + signature Ed25519 d'un payload/manifeste.</summary>
public static class PayloadVerifier
{
    /// <summary>pubKeyHex : clé publique Ed25519 embarquée (hex 64 chars).</summary>
    public static bool VerifyPayload(string payloadPath, string manifestPath, string pubKeyHex)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(manifestPath))!;

            // 1. Hash SHA256 du payload
            if (!string.Equals(Sha256Helper.ComputeFile(payloadPath), manifest.PayloadSha256,
                               StringComparison.OrdinalIgnoreCase))
                return false;

            // 2. Signature Ed25519 du hash
            var pub = Sha256Helper.FromHex(pubKeyHex);
            var sig = Sha256Helper.FromHex(manifest.Signature);
            var msg = System.Text.Encoding.UTF8.GetBytes(manifest.PayloadSha256);
            return Ed25519.Verify(msg, sig, pub);
        }
        catch { return false; }
    }
}
