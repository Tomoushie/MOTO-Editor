// Moto.Core/AI/Embedded/AiModePreset.cs
using System;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Presets de modes IA prédéfinis.
/// Chaque preset force un comportement spécifique du governor.
/// </summary>
public sealed class AiModePreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "⚙️";
    public GovernorMode ForcedMode { get; init; } = GovernorMode.Balanced;
    public ResourceBudget BudgetOverride { get; init; } = ResourceBudget.Balanced;
    public bool AllowAutoUpgrade { get; init; } = false;
    public bool GpuOnly { get; init; } = false;
    public bool NightlyOnly { get; init; } = false;

    public static AiModePreset Eco => new()
    {
        Id = "eco",
        Name = "Mode Eco IA",
        Description = "IA en Idle permanent (1 thread, 512 MB). Idéal pour économiser la batterie.",
        Icon = "🌱",
        ForcedMode = GovernorMode.Idle,
        BudgetOverride = ResourceBudget.Minimal,
        AllowAutoUpgrade = false
    };

    public static AiModePreset Turbo => new()
    {
        Id = "turbo",
        Name = "Mode Turbo IA",
        Description = "IA passe en Performance dès qu'une tâche lourde est détectée.",
        Icon = "🚀",
        ForcedMode = GovernorMode.Performance,
        BudgetOverride = ResourceBudget.Performance,
        AllowAutoUpgrade = true
    };

    public static AiModePreset Neophyte => new()
    {
        Id = "neophyte",
        Name = "Mode Néophyte",
        Description = "IA reste en Balanced, jamais en Performance. Stable et prévisible.",
        Icon = "🎓",
        ForcedMode = GovernorMode.Balanced,
        BudgetOverride = ResourceBudget.Balanced,
        AllowAutoUpgrade = false
    };

    public static AiModePreset Auto => new()
    {
        Id = "auto",
        Name = "Mode Auto",
        Description = "Le governor décide tout selon la charge système.",
        Icon = "🤖",
        ForcedMode = GovernorMode.Balanced, // Mode par défaut, mais le governor peut changer
        BudgetOverride = ResourceBudget.Balanced,
        AllowAutoUpgrade = true
    };

    public static AiModePreset GpuOnly => new()
    {
        Id = "gpu-only",
        Name = "Mode GPU Only",
        Description = "Si GPU disponible, threads CPU = 1. Maximise l'usage GPU.",
        Icon = "🎮",
        ForcedMode = GovernorMode.Balanced,
        BudgetOverride = new ResourceBudget
        {
            MaxThreads = 1,
            MaxMemoryMB = 2048,
            MaxRequestsPerSecond = 15.0,
            MaxBatchSize = 8,
            Priority = ProcessPriority.BelowNormal,
            AllowGpu = true,
            MaxCpuPercent = 20
        },
        GpuOnly = true
    };

    public static AiModePreset NightlyHeavy => new()
    {
        Id = "nightly",
        Name = "Mode Nightly Heavy Tasks",
        Description = "Les tâches lourdes sont exécutées quand l'éditeur est idle (nuit).",
        Icon = "🌙",
        ForcedMode = GovernorMode.Idle,
        BudgetOverride = ResourceBudget.Minimal,
        AllowAutoUpgrade = false,
        NightlyOnly = true
    };

    /// <summary>
    /// Liste de tous les presets disponibles.
    /// </summary>
    public static AiModePreset[] All => new[]
    {
        Eco, Turbo, Neophyte, Auto, GpuOnly, NightlyHeavy
    };

    /// <summary>
    /// Trouve un preset par ID.
    /// </summary>
    public static AiModePreset? FindById(string id)
    {
        foreach (var preset in All)
        {
            if (preset.Id == id) return preset;
        }
        return null;
    }
}
