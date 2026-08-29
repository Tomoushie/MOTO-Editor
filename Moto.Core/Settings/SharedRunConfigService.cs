using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public sealed class RunConfiguration
{
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
}

/// <summary>
/// Idée "Shared run configurations" (P2) — launch configs partageables par projet.
/// Stocké dans .moto/run-configs.json (aucun format propriétaire).
/// </summary>
public sealed class SharedRunConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public SharedRunConfigService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void Save(IReadOnlyList<RunConfiguration> configs)
    {
        if (!_settings.Shared.Collab.SharedRunConfigsEnabled.Value) return;
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(Path.Combine(ConfigDir, "run-configs.json"),
                          JsonSerializer.Serialize(configs));
        _log.Info("SharedRunConfig", "Configs sauvegardées", new { count = configs.Count });
    }

    public IReadOnlyList<RunConfiguration> Load()
    {
        var path = Path.Combine(ConfigDir, "run-configs.json");
        if (!File.Exists(path)) return Array.Empty<RunConfiguration>();
        try
        {
            return JsonSerializer.Deserialize<List<RunConfiguration>>(File.ReadAllText(path))
                   ?? new List<RunConfiguration>();
        }
        catch (Exception ex)
        {
            _log.Error("SharedRunConfig", "Échec chargement", new { ex.Message });
            return Array.Empty<RunConfiguration>();
        }
    }
}
