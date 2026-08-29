// Moto.Core/Performance/MemoryPressureMonitor.cs
using System.Diagnostics;

namespace Moto.Core.Performance;

/// <summary>
/// Surveille la RAM système et bascule automatiquement en Ultra-Lite si < seuil.
/// </summary>
public sealed class MemoryPressureMonitor : IDisposable
{
    private readonly Timer _monitorTimer;
    private readonly UltraLiteMode _ultraLite;
    private readonly long _thresholdBytes;
    private bool _isInPressureMode;

    public event Action<bool>? PressureModeChanged;
    public static MemoryPressureMonitor Instance { get; private set; } = null!;

    public MemoryPressureMonitor(UltraLiteMode ultraLite, long thresholdMB = 2048)
    {
        _ultraLite = ultraLite;
        _thresholdBytes = thresholdMB * 1024 * 1024;
        Instance = this;

        _monitorTimer = new Timer(CheckMemoryPressure, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private void CheckMemoryPressure(object? state)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();

            var ramUsed = process.WorkingSet64;
            var shouldEnterPressureMode = ramUsed > _thresholdBytes;

            if (shouldEnterPressureMode && !_isInPressureMode)
            {
                _isInPressureMode = true;
                _ultraLite.Activate();
                PressureModeChanged?.Invoke(true);
            }
            else if (!shouldEnterPressureMode && _isInPressureMode)
            {
                _isInPressureMode = false;
                _ultraLite.Deactivate();
                PressureModeChanged?.Invoke(false);
            }
        }
        catch
        {
            // Ignore les erreurs de monitoring
        }
    }

    public bool IsInPressureMode => _isInPressureMode;

    public void Dispose()
    {
        _monitorTimer?.Dispose();
    }
}
