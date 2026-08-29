using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Editor.Services;

public sealed class UpdateInfo
{
    public bool IsAvailable { get; init; }
    public string Version { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string Notes { get; init; } = "";
}

/// <summary>
/// Mise à jour automatique : vérifie la dernière release, télécharge le setup,
/// délègue l'application à l'installateur (qui réutilise PayloadExtractor).
/// </summary>
public sealed class AutoUpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public AutoUpdateService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Vérifie si une mise à jour est disponible.</summary>
    public async Task<UpdateInfo> CheckAsync()
    {
        if (!_settings.Shared.Editor.Update.AutoCheck.Value)
            return new UpdateInfo { IsAvailable = false };

        try
        {
            string url = _settings.Shared.Editor.Update.ReleaseUrl.Value;
            string json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            string tag = doc.RootElement.TryGetProperty("tag_name", out var t)
                ? t.GetString() ?? "" : "";

            string download = "";
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string name = a.GetProperty("name").GetString() ?? "";
                    if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        download = a.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
            }

            bool available = !string.IsNullOrEmpty(download) && IsNewer(tag, AppInfo.VersionString);
            _log.Info("AutoUpdate", "Vérification", new { tag, available });

            return new UpdateInfo
            {
                IsAvailable = available,
                Version = tag,
                DownloadUrl = download,
                Notes = doc.RootElement.TryGetProperty("body", out var b) ? b.GetString() ?? "" : ""
            };
        }
        catch (Exception ex)
        {
            _log.Error("AutoUpdate", "Échec vérification", new { ex.Message });
            return new UpdateInfo { IsAvailable = false };
        }
    }

    /// <summary>Télécharge le setup et délègue la mise à jour à l'installateur.</summary>
    public async Task ApplyAsync(UpdateInfo info)
    {
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), "moto-update");
            Directory.CreateDirectory(dir);
            string setupPath = Path.Combine(dir, "MotoEditor-Setup.exe");

            _log.Info("AutoUpdate", "Téléchargement", new { info.DownloadUrl });
            using (var resp = await Http.GetAsync(info.DownloadUrl))
            using (var fs = File.Create(setupPath))
                await resp.Content.CopyToAsync(fs);

            // Lance l'installateur en mode update, attend ce processus, puis relance
            var psi = new System.Diagnostics.ProcessStartInfo(setupPath,
                $"--update --silent --target \"{AppContext.BaseDirectory}\" " +
                $"--wait-for {Environment.ProcessId} --relaunch \"{Environment.ProcessPath}\"")
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            _log.Info("AutoUpdate", "Délégation à l'installateur, fermeture de l'éditeur");
            Application.Current?.Quit();
        }
        catch (Exception ex)
        {
            _log.Error("AutoUpdate", "Échec application", new { ex.Message });
        }
    }

    private static bool IsNewer(string tag, string current)
    {
        if (!TryParseVersion(tag, out var newV) || !TryParseVersion(current, out var curV))
            return false;
        return newV > curV;
    }

    private static bool TryParseVersion(string s, out Version v)
        => Version.TryParse(s.TrimStart('v', 'V'), out v!);
}
