// Moto.Editor/Views/AIWorkspaceView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Workspace;

namespace Moto.Editor.Views
{
    /// <summary>
    /// UI du AI Workspace : suggestions proactives, navigation intelligente,
    /// refactor automatique, documentation automatique.
    /// </summary>
    public partial class AIWorkspaceView : ContentView
    {
        private readonly AIWorkspace _workspace;
        private List<WorkspaceSuggestion> _suggestions = new();

        /// <summary>Déclenché quand l'utilisateur applique une suggestion.</summary>
        public event Action<WorkspaceSuggestion> ApplyRequested;

        public AIWorkspaceView(AIWorkspace workspace)
        {
            InitializeComponent();
            _workspace = workspace;

            _workspace.StatusUpdated += msg =>
                MainThread.BeginInvokeOnMainThread(() => StatusFooter.Text = msg);
        }

        /// <summary>Lance l'analyse asynchrone du workspace.</summary>
        public async void Analyze()
        {
            SummaryLabel.Text = "Analyse en cours…";

            try
            {
                _suggestions = await _workspace.AnalyzeAsync();
                SuggestionsList.ItemsSource = _suggestions.ToList();

                SummaryLabel.Text = _suggestions.Count > 0
                    ? $"🏗 {_suggestions.Count} suggestion(s) pour ton projet."
                    : "✅ Projet sain — aucune action nécessaire.";
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = "❌ " + ex.Message;
            }
        }

        /// <summary>Met à jour les stats affichées.</summary>
        public void RefreshStats()
        {
            var stats = _workspace.GetStats();
            StatusFooter.Text =
                $"💡 Cortex : {stats.CortexHabits} hab · {stats.CortexPatterns} patterns · " +
                $"{stats.CortexCorrections} corrections.";
        }

        private void OnReanalyzeClicked(object sender, EventArgs e) => Analyze();

        private void OnApplyClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.BindingContext is WorkspaceSuggestion suggestion)
            {
                _suggestions.Remove(suggestion);
                SuggestionsList.ItemsSource = _suggestions.ToList();
                ApplyRequested?.Invoke(suggestion);
            }
        }
    }
}
