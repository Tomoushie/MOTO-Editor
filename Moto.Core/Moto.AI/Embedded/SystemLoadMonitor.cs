// Moto.Core/AI/Embedded/SystemLoadMonitor.cs
using System;
using System.Diagnostics;
using System.Threading;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Surveille la charge système (CPU, RAM, éditeur) en temps réel.
/// Échantillonne toutes les 500 ms pour réactivité.
/// </summary>
public sealed class SystemLoadMonitor : IDisposable
{
    private readonly Timer _samplingTimer;
    private readonly Process _currentProcess;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;

    public static SystemLoadMonitor? Instance { get; private set; }

    /// <summary>Utilisation CPU système (0-100%).</summary>
    public double SystemCpuPercent { get; private set; }

    /// <summary>RAM système disponible en MB.</summary>
    public long AvailableRamMB { get; private set; }

    /// <summary>RAM utilisée par l'éditeur en MB.</summary>
    public long EditorRamMB { get; private set; }

    /// <summary>RAM utilisée par le processus IA en MB.</summary>
    public long InferenceHostRamMB { get; set; }

    /// <summary>Charge globale (0-1, combiné CPU+RAM).</summary>
    public double OverallLoad => (SystemCpuPercent / 100.0 + InferenceHostRamMB / 8192.0) / 2.0;

    /// <summary>Événement déclenché quand la charge change significativement.</summary>
    public event Action<SystemLoadSnapshot>? LoadChanged;

    public SystemLoadMonitor()
    {
        _currentProcess = Process.GetCurrentProcess();
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;
        Instance = this;

        _samplingTimer = new Timer(SampleLoad, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    private void SampleLoad(object? state)
    {
        try
        {
            _currentProcess.Refresh();

            // CPU éditeur
            var currentCpuTime = _currentProcess.TotalProcessorTime;
            var elapsedTime = DateTime.UtcNow - _lastSampleTime;
            var cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var cpuPercent = cpuUsedMs / (elapsedTime.TotalMilliseconds * Environment.ProcessorCount) * 100;

            SystemCpuPercent = Math.Clamp(cpuPercent, 0, 100);
            EditorRamMB = _currentProcess.WorkingSet64 / 1024 / 1024;

            // RAM disponible (estimation via GC)
            var gcInfo = GC.GetGCMemoryInfo();
            AvailableRamMB = (gcInfo.TotalAvailableMemoryBytes - gcInfo.MemoryLoadBytes) / 1024 / 1024;

            _lastCpuTime = currentCpuTime;
            _lastSampleTime = DateTime.UtcNow;

            // Notifie les changements
            LoadChanged?.Invoke(new SystemLoadSnapshot
            {
                SystemCpuPercent = SystemCpuPercent,
                AvailableRamMB = AvailableRamMB,
                EditorRamMB = EditorRamMB,
                InferenceHostRamMB = InferenceHostRamMB,
                OverallLoad = OverallLoad
            });
        }
        catch
        {
            // Ignore les erreurs de sampling
        }
    }

    public void Dispose()
    {
        _samplingTimer?.Dispose();
    }
}

public class SystemLoadSnapshot
{
    public double SystemCpuPercent { get; set; }
    public long AvailableRamMB { get; set; }
    public long EditorRamMB { get; set; }
    public long InferenceHostRamMB { get; set; }
    public double OverallLoad { get; set; }
}
