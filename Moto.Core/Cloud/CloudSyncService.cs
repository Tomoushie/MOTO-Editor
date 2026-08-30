// Moto.Core/Cloud/CloudSyncService.cs
// Service de synchronisation automatique des fichiers du workspace.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Cloud
{
    public sealed class CloudSyncServiceConfig
    {
        public CloudProvider Provider { get; set; } = CloudProvider.None;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public string RemotePath { get; set; } = "/MotoEditor/Backups";
        public bool AutoSync { get; set; } = true;
        public int IntervalMinutes { get; set; } = 30;
        public List<string> ExcludedExtensions { get; set; } = new() { ".dll", ".exe", ".pdb" };
    }

    /// <summary>
    /// Service de synchronisation cloud avec planification automatique.
    /// </summary>
    public sealed class CloudSyncService : IDisposable
    {
        private readonly ILogger<CloudSyncService> _logger;
        private readonly CloudProviderClient _client;
        private readonly string _configPath;
        private CloudSyncServiceConfig _config = new();
        private Timer? _syncTimer;
        private readonly SemaphoreSlim _syncGate = new(1, 1);

        public event Action<string>? SyncProgress;
        public event Action<bool, string>? SyncCompleted;

        public CloudSyncService(ILogger<CloudSyncService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = new CloudProviderClient(CloudProvider.None, logger);

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _configPath = Path.Combine(appData, "MotoEditor", "cloud-sync.json");
            LoadConfig();
        }

        public CloudSyncServiceConfig GetConfig() => _config;

        /// <summary>
        /// Configure le provider cloud et démarre la synchronisation.
        /// </summary>
        public async Task<bool> ConfigureAsync(
            CloudProvider provider,
            string accessToken,
            string? refreshToken,
            CancellationToken ct = default)
        {
            _config.Provider = provider;
            _config.AccessToken = accessToken;
            _config.RefreshToken = refreshToken;

            _client.SetTokens(accessToken, refreshToken);
            SaveConfig();

            if (_config.AutoSync)
                StartAutoSync();

            SyncProgress?.Invoke($"✅ Configuré pour {provider}");
            return true;
        }

        /// <summary>
        /// Synchronise le workspace vers le cloud.
        /// </summary>
        public async Task SyncWorkspaceAsync(string workspaceRoot, CancellationToken ct = default)
        {
            if (!_client.IsAuthenticated)
            {
                SyncCompleted?.Invoke(false, "Non authentifié");
                return;
            }

            await _syncGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                SyncProgress?.Invoke("🔄 Synchronisation…");

                var files = Directory.GetFiles(workspaceRoot, "*.*", SearchOption.AllDirectories)
                    .Where(f => !IsExcluded(f))
                    .Take(100) // Limite pour éviter les uploads massifs
                    .ToList();

                int uploaded = 0, failed = 0;

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
                    var remotePath = $"{_config.RemotePath}/{relativePath}";

                    var success = await _client.UploadAsync(file, remotePath, ct).ConfigureAwait(false);
                    if (success) uploaded++;
                    else failed++;

                    SyncProgress?.Invoke($"📤 {uploaded + failed}/{files.Count} fichiers…");
                }

                SaveConfig();
                SyncCompleted?.Invoke(true, $"✅ {uploaded} uploadés, {failed} échecs");
                _logger.LogInformation("[CloudSync] Terminé : {Uploaded} OK, {Failed} échecs", uploaded, failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CloudSync] Erreur synchronisation");
                SyncCompleted?.Invoke(false, $"❌ {ex.Message}");
            }
            finally
            {
                _syncGate.Release();
            }
        }

        /// <summary>
        /// Démarre la synchronisation automatique.
        /// </summary>
        public void StartAutoSync()
        {
            _syncTimer?.Dispose();
            var interval = TimeSpan.FromMinutes(_config.IntervalMinutes);
            _syncTimer = new Timer(async _ =>
            {
                // Le workspace actuel doit être fourni par l'UI
                // Pour la démo : on log seulement
                _logger.LogInformation("[CloudSync] Tick auto-sync");
            }, null, interval, interval);
        }

        public void StopAutoSync()
        {
            _syncTimer?.Dispose();
            _syncTimer = null;
        }

        public void Disconnect()
        {
            _config = new CloudSyncServiceConfig();
            StopAutoSync();
            SaveConfig();
            SyncProgress?.Invoke("Déconnecté du cloud");
        }

        private bool IsExcluded(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return _config.ExcludedExtensions.Contains(ext) ||
                   filePath.Contains("bin") || filePath.Contains("obj") ||
                   filePath.Contains(".git") || filePath.Contains("node_modules");
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = System.Text.Json.JsonSerializer.Deserialize<CloudSyncServiceConfig>(json) ?? new();

                    if (!string.IsNullOrEmpty(_config.AccessToken))
                        _client.SetTokens(_config.AccessToken, _config.RefreshToken);
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var json = System.Text.Json.JsonSerializer.Serialize(_config,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public void Dispose()
        {
            _syncTimer?.Dispose();
            _syncGate.Dispose();
            _client.Dispose();
        }
    }
}
