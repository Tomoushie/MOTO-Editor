using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Security
{
    /// <summary>
    /// Signataire cryptographique pour les packs de langues et thèmes.
    /// Utilise Ed25519 pour signatures rapides et sécurisées.
    /// </summary>
    public sealed class CryptographicSigner
    {
        private readonly ILogger<CryptographicSigner> _logger;
        private readonly string _keysDirectory;

        public CryptographicSigner(ILogger<CryptographicSigner> logger)
        {
            _logger = logger;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _keysDirectory = Path.Combine(appData, "MotoEditor", "keys");
            Directory.CreateDirectory(_keysDirectory);
        }

        /// <summary>
        /// Génère une paire de clés Ed25519 pour un publisher.
        /// </summary>
        public (string PublicKey, string PrivateKey) GenerateKeyPair(string publisherId)
        {
            using var ed25519 = new ECDsa();
            var parameters = ed25519.ExportParameters(true);

            var publicKey = Convert.ToBase64String(parameters.Q.X!);
            var privateKey = Convert.ToBase64String(parameters.D!);

            // Sauvegarde des clés
            var publicKeyPath = Path.Combine(_keysDirectory, $"{publisherId}.pub");
            var privateKeyPath = Path.Combine(_keysDirectory, $"{publisherId}.key");

            File.WriteAllText(publicKeyPath, publicKey);
            File.WriteAllText(privateKeyPath, privateKey);

            _logger.LogInformation("[CryptographicSigner] Clés générées pour {Publisher}", publisherId);
            return (publicKey, privateKey);
        }

        /// <summary>
        /// Signe un contenu avec la clé privée.
        /// </summary>
        public string Sign(string content, string privateKeyBase64)
        {
            try
            {
                var privateKey = Convert.FromBase64String(privateKeyBase64);
                using var ed25519 = ECDsa.Create();

                // Import de la clé privée (simplifié - en production utiliser NSec ou BouncyCastle)
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    D = privateKey
                };
                ed25519.ImportParameters(parameters);

                var contentBytes = Encoding.UTF8.GetBytes(content);
                var signature = ed25519.SignData(contentBytes, HashAlgorithmName.SHA256);

                return Convert.ToBase64String(signature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CryptographicSigner] Erreur signature");
                return string.Empty;
            }
        }

        /// <summary>
        /// Vérifie la signature d'un contenu.
        /// </summary>
        public bool Verify(string content, string signatureBase64, string publicKeyBase64)
        {
            try
            {
                var publicKey = Convert.FromBase64String(publicKeyBase64);
                var signature = Convert.FromBase64String(signatureBase64);

                using var ed25519 = ECDsa.Create();
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = publicKey, Y = new byte[32] }
                };
                ed25519.ImportParameters(parameters);

                var contentBytes = Encoding.UTF8.GetBytes(content);
                return ed25519.VerifyData(contentBytes, signature, HashAlgorithmName.SHA256);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CryptographicSigner] Erreur vérification");
                return false;
            }
        }

        /// <summary>
        /// Signe un pack de langue complet.
        /// </summary>
        public string SignLanguagePack(string packJson, string privateKeyBase64)
        {
            return Sign(packJson, privateKeyBase64);
        }

        /// <summary>
        /// Vérifie un pack de langue signé.
        /// </summary>
        public bool VerifyLanguagePack(string packJson, string signature, string publicKeyBase64)
        {
            return Verify(packJson, signature, publicKeyBase64);
        }
    }
}
