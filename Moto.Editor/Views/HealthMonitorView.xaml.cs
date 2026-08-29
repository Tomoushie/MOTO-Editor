// Moto.Editor/Views/HealthMonitorView.xaml.cs (v2 — ajoute les 3 métriques)
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Internal;

namespace Moto.Editor.Views
{
    public partial class HealthMonitorView : ContentView
    {
        private readonly HealthMonitorEngine _engine = new();
        private readonly HealthMetricsEngine _metrics = new();
        private readonly ProjectUnderstandingEngine _understanding = new();

        public HealthMonitorView()
        {
            InitializeComponent();
        }

        public async Task AnalyzeAsync(string workspacePath)
        {
            var (report, metrics) = await Task.Run(() =>
            {
                var map = _understanding.BuildMap(workspacePath);
                return (_engine.Analyze(map), _metrics.Analyze(map));
            });

            // Score global existant
            ScoreLabel.Text = $"{report.GlobalScore}/100";
            ScoreBar.Progress = report.GlobalScore / 100.0;
            ScoreBar.ProgressColor = report.GlobalScore >= 80 ? Colors.LimeGreen
                                   : report.GlobalScore >= 50 ? Colors.Orange
                                   : Colors.Red;

            // NOUVEAU : cohérence / architecture / complexité
            MetricsLabel.Text =
                $"Cohérence {metrics.ConsistencyScore}/100 · " +
                $"Architecture {metrics.ArchitectureScore}/100 · " +
                $"Complexité {metrics.ComplexityScore}/100 ({metrics.AvgCyclomatic:0.0} branches/méthode)";

            IssueList.ItemsSource = report.Issues;
        }
    }
}
// → Dans HealthMonitorView.xaml, ajouter sous ScoreBar :
//   <Label x:Name="MetricsLabel" FontSize="10" Opacity="0.7" />
