using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moto.Core.Performance;

namespace Moto.Editor.Views;

public sealed partial class PerformanceDashboardView : UserControl
{
    private readonly PerformanceProfiler _profiler;
    private readonly DispatcherTimer _refreshTimer;

    public PerformanceDashboardView()
    {
        this.InitializeComponent();
        _profiler = PerformanceProfiler.Instance ?? new PerformanceProfiler();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += (_, _) => RefreshMetrics();
        _refreshTimer.Start();

        RefreshMetrics();
    }

    private void RefreshMetrics()
    {
        var metrics = _profiler.GetMetrics();

        // Métriques clés
        RamValue.Text = metrics.TryGetValue("ram_working_set", out var ram)
            ? $"{ram.Value:F0} MB" : "-- MB";
        CpuValue.Text = metrics.TryGetValue("cpu_usage", out var cpu)
            ? $"{cpu.Value / 100:F1} %" : "-- %";
        ThreadsValue.Text = metrics.TryGetValue("thread_count", out var threads)
            ? $"{threads.Value:F0}" : "--";
        GcValue.Text = metrics.TryGetValue("gc_gen2", out var gc)
            ? $"{gc.Value:F0}" : "--";

        // Liste complète
        MetricsList.ItemsSource = metrics.Values
            .OrderBy(m => m.Name)
            .ToList();
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        var json = _profiler.ExportMetrics();
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"moto-perf-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(path, json);
        StatusText.Text = $"✅ Exporté : {path}";
    }
}
