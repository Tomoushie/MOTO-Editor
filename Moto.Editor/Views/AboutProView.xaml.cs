using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views;

public partial class AboutProView : ContentView
{
    public ObservableCollection<string> Modules { get; } = new();

    public AboutProView()
    {
        InitializeComponent();
        ModulesList.ItemsSource = Modules;
        Fill();
    }

    private void Fill()
    {
        VersionLabel.Text = $"Version {AppInfo.VersionString} (build {AppInfo.BuildString}) — Pro";

        foreach (var m in new[]
        {
            "MotoAiKernel (routeur d'intentions)",
            "CortexEngine (mémoire + style)",
            "NeuralMode (embeddings)",
            "ContextEngine (suggestions senior)",
            "TimeMachineEngine (snapshots)",
            "SpeculativeDecoder (inférence rapide)",
            "PatternDetectorEngine",
            "DocEngine / PlatformEngine"
        })
            Modules.Add("• " + m);

        StatsLabel.Text =
            "Paramètres data-driven : ~300\n" +
            "Modes performance : Eco / Balanced / Turbo / Ultra\n" +
            "IA : 100 % locale, sans cloud";
    }
}
