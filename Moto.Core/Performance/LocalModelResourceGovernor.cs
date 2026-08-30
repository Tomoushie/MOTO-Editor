using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Performance;

/// <summary>
/// Item 50 — Gouverneur coopératif.
/// Détecte les modèles locaux externes (Ollama, llama.cpp, LM Studio...) et,
/// lorsque MOTO AI est inactif, réduit l'empreinte de MOTO pour éviter
/// lag/freeze du PC. Aucune interférence avec XENO ou les panneaux IA.
/// </summary>
public sealed class LocalModelResourceGovernor : IDisposable
{
    private static readonly string[] ExternalModelProcessNames =
    {
        "ollama", "llama-server", "llama.cpp", "llamacpp", "llamafile",
        "vllm", "text-generation", "localai", "lmstudio", "gpt4all", "koboldcpp"
    };

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Timer _scanTimer;
    private readonly object _stateLock = new();

    private ProcessPriorityClass _originalPriority = ProcessPriorityClass.Normal;
    private bool _cooperativeActive;
    private bool _disposed;

    public bool IsCooperativeModeActive { get { lock (_stateLock) return _cooperativeActive; } }
    public event EventHandler<CooperativeModeChangedEventArgs>? CooperativeModeChanged;

    public LocalModelResourceGovernor(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;

        var interval = TimeSpan.FromSeconds(
            Math.Max(2, SettingsCatalog.Ai.Advanced.CooperativeScanIntervalSeconds.Value));

        // Timer debouncé : jamais de travail inutile (règle PerformanceEngine)
        _scanTimer = new Timer(_ => ScanAndAdapt(), null, interval, interval);
        _log.Info("ResourceGovernor", "Initialisé", new { intervalSec = interval.TotalSeconds });
    }

    private void ScanAndAdapt()
    {
        if (_disposed) return;
        try
        {
            if (!SettingsCatalog.Ai.Advanced.CooperativeResourceMode.Value)
            {
                if (IsCooperativeModeActive) ExitCooperativeMode("désactivé par l'utilisateur");
                return;
            }

            bool externalDetected = DetectExternalModelProcess();
            bool motoAiIdle = MotoAiIdleProbe.IsIdle();

            bool shouldEnter = externalDetected && motoAiIdle;
            if (shouldEnter && !IsCooperativeModeActive)
                EnterCooperativeMode();
            else if (!shouldEnter && IsCooperativeModeActive)
                ExitCooperativeMode(externalDetected ? "MOTO AI réactivé" : "modèle externe arrêté");
        }
        catch (Exception ex)
        {
            _log.Error("ResourceGovernor", "Erreur scan", new { ex.Message });
        }
    }

    private bool DetectExternalModelProcess()
    {
        Process[] snapshot = Process.GetProcesses();
        try
        {
            foreach (var proc in snapshot)
            {
                try
                {
                    if (proc.Id == Environment.ProcessId) continue;
                    string name = proc.ProcessName.ToLowerInvariant();
                    if (ExternalModelProcessNames.Any(k => name.Contains(k)))
                    {
                        _log.Debug("ResourceGovernor", "Modèle externe détecté", new { name, proc.Id });
                        return true;
                    }
                }
                catch { /* processus protégé : on ignore */ }
            }
            return false;
        }
        finally
        {
            foreach (var p in snapshot) p.Dispose();
        }
    }

    private void EnterCooperativeMode()
    {
        lock (_stateLock)
        {
            if (_cooperativeActive) return;
            var current = Process.GetCurrentProcess();
            _originalPriority = current.PriorityClass;

            if (SettingsCatalog.Ai.Advanced.LowerPriorityOnCooperative.Value)
                current.PriorityClass = ProcessPriorityClass.BelowNormal;

            PerformanceEngine.EnterEcoMode();

            if (SettingsCatalog.Ai.Advanced.TrimMemoryOnCooperative.Value)
                TrimWorkingSet();

            _cooperativeActive = true;
            _log.Info("ResourceGovernor", "Mode coopératif ACTIVÉ", new { priority = current.PriorityClass });
            CooperativeModeChanged?.Invoke(this, new CooperativeModeChangedEventArgs(true));
        }
    }

    private void ExitCooperativeMode(string reason)
    {
        lock (_stateLock)
        {
            if (!_cooperativeActive) return;
            Process.GetCurrentProcess().PriorityClass = _originalPriority;
            PerformanceEngine.ExitEcoMode();
            _cooperativeActive = false;
            _log.Info("ResourceGovernor", "Mode coopératif DÉSACTIVÉ", new { reason });
            CooperativeModeChanged?.Invoke(this, new CooperativeModeChangedEventArgs(false));
        }
    }

    private void TrimWorkingSet()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
#if WINDOWS
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
#endif
        }
        catch (Exception ex)
        {
            _log.Error("ResourceGovernor", "Trim mémoire échoué", new { ex.Message });
        }
    }

#if WINDOWS
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
#endif

    public void Dispose()
    {
        _disposed = true;
        _scanTimer.Dispose();
        if (_cooperativeActive) ExitCooperativeMode("dispose");
    }
}

public sealed class CooperativeModeChangedEventArgs : EventArgs
{
    public bool IsActive { get; }
    public CooperativeModeChangedEventArgs(bool isActive) => IsActive = isActive;
}

/// <summary>
/// Sonde légère pour savoir si MOTO AI est inactif.
/// Évite tout couplage fort avec MotoAiKernel (règle d'architecture).
/// </summary>
public static class MotoAiIdleProbe
{
    private static int _activeInferences;
    public static void NotifyInferenceStarted() => Interlocked.Increment(ref _activeInferences);
    public static void NotifyInferenceCompleted() => Interlocked.Decrement(ref _activeInferences);
    public static bool IsIdle() => Volatile.Read(ref _activeInferences) == 0;
}
