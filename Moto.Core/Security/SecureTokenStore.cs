// Moto.Core/Security/SecureTokenStore.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Moto.Core.Security
{
    /// <summary>
    /// Stockage sécurisé des tokens API.
    /// Utilise DPAPI sur Windows pour chiffrer les clés.
    /// Sur les autres plateformes, utilise un chiffrement AES local.
    /// </summary>
    public class SecureTokenStore
    {
        private readonly string _storagePath;
        private readonly byte[] _entropy;

        public SecureTokenStore()
        {
            _storagePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor",
                "secure_tokens.dat"
            );

            // Entropie locale pour le chiffrement.
            _entropy = Encoding.UTF8.GetBytes("MOTO_EDITOR_SECURE_STORE_V1");
        }

        /// <summary>
        /// Sauvegarde un token de manière chiffrée.
        /// </summary>
        public void SaveToken(string providerName, string apiKey)
        {
            try
            {
                var tokens = LoadAllTokens();
                tokens[providerName] = Protect(apiKey);

                var json = JsonSerializer.Serialize(tokens);
                var directory = Path.GetDirectoryName(_storagePath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // Le stockage ne doit jamais crasher l'application.
            }
        }

        /// <summary>
        /// Récupère un token déchiffré.
        /// </summary>
        public string GetToken(string providerName)
        {
            try
            {
                var tokens = LoadAllTokens();

                if (tokens.TryGetValue(providerName, out var protectedToken))
                {
                    return Unprotect(protectedToken);
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Supprime un token.
        /// </summary>
        public void DeleteToken(string providerName)
        {
            try
            {
                var tokens = LoadAllTokens();

                if (tokens.Remove(providerName))
                {
                    var json = JsonSerializer.Serialize(tokens);
                    File.WriteAllText(_storagePath, json);
                }
            }
            catch
            {
                // Silencieux.
            }
        }

        /// <summary>
        /// Vérifie si un token existe.
        /// </summary>
        public bool HasToken(string providerName)
        {
            return !string.IsNullOrWhiteSpace(GetToken(providerName));
        }

        private System.Collections.Generic.Dictionary<string, string> LoadAllTokens()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json)
                           ?? new System.Collections.Generic.Dictionary<string, string>();
                }
            }
            catch
            {
                // Fichier corrompu.
            }

            return new System.Collections.Generic.Dictionary<string, string>();
        }

        /// <summary>
        /// Chiffre une chaîne.
        /// Sur Windows : DPAPI.
        /// Sur autres : AES local.
        /// </summary>
        private string Protect(string data)
        {
            if (OperatingSystem.IsWindows())
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                var protectedBytes = ProtectedData.Protect(bytes, _entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedBytes);
            }
            else
            {
                // Fallback AES pour les autres plateformes.
                return AesEncrypt(data);
            }
        }

        /// <summary>
        /// Déchiffre une chaîne.
        /// </summary>
        private string Unprotect(string protectedData)
        {
            if (OperatingSystem.IsWindows())
            {
                var protectedBytes = Convert.FromBase64String(protectedData);
                var bytes = ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            else
            {
                return AesDecrypt(protectedData);
            }
        }

        private string AesEncrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _entropy;
            aes.GenerateIV();

            var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Préfixer l'IV.
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        private string AesDecrypt(string cipherText)
        {
            var fullBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _entropy;

            var iv = new byte[aes.IV.Length];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;

            var cipherBytes = new byte[fullBytes.Length - iv.Length];
            Buffer.BlockCopy(fullBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

            var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
