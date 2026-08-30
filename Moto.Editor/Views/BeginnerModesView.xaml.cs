// Moto.Editor/Views/BeginnerModesView.xaml.cs
using System;
using System.Linq;
using System.Text;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Beginner;
using Moto.Core.AI.Builders;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;
using Moto.Core.Integration;

namespace Moto.Editor.Views
{
    public enum BeginnerMode { Normal, Explain, Tutor, NoCode, Pair }

    public partial class BeginnerModesView : ContentView
    {
        private readonly ExplainEverythingEngine _explain = new();
        private readonly AiTutorEngine _tutor = new();
        private readonly PairProgrammingEngine _pair = new();
        private NoCodeOrchestrator _noCode;

        private ProjectMap _map;
        private string _activeFile;
        private TutorMessage _currentTutorMessage;

        /// <summary>Mode actif, consommé par MainPage (ex : ghost du Pair).</summary>
        public BeginnerMode CurrentMode { get; private set; } = BeginnerMode.Normal;

        public event Action<BeginnerMode> ModeChanged;
        public event Action<string> NoCodeProjectReady;

        public BeginnerModesView()
        {
            InitializeComponent();
        }

        /// <summary>Injecté par MainPage (builder + gateway XENO).</summary>
        public void SetNoCode(AutoProjectBuilder builder, IXenoGateway xeno, string workspaceRoot)
        {
            _noCode = new NoCodeOrchestrator(builder, xeno);
        }

        public void SetContext(ProjectMap map, string activeFile)
        {
            _map = map;
            _activeFile = activeFile;

            if (CurrentMode == BeginnerMode.Explain && activeFile != null)
            {
                RenderExplain();
            }
        }

        public PairProgrammingEngine Pair => _pair;

        // ------------------------------------------------------------------

        private void OnModeClicked(object sender, EventArgs e)
        {
            CurrentMode =
                sender == BtnExplain ? BeginnerMode.Explain :
                sender == BtnTutor ? BeginnerMode.Tutor :
                sender == BtnNoCode ? BeginnerMode.NoCode :
                sender == BtnPair ? BeginnerMode.Pair : BeginnerMode.Normal;

            ExplainPanel.IsVisible = CurrentMode == BeginnerMode.Explain;
            TutorPanel.IsVisible = CurrentMode == BeginnerMode.Tutor;
            NoCodePanel.IsVisible = CurrentMode == BeginnerMode.NoCode;
            PairPanel.IsVisible = CurrentMode == BeginnerMode.Pair;

            if (CurrentMode == BeginnerMode.Explain) RenderExplain();
            if (CurrentMode == BeginnerMode.Tutor && _currentTutorMessage == null) OnTutorNext(this, EventArgs.Empty);

            ModeChanged?.Invoke(CurrentMode);
        }

        private void RenderExplain()
        {
            if (_map == null || _activeFile == null) return;

            var report = _explain.Explain(_map, _activeFile);

            ExplainFileLabel.Text = System.IO.Path.GetFileName(_activeFile);
            ExplainSummaryLabel.Text = report.FileSummary;

            var sb = new StringBuilder();
            foreach (var l in report.Lines.Take(40))
            {
                sb.AppendLine($"{l.Line,4}  {l.Code}\n      → {l.Explanation}");
            }
            ExplainLinesLabel.Text = sb.ToString();

            var extra = new StringBuilder();
            report.Errors.ForEach(e => extra.AppendLine("❌ " + e));
            report.Systems.ForEach(s => extra.AppendLine("⚙ " + s));
            report.Dependencies.ForEach(d => extra.AppendLine("🔗 " + d));
            ExplainExtraLabel.Text = extra.ToString();
        }

        private void OnTutorNext(object sender, EventArgs e)
        {
            _currentTutorMessage = _tutor.Next();
            TutorMessageLabel.Text = _currentTutorMessage.Text;
            TutorScoreLabel.Text = $"Score : {_tutor.Score} | Série : {_tutor.Streak}";
            TutorAnswerEntry.Text = string.Empty;
        }

        private void OnTutorAnswer(object sender, EventArgs e)
        {
            if (_currentTutorMessage == null) return;

            var feedback = _tutor.Evaluate(TutorAnswerEntry.Text, _currentTutorMessage);
            TutorMessageLabel.Text = feedback.Text;
            TutorScoreLabel.Text = $"Score : {_tutor.Score} | Série : {_tutor.Streak}";
        }

        private async void OnNoCodeRun(object sender, EventArgs e)
        {
            var description = NoCodeInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(description) || _noCode == null) return;

            NoCodeResultLabel.Text = "Je m'en occupe…";

            var result = await _noCode.RunAsync(description,
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/MotoProjects",
                step => MainThread.BeginInvokeOnMainThread(() =>
                    NoCodeStepsLabel.Text += $"\n{step.Status} {step.Name} — {step.Detail}"));

            NoCodeStepsLabel.Text = string.Join("\n", result.Steps.Select(s => $"{s.Status} {s.Name} — {s.Detail}"));
            NoCodeResultLabel.Text = result.Summary;

            if (result.Success) NoCodeProjectReady?.Invoke(result.ProjectPath);
        }
    }
}
