using System;
using Microsoft.Maui.Controls;
using Moto.Core.Performance;

namespace Moto.Editor.Views;

/// <summary>
/// Item 107 — PerformanceProfiler dashboard in StatusBar.
/// Rafraîchissement throttlé (1 s) pour éviter tout travail inutile.
/// </summary>
public partial class PerformanceStatusBarView : ContentView
{
    private readonly PerformanceProfiler _profiler;
    private readonly System.Timers.Timer _refreshTimer;

    public PerformanceStatusBarView(PerformanceProfiler profiler)
    {
        InitializeComponent();
        _profiler = profiler;

        // Throttle 1 s (cohérent avec la règle "jamais de travail inutile")
        _refreshTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _refreshTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        _refreshTimer.Start();
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            double memMb = proc.WorkingSet64 / (1024.0 * 1024.0);
            MemLabel.Text = $"💾 {memMb:F0} Mo";
            CpuLabel.Text = $"⚙ {proc.TotalProcessorTime.TotalMilliseconds % 100:F0} %";
            ModeLabel.Text = $"⚡ {_profiler.GetCurrentMode()}";
            FpsLabel.Text = $"🎞 {_profiler.GetEstimatedFps():F0} fps";
        }
        catch
        {
            // La StatusBar ne doit jamais crasher l'éditeur.
        }
    }
}
