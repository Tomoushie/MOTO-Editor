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
    private readonly int _coreCount = Environment.ProcessorCount;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleAt;

    public PerformanceStatusBarView(PerformanceProfiler profiler)
    {
        InitializeComponent();
        _profiler = profiler;

        var proc0 = System.Diagnostics.Process.GetCurrentProcess();
        _lastCpuTime = proc0.TotalProcessorTime;
        _lastSampleAt = DateTime.UtcNow;

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
            proc.Refresh();

            double memMb = proc.WorkingSet64 / (1024.0 * 1024.0);
            MemLabel.Text = $"💾 {memMb:F0} Mo";

            // ★ CORRECTION (02/09) : "% modulo 100 du temps CPU total écoulé
            // depuis le lancement" (ancien code) ne représente RIEN — un
            // pourcentage réel se calcule sur un DELTA de temps CPU consommé
            // entre deux instants, divisé par le temps réel écoulé et le
            // nombre de cœurs.
            var now = DateTime.UtcNow;
            var cpuDelta = proc.TotalProcessorTime - _lastCpuTime;
            var wallDelta = now - _lastSampleAt;
            double cpuPercent = wallDelta.TotalMilliseconds > 0
                ? Math.Clamp(cpuDelta.TotalMilliseconds / wallDelta.TotalMilliseconds / _coreCount * 100.0, 0, 100)
                : 0;
            _lastCpuTime = proc.TotalProcessorTime;
            _lastSampleAt = now;
            CpuLabel.Text = $"⚙ {cpuPercent:F0} %";

            // ★ CORRECTION (02/09) : "Mode"/"fps" (ancien code, PerformanceProfiler.
            // GetCurrentMode()/GetEstimatedFps() n'existent pas et n'ont pas
            // d'équivalent réel pour un éditeur XAML) remplacés par 2 vraies
            // mesures déjà échantillonnées par PerformanceProfiler.SampleMetrics.
            var metrics = _profiler.GetMetrics();
            ThreadsLabel.Text = metrics.TryGetValue("thread_count", out var threads)
                ? $"🧵 {threads.Value:F0}"
                : $"🧵 {proc.Threads.Count}"; // repli direct si pas encore échantillonné
            GcLabel.Text = metrics.TryGetValue("gc_gen0", out var gc)
                ? $"♻ {gc.Value:F0}"
                : $"♻ {GC.CollectionCount(0)}";
        }
        catch
        {
            // La StatusBar ne doit jamais crasher l'éditeur.
        }
    }
}
