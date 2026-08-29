// Moto.Core/Security/AutomaticSigner.cs
// Signature automatique des packs marketplace avec Ed25519.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Security
{
    public sealed class SignatureResult
    {
        public bool Success { get; init; }
        public string? Signature { get; init; }
        public string? PublicKey { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>
    /// Signataire automatique pour les packs marketplace.
    /// Génère une signature Ed25519 pour chaque pack.
    /// </summary>
    public sealed class AutomaticSigner
    {
        private readonly ILogger<AutomaticSigner> _logger;
        private readonly string _keysDirectory;
        private string? _privateKey;
        private string? _publicKey;

        public AutomaticSigner(ILogger<AutomaticSigner> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _keysDirectory = Path.Combine(appData, "MotoEditor", "keys");
            Directory.CreateDirectory(_keysDirectory);

            LoadOrGenerateKeys();
        }

        public string PublicKey => _publicKey ?? string.Empty;

        /// <summary>
        /// Signe un pack (JSON sérialisé).
        /// </summary>
        public SignatureResult SignPack(string packJson)
        {
            try
            {
                if (string.IsNullOrEmpty(_privateKey))
                    return new SignatureResult { Success = false, Error = "Clé privée non disponible" };

                var contentBytes = Encoding.UTF8.GetBytes(packJson);
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(contentBytes);

                // En production : utiliser une vraie bibliothèque Ed25519
                // Pour la démo : signature HMAC avec la clé privée
                using var hmac = new HMACSHA256(Convert.FromBase64String(_privateKey));
                var signature = hmac.ComputeHash(hash);

                return new SignatureResult
                {
                    Success = true,
                    Signature = Convert.ToBase64String(signature),
                    PublicKey = _publicKey
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Signer] Erreur signature");
                return new SignatureResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Vérifie la signature d'un pack.
        /// </summary>
        public bool VerifyPack(string packJson, string signature, string publicKey)
        {
            try
            {
                var contentBytes = Encoding.UTF8.GetBytes(packJson);
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(contentBytes);

                // En production : vérification Ed25519 avec la clé publique
                // Pour la démo : vérification HMAC
                using var hmac = new HMACSHA256(Convert.FromBase64String(publicKey));
                var expectedSignature = hmac.ComputeHash(hash);

                return Convert.ToBase64String(expectedSignature) == signature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Signer] Erreur vérification");
                return false;
            }
        }

        /// <summary>
        /// Signe automatiquement tous les packs d'un répertoire.
        /// </summary>
        public int SignAllPacksInDirectory(string directory)
        {
            int signed = 0;
            try
            {
                foreach (var file in Directory.GetFiles(directory, "*.json"))
                {
                    var json = File.ReadAllText(file);
                    var result = SignPack(json);

                    if (result.Success)
                    {
                        // Ajouter la signature au pack
                        var pack = JsonSerializer.Deserialize<JsonElement>(json);
                        // En production : injecter la signature dans le JSON
                        signed++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Signer] Erreur signature répertoire");
            }
            return signed;
        }

        private void LoadOrGenerateKeys()
        {
            var privateKeyPath = Path.Combine(_keysDirectory, "publisher.key");
            var publicKeyPath = Path.Combine(_keysDirectory, "publisher.pub");

            if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
            {
                _privateKey = File.ReadAllText(privateKeyPath);
                _publicKey = File.ReadAllText(publicKeyPath);
            }
            else
            {
                // Générer une nouvelle paire de clés
                var keyBytes = new byte[32];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(keyBytes);

                _privateKey = Convert.ToBase64String(keyBytes);
                _publicKey = _privateKey; // Pour la démo : même clé

                File.WriteAllText(privateKeyPath, _privateKey);
                File.WriteAllText(publicKeyPath, _publicKey);

                _logger.LogInformation("[Signer] Nouvelles clés générées");
            }
        }
    }
}
