// Moto.Core/Security/SignatureVerifier.cs
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Security
{
    public interface ISignatureVerifier
    {
        bool Verify(string content, string signature);
        string Sign(string content, string privateKey);
    }

    /// <summary>
    /// Vérifie les signatures Ed25519 des packs de langues et snippets.
    /// </summary>
    public sealed class SignatureVerifier : ISignatureVerifier
    {
        private readonly ILogger<SignatureVerifier> _logger;
        private readonly Dictionary<string, string> _trustedKeys = new();

        public SignatureVerifier(ILogger<SignatureVerifier> logger)
        {
            _logger = logger;
            LoadTrustedKeys();
        }

        /// <summary>
        /// Vérifie la signature d'un contenu.
        /// </summary>
        public bool Verify(string content, string signature)
        {
            try
            {
                // En production : utiliser une vraie bibliothèque Ed25519
                // Pour la démo : vérification SHA256 simplifiée
                using var sha = SHA256.Create();
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var hash = sha.ComputeHash(contentBytes);
                var expectedSignature = Convert.ToBase64String(hash);

                return signature == expectedSignature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignatureVerifier] Erreur vérification");
                return false;
            }
        }

        /// <summary>
        /// Signe un contenu.
        /// </summary>
        public string Sign(string content, string privateKey)
        {
            try
            {
                using var sha = SHA256.Create();
                var contentBytes = Encoding.UTF8.GetBytes(content);
                var hash = sha.ComputeHash(contentBytes);
                return Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignatureVerifier] Erreur signature");
                return string.Empty;
            }
        }

        private void LoadTrustedKeys()
        {
            // Charger les clés publiques approuvées
            _trustedKeys["moto-official"] = "placeholder-public-key";
        }
    }
}
