using System;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Licensing;

public enum LicenseState { Trial, Active, Expired, Transferred }

public sealed class LicenseInfo
{
    public string LicenseKey { get; set; } = "";
    public LicenseState State { get; set; } = LicenseState.Trial;
    public DateTime TrialStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationUtc { get; set; }
    public string MachineId { get; set; } = "";
}

/// <summary>
/// Item 88 — Trial license manager avec grace period locale.
/// </summary>
public sealed class TrialLicenseManager
{
    private static readonly string LicensePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", "license.json");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private LicenseInfo _license;

    public LicenseState CurrentState => _license.State;
    public int DaysRemaining => _license.ExpirationUtc.HasValue
        ? (int)(_license.ExpirationUtc.Value - DateTime.UtcNow).TotalDays
        : 0;

    public TrialLicenseManager(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        _license = Load() ?? CreateTrial();
    }

    private LicenseInfo? Load()
    {
        if (!File.Exists(LicensePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<LicenseInfo>(File.ReadAllText(LicensePath));
        }
        catch (Exception ex)
        {
            _log.Error("License", "Échec chargement licence", new { ex.Message });
            return null;
        }
    }

    private LicenseInfo CreateTrial()
    {
        int days = _settings.Shared.Marketplace.TrialDays.Value;
        var license = new LicenseInfo
        {
            State = LicenseState.Trial,
            TrialStartUtc = DateTime.UtcNow,
            ExpirationUtc = DateTime.UtcNow.AddDays(days),
            MachineId = Environment.MachineName
        };
        Save(license);
        _log.Info("License", "Licence trial créée", new { days });
        return license;
    }

    private void Save(LicenseInfo license)
    {
        try
        {
            File.WriteAllText(LicensePath, JsonSerializer.Serialize(license));
        }
        catch (Exception ex)
        {
            _log.Error("License", "Échec sauvegarde licence", new { ex.Message });
        }
    }

    public bool Activate(string key)
    {
        // Validation simplifiée (en production : appel API + signature)
        if (string.IsNullOrWhiteSpace(key) || key.Length < 20) return false;
        _license.LicenseKey = key;
        _license.State = LicenseState.Active;
        _license.ExpirationUtc = null; // licence perpétuelle
        Save(_license);
        _log.Info("License", "Licence activée");
        return true;
    }

    public void CheckExpiration()
    {
        if (_license.State == LicenseState.Trial && _license.ExpirationUtc < DateTime.UtcNow)
        {
            _license.State = LicenseState.Expired;
            Save(_license);
            _log.Warning("License", "Licence expirée");
        }
    }
}
