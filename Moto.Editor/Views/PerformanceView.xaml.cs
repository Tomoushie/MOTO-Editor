// Moto.Editor/Views/PerformanceView.xaml.cs
using System;
using System.Text;
using Microsoft.Maui.Controls;
using Moto.Core.Performance;

namespace Moto.Editor.Views
{
    public partial class PerformanceView : ContentView
    {
        private readonly PerformanceEngine _engine;

        /// <summary>Déclenché quand l'utilisateur change de mode.</summary>
        public event Action<AiPowerMode> ModeChanged;

        public PerformanceView(PerformanceEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            _engine.ProfileChanged += profile =>
            {
                MainThread.BeginInvokeOnMainThread(() => RenderProfile(profile));
            };

            RenderProfile(_engine.Current);
        }

        private void OnModeClicked(object sender, EventArgs e)
        {
            var mode =
                sender == BtnEco ? AiPowerMode.Eco :
                sender == BtnTurbo ? AiPowerMode.Turbo :
                sender == BtnUltra ? AiPowerMode.Ultra :
                AiPowerMode.Balanced;

            _engine.SetMode(mode);
            FullAutoSwitch.IsToggled = mode == AiPowerMode.Ultra;
            ModeChanged?.Invoke(mode);
        }

        private void OnFullAutoToggled(object sender, ToggledEventArgs e)
        {
            var mode = e.Value ? AiPowerMode.Ultra : AiPowerMode.Balanced;
            _engine.SetMode(mode);
            ModeChanged?.Invoke(mode);
        }

        private void RenderProfile(PerformanceProfile p)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Mode : {p.Label}");
            sb.AppendLine($"{p.Description}");
            sb.AppendLine();
            sb.AppendLine($"Scan interval      : {p.ScanIntervalSec}s");
            sb.AppendLine($"Profondeur analyse : {p.Depth}");
            sb.AppendLine($"Cache max          : {p.CacheMaxEntries} entrées");
            sb.AppendLine($"Debounce prédiction: {p.PredictionDebounceMs}ms");
            sb.AppendLine($"Max suggestions    : {p.MaxSuggestions} (conf ≥ {p.MinConfidence:0.0})");
            sb.AppendLine($"Auto-refactor      : {(p.AutoRefactor ? $"oui / {p.RefactorIntervalMin}min" : "non")}");
            sb.AppendLine($"Auto-doc           : {(p.AutoDoc ? $"oui / {p.DocIntervalSec}s" : "non")}");
            sb.AppendLine($"Auto-linking       : {(p.AutoLinkAuto ? "auto" : "manuel")}");
            sb.AppendLine($"Analyse background : {(p.BackgroundAnalysis ? "oui" : "non")}");
            sb.AppendLine($"Suggestions proact.: {(p.ProactiveSuggestions ? "oui" : "non")}");

            ProfileLabel.Text = sb.ToString();
        }
    }
}
