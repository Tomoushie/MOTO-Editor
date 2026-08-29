// Moto.Editor/Views/PlatformView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.Platform;

namespace Moto.Editor.Views
{
    public partial class PlatformView : ContentView
    {
        private readonly PlatformEngine _engine;
        private string _workspace;
        private string _csprojPath;

        public PlatformView(PlatformEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            _engine.DetectionReady += report => MainThread.BeginInvokeOnMainThread(() =>
            {
                _csprojPath = report.CsprojPath;
                ProposalsList.ItemsSource = report.Proposals.ToList();

                SummaryLabel.Text = report.IsMauiProject
                    ? $"Projet MAUI détecté · {report.Proposals.Count} portage(s) proposé(s)."
                    : $"Projet non-MAUI · {report.Proposals.Count} portage(s) proposé(s).";
            });

            _engine.Progress += (msg, ratio) => MainThread.BeginInvokeOnMainThread(() =>
            {
                GenProgress.IsVisible = true;
                GenProgress.Progress = ratio;
                LogLabel.Text = msg;
            });

            _engine.GenerationDone += (proposal, ok) => MainThread.BeginInvokeOnMainThread(() =>
            {
                GenProgress.IsVisible = false;
                LogLabel.Text = ok ? $"✅ {proposal.Title} terminé." : "❌ Échec de la génération.";
            });
        }

        public void SetWorkspace(string workspace)
        {
            _workspace = workspace;
        }

        public void Analyze()
        {
            if (!string.IsNullOrWhiteSpace(_workspace))
            {
                _engine.AnalyzeNow(_workspace);
            }
        }

        private void OnReanalyzeClicked(object sender, EventArgs e) => Analyze();

        private async void OnGenerateClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is PlatformProposal proposal)
            {
                // Génération en arrière-plan : l'éditeur reste fluide.
                await _engine.ApplyAsync(proposal, _workspace, _csprojPath);
            }
        }
    }
}
