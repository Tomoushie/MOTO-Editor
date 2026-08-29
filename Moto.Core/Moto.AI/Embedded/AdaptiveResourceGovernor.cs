// Moto.Core/AI/Embedded/AdaptiveResourceGovernor.cs
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Gouverneur adaptatif : ajuste automatiquement les ressources de l'IA
/// selon la charge système, la queue de requêtes et les besoins réels.
/// </summary>
public sealed class AdaptiveResourceGovernor : IDisposable
{
    private readonly SystemLoadMonitor _loadMonitor;
    private readonly InferenceThrottler _throttler;
    private readonly IsolatedInferenceHost _host;
    private readonly Timer _adjustmentTimer;
    private ResourceBudget _currentBudget;
    private int _consecutiveIdleCycles;
    private int _consecutiveBusyCycles;

    public static AdaptiveResourceGovernor? Instance { get; private set; }

    /// <summary>Budget actuellement appliqué.</summary>
    public ResourceBudget CurrentBudget => _currentBudget.Clone();

    /// <summary>Mode actuel du governor.</summary>
    public GovernorMode Mode { get; private set; } = GovernorMode.Idle;

    /// <summary>Événement quand le mode change.</summary>
    public event Action<GovernorMode>? ModeChanged;

    public AdaptiveResourceGovernor(
        SystemLoadMonitor loadMonitor,
        InferenceThrottler throttler,
        IsolatedInferenceHost host)
    {
        _loadMonitor = loadMonitor;
        _throttler = throttler;
        _host = host;
        _currentBudget = ResourceBudget.Minimal.Clone();
        Instance = this;

        _loadMonitor.LoadChanged += OnLoadChanged;

        // Ajustement toutes les 2 secondes
        _adjustmentTimer = new Timer(AdjustResources, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void OnLoadChanged(SystemLoadSnapshot snapshot)
    {
        // Détection de surcharge système
        if (snapshot.SystemCpuPercent > 80 || snapshot.AvailableRamMB < 1024)
        {
            ApplyBudget(ResourceBudget.Minimal);
            Mode = GovernorMode.Emergency;
            ModeChanged?.Invoke(Mode);
        }
    }

    private void AdjustResources(object? state)
    {
        try
        {
            var queueSize = _throttler.PendingCount;
            var systemLoad = _loadMonitor.OverallLoad;

            // Logique d'ajustement
            if (queueSize == 0 && systemLoad < 0.2)
            {
                _consecutiveIdleCycles++;
                _consecutiveBusyCycles = 0;

                if (_consecutiveIdleCycles > 3 && Mode != GovernorMode.Idle)
                {
                    ApplyBudget(ResourceBudget.Minimal);
                    Mode = GovernorMode.Idle;
                    ModeChanged?.Invoke(Mode);
                }
            }
            else if (queueSize > 3 || systemLoad > 0.6)
            {
                _consecutiveBusyCycles++;
                _consecutiveIdleCycles = 0;

                if (_consecutiveBusyCycles > 2)
                {
                    if (Mode == GovernorMode.Idle)
                    {
                        ApplyBudget(ResourceBudget.Balanced);
                        Mode = GovernorMode.Balanced;
                    }
                    else if (Mode == GovernorMode.Balanced && queueSize > 5)
                    {
                        ApplyBudget(ResourceBudget.Performance);
                        Mode = GovernorMode.Performance;
                    }
                    ModeChanged?.Invoke(Mode);
                }
            }
            else
            {
                _consecutiveIdleCycles = 0;
                _consecutiveBusyCycles = 0;

                if (Mode == GovernorMode.Performance && queueSize < 2)
                {
                    ApplyBudget(ResourceBudget.Balanced);
                    Mode = GovernorMode.Balanced;
                    ModeChanged?.Invoke(Mode);
                }
            }

            // Applique la priorité OS au processus hôte
            ApplyProcessPriority(_currentBudget.Priority);
        }
        catch
        {
            // Ignore les erreurs d'ajustement
        }
    }

    private void ApplyBudget(ResourceBudget newBudget)
    {
        _currentBudget = newBudget.Clone();
        _throttler.UpdateBudget(_currentBudget);

        // Notifie le hôte isolé
        _ = Task.Run(async () =>
        {
            try
            {
                await _host.UpdateBudgetAsync(_currentBudget);
            }
            catch
            {
                // Hôte peut être arrêté
            }
        });
    }

    private void ApplyProcessPriority(ProcessPriority priority)
    {
        try
        {
            var processes = Process.GetProcessesByName("Moto.InferenceHost");
            foreach (var proc in processes)
            {
                proc.PriorityClass = priority switch
                {
                    ProcessPriority.Idle => ProcessPriorityClass.Idle,
                    ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                    ProcessPriority.Normal => ProcessPriorityClass.Normal,
                    ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                    ProcessPriority.High => ProcessPriorityClass.High,
                    _ => ProcessPriorityClass.BelowNormal
                };
            }
        }
        catch
        {
            // Ignore les erreurs de priorité
        }
    }

    /// <summary>
    /// Force un mode spécifique (override manuel).
    /// </summary>
    public void ForceMode(GovernorMode mode)
    {
        var budget = mode switch
        {
            GovernorMode.Idle => ResourceBudget.Minimal,
            GovernorMode.Balanced => ResourceBudget.Balanced,
            GovernorMode.Performance => ResourceBudget.Performance,
            GovernorMode.Emergency => ResourceBudget.Minimal,
            _ => ResourceBudget.Balanced
        };

        ApplyBudget(budget);
        Mode = mode;
        ModeChanged?.Invoke(Mode);
    }

    public void Dispose()
    {
        _adjustmentTimer?.Dispose();
        _loadMonitor.LoadChanged -= OnLoadChanged;
    }
}

public enum GovernorMode
{
    Idle,        // 1 thread, 512 MB, priorité Idle
    Balanced,    // 4 threads, 2 GB, priorité BelowNormal
    Performance, // 8 threads, 4 GB, priorité Normal
    Emergency    // 1 thread, 512 MB, priorité Idle (surcharge système)
}
