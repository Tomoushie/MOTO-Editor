// Moto.Core/AI/Embedded/ResourceBudget.cs
using System;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Budget de ressources alloué à l'IA embarquée.
/// Ajusté dynamiquement par le AdaptiveResourceGovernor.
/// </summary>
public sealed class ResourceBudget
{
    /// <summary>Nombre de threads alloués à l'inférence (1-16).</summary>
    public int MaxThreads { get; set; } = 2;

    /// <summary>RAM maximale en MB (512 MB - 8 GB).</summary>
    public long MaxMemoryMB { get; set; } = 1024;

    /// <summary>Fréquence max d'inférence (requêtes/seconde).</summary>
    public double MaxRequestsPerSecond { get; set; } = 5.0;

    /// <summary>Taille max des batches (regroupement de requêtes).</summary>
    public int MaxBatchSize { get; set; } = 1;

    /// <summary>Priorité du processus hôte.</summary>
    public ProcessPriority Priority { get; set; } = ProcessPriority.BelowNormal;

    /// <summary>Timeout d'inférence (ms) avant cancellation.</summary>
    public int InferenceTimeoutMs { get; set; } = 30_000;

    /// <summary>Si true, l'IA peut utiliser le GPU.</summary>
    public bool AllowGpu { get; set; } = true;

    /// <summary>Pourcentage max de CPU système utilisable (0-100).</summary>
    public int MaxCpuPercent { get; set; } = 50;

    /// <summary>Clone le budget (thread-safe).</summary>
    public ResourceBudget Clone() => new()
    {
        MaxThreads = MaxThreads,
        MaxMemoryMB = MaxMemoryMB,
        MaxRequestsPerSecond = MaxRequestsPerSecond,
        MaxBatchSize = MaxBatchSize,
        Priority = Priority,
        InferenceTimeoutMs = InferenceTimeoutMs,
        AllowGpu = AllowGpu,
        MaxCpuPercent = MaxCpuPercent
    };

    /// <summary>Budget minimal (idle).</summary>
    public static ResourceBudget Minimal => new()
    {
        MaxThreads = 1,
        MaxMemoryMB = 512,
        MaxRequestsPerSecond = 1.0,
        MaxBatchSize = 1,
        Priority = ProcessPriority.Idle,
        MaxCpuPercent = 10
    };

    /// <summary>Budget équilibré (usage normal).</summary>
    public static ResourceBudget Balanced => new()
    {
        MaxThreads = 4,
        MaxMemoryMB = 2048,
        MaxRequestsPerSecond = 10.0,
        MaxBatchSize = 4,
        Priority = ProcessPriority.BelowNormal,
        MaxCpuPercent = 50
    };

    /// <summary>Budget performance (tâche lourde).</summary>
    public static ResourceBudget Performance => new()
    {
        MaxThreads = 8,
        MaxMemoryMB = 4096,
        MaxRequestsPerSecond = 20.0,
        MaxBatchSize = 8,
        Priority = ProcessPriority.Normal,
        MaxCpuPercent = 75
    };
}

public enum ProcessPriority
{
    Idle,           // ~5% CPU
    BelowNormal,    // ~25% CPU
    Normal,         // ~50% CPU
    AboveNormal,    // ~75% CPU (rare)
    High            // ~100% CPU (jamais utilisé)
}
