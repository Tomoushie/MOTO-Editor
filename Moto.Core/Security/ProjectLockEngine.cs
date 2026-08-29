// Moto.Core/Security/ProjectLockEngine.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Moto.Core.Security
{
    /// <summary>
    /// Verrouillage de projet par mot de passe.
    /// Le hash (SHA256 + sel) est stocké dans .moto/lock.json.
    /// Le mot de passe est demandé à chaque ouverture.
    /// </summary>
    public class ProjectLockEngine
    {
        private class LockData
        {
            public string Salt { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
        }

        public bool IsLocked(string projectPath)
        {
            return File.Exists(GetLockPath(projectPath));
        }

        public void SetPassword(string projectPath, string password)
        {
            var salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            var data = new LockData
            {
                Salt = Convert.ToBase64String(salt),
                Hash = Hash(password, salt)
            };

            var path = GetLockPath(projectPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(data));
        }

        public bool Verify(string projectPath, string password)
        {
            var path = GetLockPath(projectPath);

            if (!File.Exists(path))
            {
                return true; // Non verrouillé.
            }

            try
            {
                var data = JsonSerializer.Deserialize<LockData>(File.ReadAllText(path));
                var salt = Convert.FromBase64String(data.Salt);

                return Hash(password, salt) == data.Hash;
            }
            catch
            {
                return false;
            }
        }

        public void RemovePassword(string projectPath)
        {
            var path = GetLockPath(projectPath);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string GetLockPath(string projectPath)
        {
            return Path.Combine(projectPath, ".moto", "lock.json");
        }

        private string Hash(string password, byte[] salt)
        {
            using var sha = SHA256.Create();

            var bytes = new byte[salt.Length + password.Length];
            Buffer.BlockCopy(salt, 0, bytes, 0, salt.Length);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(password), 0, bytes, salt.Length, password.Length);

            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}
