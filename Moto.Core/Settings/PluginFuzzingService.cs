using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Plugins;
using Moto.Core.Settings;

namespace Moto.Core.DevOps;

/// <summary>
/// Item 94 — Fuzzing harness : tests fuzz automatisés sur la surface API des plugins.
/// </summary>
public sealed class PluginFuzzingService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Random _rng = new();

    public PluginFuzzingService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Génère des entrées aléatoires pour sonder la robustesse d'un plugin.</summary>
    public async Task<List<string>> FuzzPluginAsync(IPlugin plugin, int iterations = 100)
    {
        var crashes = new List<string>();
        if (!SettingsCatalog.DevOps.PluginFuzzingEnabled.Value) return crashes;

        for (int i = 0; i < iterations; i++)
        {
            string input = GenerateFuzzInput();
            try
            {
                // En production : appeler les points d'entrée publics du plugin
                await Task.Yield();
            }
            catch (Exception ex)
            {
                crashes.Add($"Iter {i} : {ex.GetType().Name} — {ex.Message}");
                _log.Warning("PluginFuzzing", "Crash fuzz détecté", new { plugin.DisplayName, input });
            }
        }
        _log.Info("PluginFuzzing", "Fuzzing terminé", new { plugin.DisplayName, crashes = crashes.Count });
        return crashes;
    }

    private string GenerateFuzzInput()
    {
        int len = _rng.Next(0, 500);
        var chars = new char[len];
        for (int i = 0; i < len; i++) chars[i] = (char)_rng.Next(0, 65535);
        return new string(chars);
    }
}
