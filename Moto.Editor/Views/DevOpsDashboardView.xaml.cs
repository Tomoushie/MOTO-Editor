using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Moto.Core.DevOps;

namespace Moto.Editor.Views;

/// <summary>
/// Item 101 — DevOps Dashboard : regroupe PerfGate, CrashTriage, FeatureFlags.
/// </summary>
public partial class DevOpsDashboardView : ContentView
{
    private readonly PerfGateService _perfGate;
    private readonly CrashTriageService _crashTriage;
    private readonly FeatureFlagService _flags;

    public ObservableCollection<FeatureFlagItem> FlagItems { get; } = new();

    public DevOpsDashboardView(PerfGateService perfGate,
                               CrashTriageService crashTriage,
                               FeatureFlagService flags)
    {
        InitializeComponent();
        _perfGate = perfGate;
        _crashTriage = crashTriage;
        _flags = flags;

        FlagsList.ItemsSource = FlagItems;
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshCrashes();
        RefreshFlags();
    }

    private async void OnCaptureMetricsClicked(object? sender, EventArgs e)
    {
        var metrics = _perfGate.CaptureCurrentMetrics();
        bool passes = _perfGate.PassesGate(metrics);

        StartupLabel.Text = $"Startup : {metrics.StartupTimeMs:F0} ms";
        MemoryLabel.Text = $"Mémoire : {metrics.PeakMemoryMb:F0} Mo";
        GateStatusLabel.Text = passes ? "✅ Gate : PASS" : "❌ Gate : FAIL";
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private void RefreshCrashes()
    {
        var groups = _crashTriage.GetGroupedCounts();
        int total = 0;
        foreach (var kv in groups) total += kv.Value;
        CrashCountLabel.Text = $"Groupes de crash : {groups.Count} ({total} rapports)";
    }

    private void RefreshFlags()
    {
        FlagItems.Clear();
        foreach (var name in new[] { "feature.command_palette", "feature.proactive_suggestions", "feature.context_engine" })
        {
            FlagItems.Add(new FeatureFlagItem { Name = name, IsEnabled = _flags.IsEnabled(name) });
        }
    }

    private void OnFlagToggled(object? sender, ToggledEventArgs e)
    {
        // Le toggle est piloté par SettingsCatalog ; ici on reflète l'état.
        // Une vraie écriture passerait par SettingsEngine.Shared.
    }
}

public sealed class FeatureFlagItem
{
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; }
}
