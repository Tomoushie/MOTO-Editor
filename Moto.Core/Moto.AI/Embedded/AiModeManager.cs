// Moto.Core/AI/Embedded/AiModeManager.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Gestionnaire des modes IA.
/// Applique les presets au governor et gère les transitions.
/// </summary>
public sealed class AiModeManager
{
    private readonly AdaptiveResourceGovernor _governor;
    private readonly SystemLoadMonitor _loadMonitor;
    private AiModePreset _currentPreset;
    private bool _nightlyTaskScheduled;

    public static AiModeManager? Instance { get; private set; }
    public AiModePreset CurrentPreset => _currentPreset;

    public event Action<AiModePreset>? PresetChanged;
    public event Action<string>? NightlyTaskQueued;

    public AiModeManager(AdaptiveResourceGovernor governor, SystemLoadMonitor loadMonitor)
    {
        _governor = governor;
        _loadMonitor = loadMonitor;
        _currentPreset = AiModePreset.Auto; // Mode par défaut
        Instance = this;
    }

    /// <summary>
    /// Applique un preset.
    /// </summary>
    public void ApplyPreset(AiModePreset preset)
    {
        _currentPreset = preset;

        // Force le mode du governor
        _governor.ForceMode(preset.ForcedMode);

        // Applique le budget override
        if (preset.IsGpuOnly)
        {
            // Active GPU, réduit CPU
            ApplyGpuOnlyMode();
        }

        if (preset.NightlyOnly && !_nightlyTaskScheduled)
        {
            ScheduleNightlyTasks();
        }

        PresetChanged?.Invoke(preset);
    }

    /// <summary>
    /// Applique un preset par ID.
    /// </summary>
    public bool ApplyPresetById(string presetId)
    {
        var preset = AiModePreset.FindById(presetId);
        if (preset == null) return false;
        ApplyPreset(preset);
        return true;
    }

    /// <summary>
    /// Enqueue une tâche lourde pour exécution nocturne.
    /// </summary>
    public void QueueNightlyTask(Func<Task> heavyTask)
    {
        var taskId = Guid.NewGuid().ToString("N")[..8];
        NightlyTaskQueued?.Invoke(taskId);

        _ = Task.Run(async () =>
        {
            // Attend que l'éditeur soit idle
            while (_loadMonitor.SystemCpuPercent > 20 || _loadMonitor.EditorRamMB > 500)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
            }

            // Passe en mode performance temporairement
            var previousPreset = _currentPreset;
            ApplyPreset(AiModePreset.Turbo);

            try
            {
                await heavyTask();
            }
            finally
            {
                // Restaure le preset précédent
                ApplyPreset(previousPreset);
            }
        });
    }

    private void ApplyGpuOnlyMode()
    {
        // Réduit les threads CPU au minimum
        var budget = _currentPreset.BudgetOverride.Clone();
        budget.MaxThreads = 1;
        budget.MaxCpuPercent = 20;
        budget.AllowGpu = true;

        _governor.ForceMode(GovernorMode.Balanced);
    }

    private void ScheduleNightlyTasks()
    {
        _nightlyTaskScheduled = true;
        // Les tâches seront ajoutées via QueueNightlyTask()
    }
}
