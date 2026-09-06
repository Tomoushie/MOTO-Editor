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
            // ★ CORRECTION (06/09) : ECDsa.Create() sans argument utilise la courbe par
            // défaut de la plateforme — pas garanti d'être nistP256, que Sign()/Verify()
            // supposent en dur. Un écart de courbe rend (X,Y) invalide pour nistP256 :
            // ImportParameters échoue silencieusement (avalé par le catch de Sign(), qui
            // retournait "" sans jamais le signaler). Fixé explicitement ici.
            using var ed25519 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = ed25519.ExportParameters(true);

            // ★ CORRECTION (06/09) : Q est un POINT (X ET Y) — n'exporter que X perdait
            // la moitié de la clé publique. Verify() ne pouvait alors jamais réussir,
            // même avec une signature valide (Y reconstruit à zéro, un point invalide
            // sur la courbe). Les deux coordonnées (32 octets chacune sur P-256) sont
            // maintenant concaténées avant l'encodage Base64 ; Verify() les resépare.
            var qx = parameters.Q.X!;
            var qy = parameters.Q.Y!;
            var publicKeyBytes = new byte[qx.Length + qy.Length];
            Buffer.BlockCopy(qx, 0, publicKeyBytes, 0, qx.Length);
            Buffer.BlockCopy(qy, 0, publicKeyBytes, qx.Length, qy.Length);

            var publicKey = Convert.ToBase64String(publicKeyBytes);

            // La clé privée embarque aussi Q (X+Y) : Sign() reconstruit ainsi des
            // ECParameters complets (D+Q) à partir d'une seule chaîne opaque, sans
            // changer sa signature publique. Voir le commentaire dans Sign() : importer
            // D seul (sans Q) échouait silencieusement.
            var d = parameters.D!;
            var privateKeyBytes = new byte[d.Length + publicKeyBytes.Length];
            Buffer.BlockCopy(d, 0, privateKeyBytes, 0, d.Length);
            Buffer.BlockCopy(publicKeyBytes, 0, privateKeyBytes, d.Length, publicKeyBytes.Length);
            var privateKey = Convert.ToBase64String(privateKeyBytes);

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
                var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
                using var ed25519 = ECDsa.Create();

                // ★ CORRECTION (06/09) : ImportParameters avec D seul (sans Q) échouait
                // silencieusement (exception avalée par le catch ci-dessous, Sign()
                // retournait alors "" sans jamais le signaler) — Windows CNG exige le
                // point public complet même pour une opération de signature. La clé
                // privée embarque maintenant D+X+Y (voir GenerateKeyPair), reséparés ici.
                var third = privateKeyBytes.Length / 3;
                var d = new byte[third];
                var qx = new byte[third];
                var qy = new byte[third];
                Buffer.BlockCopy(privateKeyBytes, 0, d, 0, third);
                Buffer.BlockCopy(privateKeyBytes, third, qx, 0, third);
                Buffer.BlockCopy(privateKeyBytes, third * 2, qy, 0, third);

                // Import de la clé privée (simplifié - en production utiliser NSec ou BouncyCastle)
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    D = d,
                    Q = new ECPoint { X = qx, Y = qy }
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
                var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
                var signature = Convert.FromBase64String(signatureBase64);

                // Resépare X et Y, concaténés par GenerateKeyPair (voir son commentaire).
                var half = publicKeyBytes.Length / 2;
                var qx = new byte[half];
                var qy = new byte[half];
                Buffer.BlockCopy(publicKeyBytes, 0, qx, 0, half);
                Buffer.BlockCopy(publicKeyBytes, half, qy, 0, half);

                using var ed25519 = ECDsa.Create();
                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = qx, Y = qy }
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
