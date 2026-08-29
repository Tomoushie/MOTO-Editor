// Moto.Editor/Views/AdvancedAiSettingsView.xaml.cs
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moto.Core.AI.Embedded;

namespace Moto.Editor.Views;

public sealed partial class AdvancedAiSettingsView : UserControl
{
    private readonly DualModelIntegration _dual;
    private readonly SpeculativeActivationService _spec;
    private readonly LayeredActivationService _layered;
    private readonly AiOptimizationsBenchmark _benchmark;
    private readonly ModelBundleManager _bundleManager;

    public AdvancedAiSettingsView()
    {
        this.InitializeComponent();

        _dual = App.Services.GetRequiredService<DualModelIntegration>();
        _spec = App.Services.GetRequiredService<SpeculativeActivationService>();
        _layered = App.Services.GetRequiredService<LayeredActivationService>();
        _benchmark = App.Services.GetRequiredService<AiOptimizationsBenchmark>();
        _bundleManager = App.Services.GetRequiredService<ModelBundleManager>();

        Loaded += async (_, _) => await RefreshStatsAsync();
    }

    private async System.Threading.Tasks.Task RefreshStatsAsync()
    {
        // Dual
        var dualStats = _dual.GetStats();
        DualToggle.IsOn = dualStats.IsEnabled;
        DualStatsText.Text = dualStats.IsEnabled
            ? $"✅ Actif · {dualStats.SmallModelRequests} small / {dualStats.LargeModelRequests} large"
            : "⚠️ Inactif";

        // Speculative
        var specStats = _spec.GetStats();
        SpecToggle.IsOn = specStats.IsEnabled;
        SpecStatsText.Text = specStats.IsEnabled
            ? $"✅ Actif · {specStats.AcceptanceRate:P0} acceptance · ×{specStats.SpeedupFactor:F1}"
            : specStats.IsDraftAvailable
                ? "⚠️ Draft disponible mais inactif"
                : "⚠️ Draft non téléchargé";

        // Layered
        var layeredStats = _layered.GetStats();
        LayeredToggle.IsOn = layeredStats.IsEnabled;
        LayeredStatsText.Text = layeredStats.IsEnabled
            ? $"✅ Actif · {layeredStats.LoadedLayers}/{layeredStats.TotalLayers} couches · {layeredStats.ActiveMemoryMB} MB"
            : layeredStats.ShouldActivate
                ? "⚠️ Recommandé (RAM faible ou modèle lourd)"
                : "⚠️ Inactif";
    }

    private async void OnDualToggled(object sender, RoutedEventArgs e)
    {
        if (DualToggle.IsOn) await _dual.EnableAsync();
        await RefreshStatsAsync();
    }

    private async void OnSpecToggled(object sender, RoutedEventArgs e)
    {
        if (SpecToggle.IsOn) await _spec.TryActivateAsync();
        await RefreshStatsAsync();
    }

    private async void OnLayeredToggled(object sender, RoutedEventArgs e)
    {
        if (LayeredToggle.IsOn) await _layered.TryActivateAsync();
        await RefreshStatsAsync();
    }

    private void OnAutoSettingsChanged(object sender, RoutedEventArgs e)
    {
        // Persiste dans SettingsEngine
    }

    private async void OnBenchmarkClicked(object sender, RoutedEventArgs e)
    {
        BenchmarkStatusText.Text = "🏁 Benchmark en cours...";
        var results = await _benchmark.RunFullBenchmarkAsync();
        BenchmarkStatusText.Text = $"✅ Benchmark terminé · Voir AiMonitoringView pour détails";
    }

    private async void OnDownloadAllClicked(object sender, RoutedEventArgs e)
    {
        DownloadStatusText.Text = "⬇️ Téléchargement en cours...";
        await _bundleManager.DownloadAllAsync();
        DownloadStatusText.Text = "✅ Téléchargement terminé";
        await RefreshStatsAsync();
    }
}
