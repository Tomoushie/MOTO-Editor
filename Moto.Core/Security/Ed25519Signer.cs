// Moto.Core/Security/Ed25519Signer.cs
// Signature Ed25519 native via NSec.Cryptography (remplace HMAC).
using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

namespace Moto.Core.Security
{
    public sealed class Ed25519KeyPair
    {
        public byte[] PublicKey { get; init; } = Array.Empty<byte>();
        public byte[] PrivateKey { get; init; } = Array.Empty<byte>();
        public string PublicKeyBase64 => Convert.ToBase64String(PublicKey);
        public string PrivateKeyBase64 => Convert.ToBase64String(PrivateKey);
    }

    /// <summary>
    /// Signataire Ed25519 natif utilisant NSec.Cryptography.
    /// Remplace le HMAC pour une vraie signature cryptographique asymétrique.
    /// </summary>
    public sealed class Ed25519Signer
    {
        private readonly ILogger<Ed25519Signer> _logger;
        private readonly string _keysDirectory;
        private readonly SignatureAlgorithm _algorithm = SignatureAlgorithm.Ed25519;

        public Ed25519Signer(ILogger<Ed25519Signer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _keysDirectory = Path.Combine(appData, "MotoEditor", "keys", "ed25519");
            Directory.CreateDirectory(_keysDirectory);
        }

        /// <summary>
        /// Génère une nouvelle paire de clés Ed25519.
        /// </summary>
        public Ed25519KeyPair GenerateKeyPair(string publisherId)
        {
            using var key = Key.Create(_algorithm, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

            var publicKey = key.Export(KeyBlobFormat.PkixPublicKeyText);
            var privateKey = key.Export(KeyBlobFormat.PkixPrivateKeyText);

            // Sauvegarder
            var publicKeyPath = Path.Combine(_keysDirectory, $"{publisherId}.pub");
            var privateKeyPath = Path.Combine(_keysDirectory, $"{publisherId}.key");
            File.WriteAllBytes(publicKeyPath, publicKey);
            File.WriteAllBytes(privateKeyPath, privateKey);

            _logger.LogInformation("[Ed25519] Clés générées pour {Publisher}", publisherId);

            return new Ed25519KeyPair
            {
                PublicKey = publicKey,
                PrivateKey = privateKey
            };
        }

        /// <summary>
        /// Charge une paire de clés existante.
        /// </summary>
        public Ed25519KeyPair? LoadKeyPair(string publisherId)
        {
            var publicKeyPath = Path.Combine(_keysDirectory, $"{publisherId}.pub");
            var privateKeyPath = Path.Combine(_keysDirectory, $"{publisherId}.key");

            if (!File.Exists(publicKeyPath) || !File.Exists(privateKeyPath))
                return null;

            return new Ed25519KeyPair
            {
                PublicKey = File.ReadAllBytes(publicKeyPath),
                PrivateKey = File.ReadAllBytes(privateKeyPath)
            };
        }

        /// <summary>
        /// Signe un contenu avec la clé privée Ed25519.
        /// </summary>
        public string Sign(string content, byte[] privateKeyBytes)
        {
            try
            {
                using var key = Key.Import(_algorithm, privateKeyBytes, KeyBlobFormat.PkixPrivateKeyText);
                var data = Encoding.UTF8.GetBytes(content);
                var signature = _algorithm.Sign(key, data);
                return Convert.ToBase64String(signature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Ed25519] Erreur signature");
                return string.Empty;
            }
        }

        /// <summary>
        /// Vérifie la signature d'un contenu avec la clé publique Ed25519.
        /// </summary>
        public bool Verify(string content, string signatureBase64, byte[] publicKeyBytes)
        {
            try
            {
                var publicKey = PublicKey.Import(_algorithm, publicKeyBytes, KeyBlobFormat.PkixPublicKeyText);
                var data = Encoding.UTF8.GetBytes(content);
                var signature = Convert.FromBase64String(signatureBase64);
                return _algorithm.Verify(publicKey, data, signature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Ed25519] Erreur vérification : {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Signe un pack complet (JSON) et retourne la signature + clé publique.
        /// </summary>
        public (string Signature, string PublicKeyBase64) SignPack(string packJson, string publisherId)
        {
            var keyPair = LoadKeyPair(publisherId) ?? GenerateKeyPair(publisherId);
            var signature = Sign(packJson, keyPair.PrivateKey);
            return (signature, keyPair.PublicKeyBase64);
        }

        /// <summary>
        /// Vérifie un pack signé.
        /// </summary>
        public bool VerifyPack(string packJson, string signatureBase64, string publicKeyBase64)
        {
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            return Verify(packJson, signatureBase64, publicKeyBytes);
        }
    }
}
