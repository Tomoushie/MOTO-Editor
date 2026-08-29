using System;
using System.Collections.Generic;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.DevOps;

/// <summary>
/// Item 97 — Feature flags pour features lourdes : rollout progressif.
/// Les flags sont pilotés par SettingsCatalog (modifiables à distance via settings.json).
/// </summary>
public sealed class FeatureFlagService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Dictionary<string, Func<bool>> _flags = new();

    public FeatureFlagService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Enregistre un flag lié à un SettingItem<bool>.</summary>
    public void RegisterFlag(string name, Func<bool> evaluator)
    {
        _flags[name] = evaluator;
    }

    public bool IsEnabled(string name)
    {
        if (!_settings.Shared.DevOps.FeatureFlagsEnabled.Value) return true;
        return _flags.TryGetValue(name, out var eval) && eval();
    }

    /// <summary>Exemple : lier un flag à un setting.</summary>
    public void BindToSetting(string flagName, SettingItem<bool> setting)
    {
        RegisterFlag(flagName, () => setting.Value);
    }
}
