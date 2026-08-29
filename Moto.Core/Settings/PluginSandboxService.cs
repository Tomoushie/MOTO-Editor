using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Plugins;
using Moto.Core.Settings;

namespace Moto.Core.Security;

public enum SandboxCapability { FileSystem, Network, UiAccess, ProcessSpawn }

/// <summary>
/// Item 90 (P1) — Sandbox plugins non vérifiés avec capability flags.
/// </summary>
public sealed class PluginSandboxService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Dictionary<string, HashSet<SandboxCapability>> _pluginCapabilities = new();

    public PluginSandboxService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void GrantCapability(string pluginId, SandboxCapability capability)
    {
        if (!_pluginCapabilities.ContainsKey(pluginId))
            _pluginCapabilities[pluginId] = new HashSet<SandboxCapability>();
        _pluginCapabilities[pluginId].Add(capability);
        _log.Info("PluginSandbox", "Capability accordée", new { pluginId, capability });
    }

    public bool HasCapability(string pluginId, SandboxCapability capability)
    {
        return _pluginCapabilities.TryGetValue(pluginId, out var caps) && caps.Contains(capability);
    }

    public async Task ExecuteSandboxedAsync(IMotoPlugin plugin, Func<Task> action)
    {
        if (!_settings.Shared.Marketplace.PluginSandboxEnabled.Value)
        {
            await action();
            return;
        }

        // En production : AppDomain isolé ou process séparé avec capability checks
        try
        {
            _log.Info("PluginSandbox", "Exécution sandboxée", new { plugin.Name });
            await action();
        }
        catch (Exception ex)
        {
            _log.Error("PluginSandbox", "Erreur plugin sandboxé", new { plugin.Name, ex.Message });
            throw;
        }
    }
}
