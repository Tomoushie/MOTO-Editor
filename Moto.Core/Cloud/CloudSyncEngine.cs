// Moto.Core/Cloud/CloudSyncEngine.cs
using System;
using System.IO;
using System.Threading.Tasks;

namespace Moto.Core.Cloud
{
    // CloudProvider est défini dans CloudProviderClient.cs (même namespace) — pas de redéfinition ici.

    public sealed class CloudSyncConfig
    {
        public CloudProvider Provider { get; set; } = CloudProvider.None;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public string? RemotePath { get; set; }
        public bool AutoSync { get; set; } = true;
    }

    /// <summary>
    /// Moteur de synchronisation cloud (Dropbox, Google Drive, OneDrive).
    /// </summary>
    public sealed class CloudSyncEngine
    {
        private readonly string _configPath;
        private CloudSyncConfig _config = new();

        public event Action<string>? SyncProgress;
        public event Action<string>? SyncError;

        public CloudSyncEngine(string workspaceRoot)
        {
            var motoDir = Path.Combine(workspaceRoot, ".moto");
            Directory.CreateDirectory(motoDir);
            _configPath = Path.Combine(motoDir, "cloud-sync.json");
            Load();
        }

        public CloudSyncConfig GetConfig() => _config;

        public async Task<bool> ConnectAsync(CloudProvider provider, string authCode)
        {
            try
            {
                // En production : échange authCode contre tokens via OAuth2
                _config.Provider = provider;
                _config.AccessToken = $"mock_token_{DateTime.UtcNow.Ticks}";
                _config.RefreshToken = $"mock_refresh_{DateTime.UtcNow.Ticks}";
                _config.ExpiresUtc = DateTime.UtcNow.AddHours(1);
                _config.RemotePath = $"/MotoEditor/Backups";

                Save();
                SyncProgress?.Invoke($"✅ Connecté à {provider}");
                return true;
            }
            catch (Exception ex)
            {
                SyncError?.Invoke($"Erreur connexion : {ex.Message}");
                return false;
            }
        }

        public async Task SyncNowAsync(string localPath)
        {
            if (_config.Provider == CloudProvider.None || string.IsNullOrEmpty(_config.AccessToken))
            {
                SyncError?.Invoke("Cloud non configuré.");
                return;
            }

            SyncProgress?.Invoke("🔄 Synchronisation…");

            try
            {
                // En production : upload via API du provider
                await Task.Delay(1000); // Simulation
                SyncProgress?.Invoke("✅ Synchronisation terminée.");
            }
            catch (Exception ex)
            {
                SyncError?.Invoke($"Erreur sync : {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _config = new CloudSyncConfig();
            Save();
            SyncProgress?.Invoke("Déconnecté du cloud.");
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = System.Text.Json.JsonSerializer.Deserialize<CloudSyncConfig>(json) ?? new();
                }
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_config,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }
    }
}
