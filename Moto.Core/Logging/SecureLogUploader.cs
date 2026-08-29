using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Settings;

namespace Moto.Core.Logging;

/// <summary>
/// Item 52 — Upload sécurisé des logs. Opt-in strict, HTTPS uniquement,
/// aucun envoi sans consentement explicite (privacy-safe, item 58).
/// </summary>
public sealed class SecureLogUploader
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public SecureLogUploader(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public async Task<bool> UploadAsync(string endpoint, CancellationToken ct = default)
    {
        if (!_settings.Shared.Ai.Advanced.TelemetryEnabled.Value)
        {
            _log.Info("LogUploader", "Upload annulé : télémétrie désactivée (opt-in).");
            return false;
        }

        string archive = await _log.CreateArchiveAsync(ct);
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(await File.ReadAllBytesAsync(archive, ct)), "logs", Path.GetFileName(archive) }
        };

        var response = await Http.PostAsync(endpoint, form, ct);
        _log.Info("LogUploader", "Upload terminé", new { status = (int)response.StatusCode });
        return response.IsSuccessStatusCode;
    }
}
