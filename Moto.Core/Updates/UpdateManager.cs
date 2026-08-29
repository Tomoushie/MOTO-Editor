// Moto.Core/Updates/UpdateManager.cs
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Updates
{
    public enum UpdateInterval
    {
        Hour1, Hour3, Hour6, Hour12, Hour24, Week1, Month1, Manual
    }

    public sealed class UpdateInfo
    {
        public string Version { get; init; } = string.Empty;
        public string DownloadUrl { get; init; } = string.Empty;
        public string Changelog { get; init; } = string.Empty;
        public DateTime ReleasedUtc { get; init; }
        public bool RequiresRestart { get; init; } = true;
        public long FileSizeBytes { get; init; }
    }

    /// <summary>
    /// Gestionnaire de mises à jour automatique.
    /// Vérifie périodiquement les nouvelles versions et notifie l'UI.
    /// </summary>
    public sealed class UpdateManager : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger<UpdateManager> _logger;
        private readonly string _currentVersion;
        private readonly string _updateUrl;
        private readonly string _settingsPath;
        private Timer? _checkTimer;
        private UpdateInterval _interval = UpdateInterval.Hour1;

        public event Action<UpdateInfo>? UpdateAvailable;
        public event Action<string>? UpdateProgress;
        public event Action? RestartRequired;

        public UpdateManager(
            string currentVersion,
            ILogger<UpdateManager> logger,
            string updateUrl = "https://updates.moto-editor.dev/api/v1/check")
        {
            _currentVersion = currentVersion;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _updateUrl = updateUrl;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsPath = Path.Combine(appData, "MotoEditor", "update-settings.json");

            LoadSettings();
        }

        public UpdateInterval Interval => _interval;
        public string CurrentVersion => _currentVersion;

        public void SetInterval(UpdateInterval interval)
        {
            _interval = interval;
            SaveSettings();
            RestartTimer();
        }

        public void StartAutoCheck()
        {
            RestartTimer();
            _ = CheckForUpdateAsync();
        }

        public void StopAutoCheck()
        {
            _checkTimer?.Dispose();
            _checkTimer = null;
        }

        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                UpdateProgress?.Invoke("Vérification des mises à jour…");

                var url = $"{_updateUrl}?current={_currentVersion}&platform={GetPlatform()}";
                var json = await _http.GetStringAsync(url);
                var update = JsonSerializer.Deserialize<UpdateInfo>(json);

                if (update != null &&
                    string.Compare(update.Version, _currentVersion, StringComparison.Ordinal) > 0)
                {
                    _logger.LogInformation("[Update] MAJ disponible : {Version}", update.Version);
                    UpdateAvailable?.Invoke(update);
                    return update;
                }

                UpdateProgress?.Invoke("Vous utilisez la dernière version.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Update] Erreur de vérification");
                UpdateProgress?.Invoke($"Erreur : {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DownloadAndUpdateAsync(UpdateInfo update, string downloadPath)
        {
            try
            {
                UpdateProgress?.Invoke("Téléchargement…");

                var bytes = await _http.GetByteArrayAsync(update.DownloadUrl);
                await File.WriteAllBytesAsync(downloadPath, bytes);

                UpdateProgress?.Invoke("✅ Téléchargé. Redémarrage requis.");
                RestartRequired?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Update] Erreur de téléchargement");
                UpdateProgress?.Invoke($"Erreur : {ex.Message}");
                return false;
            }
        }

        private void RestartTimer()
        {
            _checkTimer?.Dispose();

            var intervalMs = _interval switch
            {
                UpdateInterval.Hour1 => 3600_000,
                UpdateInterval.Hour3 => 10800_000,
                UpdateInterval.Hour6 => 21600_000,
                UpdateInterval.Hour12 => 43200_000,
                UpdateInterval.Hour24 => 86400_000,
                UpdateInterval.Week1 => 604800_000,
                UpdateInterval.Month1 => 2592000_000,
                _ => Timeout.Infinite
            };

            if (intervalMs != Timeout.Infinite)
            {
                _checkTimer = new Timer(async _ => await CheckForUpdateAsync(),
                    null, intervalMs, intervalMs);
            }
        }

        private static string GetPlatform()
        {
#if WINDOWS
            return "windows";
#elif MACCATALYST
            return "macos";
#else
            return "unknown";
#endif
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<UpdateSettings>(json);
                    if (settings != null)
                        _interval = settings.Interval;
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var settings = new UpdateSettings { Interval = _interval };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        public void Dispose()
        {
            _checkTimer?.Dispose();
            _http.Dispose();
        }

        private sealed class UpdateSettings
        {
            public UpdateInterval Interval { get; set; } = UpdateInterval.Hour1;
        }
    }
}
