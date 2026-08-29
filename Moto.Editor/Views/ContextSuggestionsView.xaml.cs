// Moto.Editor/Views/ContextSuggestionsView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Context;

namespace Moto.Editor.Views
{
    public partial class ContextSuggestionsView : ContentView
    {
        private List<ContextSuggestion> _suggestions = new();

        /// <summary>Déclenché quand l'utilisateur clique sur Apply.</summary>
        public event Action<ContextSuggestion> ApplyRequested;

        /// <summary>Déclenché quand l'utilisateur clique sur Dismiss.</summary>
        public event Action<ContextSuggestion> DismissRequested;

        public ContextSuggestionsView()
        {
            InitializeComponent();
        }

        public void Load(ContextReport report)
        {
            _suggestions = report.Suggestions.ToList();
            SuggestionsList.ItemsSource = _suggestions;

            // Ajoute l'icône à chaque suggestion
            foreach (var s in _suggestions)
            {
                s.Icon = s.GetIcon();
            }

            SummaryLabel.Text = _suggestions.Count > 0
                ? $"{_suggestions.Count} suggestion(s) pour ce fichier."
                : "Aucune suggestion pour l'instant.";
        }

        public void Clear()
        {
            _suggestions.Clear();
            SuggestionsList.ItemsSource = _suggestions;
            SummaryLabel.Text = "Aucune suggestion pour l'instant.";
        }

        private void OnApplyClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is ContextSuggestion suggestion)
            {
                _suggestions.Remove(suggestion);
                SuggestionsList.ItemsSource = _suggestions.ToList();
                ApplyRequested?.Invoke(suggestion);
            }
        }

        private void OnDismissClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is ContextSuggestion suggestion)
            {
                _suggestions.Remove(suggestion);
                SuggestionsList.ItemsSource = _suggestions.ToList();
                DismissRequested?.Invoke(suggestion);
            }
        }
    }
}
