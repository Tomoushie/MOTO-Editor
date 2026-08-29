// Moto.Core/Security/PluginSandboxMinimalService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Plugins;
using Moto.Core.Settings;

namespace Moto.Core.Security;

/// <summary>
/// Item 105 — Plugin sandboxing MINIMAL (P1, expédiable immédiatement).
/// Applique une politique de capabilities par plugin et un timeout d'exécution.
/// Version durcie (process isolé) prévue Sprint 2.
/// </summary>
public sealed class PluginSandboxMinimalService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Dictionary<string, HashSet<SandboxCapability>> _capabilities = new();

    public PluginSandboxMinimalService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Politique par défaut : aucun droit, à accorder explicitement.</summary>
    public void GrantDefault(string pluginId)
    {
        _capabilities[pluginId] = new HashSet<SandboxCapability>(); // deny-by-default
        _log.Info("PluginSandbox", "Politique deny-by-default appliquée", new { pluginId });
    }

    public void Grant(string pluginId, SandboxCapability cap)
    {
        if (!_capabilities.ContainsKey(pluginId)) GrantDefault(pluginId);
        _capabilities[pluginId].Add(cap);
    }

    public bool Can(string pluginId, SandboxCapability cap)
        => _capabilities.TryGetValue(pluginId, out var set) && set.Contains(cap);

    /// <summary>Exécute une action plugin avec timeout + vérification de capability.</summary>
    public async Task<bool> RunGuardedAsync(string pluginId, SandboxCapability required,
                                            Func<Task> action, int timeoutMs = 5000)
    {
        if (!_settings.Shared.Marketplace.PluginSandboxEnabled.Value)
        {
            await action();
            return true;
        }

        if (!Can(pluginId, required))
        {
            _log.Warning("PluginSandbox", "Capability refusée", new { pluginId, required });
            return false;
        }

        using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
        try
        {
            var task = action();
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token));
            if (completed == task)
            {
                await task;
                return true;
            }
            _log.Warning("PluginSandbox", "Timeout plugin", new { pluginId, timeoutMs });
            return false;
        }
        catch (Exception ex)
        {
            _log.Error("PluginSandbox", "Erreur plugin", new { pluginId, ex.Message });
            return false;
        }
    }
}
