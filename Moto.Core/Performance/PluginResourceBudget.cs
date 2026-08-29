// Moto.Core/Performance/PluginResourceBudget.cs
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Moto.Core.Performance;

/// <summary>
/// Limite CPU/memory par plugin pour éviter les fuites.
/// </summary>
public sealed class PluginResourceBudget
{
    private readonly ConcurrentDictionary<string, PluginBudget> _budgets = new();

    public static PluginResourceBudget Instance { get; private set; } = null!;

    public PluginResourceBudget()
    {
        Instance = this;
    }

    /// <summary>
    /// Enregistre un plugin avec son budget.
    /// </summary>
    public void RegisterPlugin(string pluginId, int maxCpuPercent = 10, long maxMemoryMB = 100)
    {
        _budgets[pluginId] = new PluginBudget
        {
            PluginId = pluginId,
            MaxCpuPercent = maxCpuPercent,
            MaxMemoryMB = maxMemoryMB
        };
    }

    /// <summary>
    /// Vérifie si un plugin dépasse son budget.
    /// </summary>
    public bool IsOverBudget(string pluginId)
    {
        if (!_budgets.TryGetValue(pluginId, out var budget)) return false;

        // TODO: Mesurer CPU/memory réel du plugin
        // Pour l'instant, retourne false (pas de monitoring réel)
        return false;
    }

    /// <summary>
    /// Tue un plugin qui dépasse son budget.
    /// </summary>
    public void EnforceBudget(string pluginId)
    {
        if (IsOverBudget(pluginId))
        {
            // TODO: Tuer le processus du plugin
        }
    }
}

public class PluginBudget
{
    public string PluginId { get; set; } = "";
    public int MaxCpuPercent { get; set; }
    public long MaxMemoryMB { get; set; }
}
