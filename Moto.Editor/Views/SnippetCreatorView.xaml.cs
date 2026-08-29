// Moto.Editor/Views/SnippetCreatorView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Snippets;

namespace Moto.Editor.Views
{
    /// <summary>
    /// UI de création de snippets avec preview live.
    /// </summary>
    public partial class SnippetCreatorView : ContentView
    {
        private SnippetEngine? _snippetEngine;
        private Snippet? _currentSnippet;
        private readonly Dictionary<string, Entry> _variableEntries = new();

        public SnippetCreatorView()
        {
            InitializeComponent();
        }

        public void SetSnippetEngine(SnippetEngine engine)
        {
            _snippetEngine = engine;
        }

        /// <summary>
        /// Met à jour le preview en temps réel.
        /// </summary>
        private void UpdatePreview()
        {
            if (_currentSnippet == null || PreviewEditor == null)
                return;

            var variables = new Dictionary<string, string>();
            foreach (var (key, entry) in _variableEntries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Text))
                    variables[key] = entry.Text;
            }

            var rendered = _snippetEngine!.RenderSnippet(_currentSnippet, variables);
            PreviewEditor.Text = rendered;
        }

        private void OnTriggerChanged(object? sender, TextChangedEventArgs e)
        {
            if (_currentSnippet != null)
                TriggerLabel.Text = $"Trigger: {e.NewTextValue}";
        }

        private void OnBodyChanged(object? sender, TextChangedEventArgs e)
        {
            if (_currentSnippet != null)
            {
                _currentSnippet.Body = e.NewTextValue;
                BuildVariableInputs();
                UpdatePreview();
            }
        }

        private void BuildVariableInputs()
        {
            if (_currentSnippet == null || VariablesContainer == null)
                return;

            VariablesContainer.Children.Clear();
            _variableEntries.Clear();

            var variables = _snippetEngine!.ExtractVariables(_currentSnippet);

            foreach (var varName in variables)
            {
                var stack = new HorizontalStackLayout { Spacing = 8 };

                var label = new Label
                {
                    Text = varName,
                    WidthRequest = 100,
                    TextColor = (Color)Application.Current.Resources["Txt1"]
                };

                var entry = new Entry
                {
                    Placeholder = $"Valeur pour {varName}",
                    BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                    TextColor = (Color)Application.Current.Resources["Txt1"],
                    WidthRequest = 200
                };

                entry.TextChanged += (s, e) => UpdatePreview();
                _variableEntries[varName] = entry;

                stack.Children.Add(label);
                stack.Children.Add(entry);
                VariablesContainer.Children.Add(stack);
            }
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            if (_currentSnippet == null || _snippetEngine == null)
                return;

            if (string.IsNullOrWhiteSpace(_currentSnippet.Trigger) ||
                string.IsNullOrWhiteSpace(_currentSnippet.Body))
            {
                StatusLabel.Text = "❌ Trigger et Body requis";
                return;
            }

            _snippetEngine.CreateSnippet(_currentSnippet);
            StatusLabel.Text = $"✅ Snippet '{_currentSnippet.Trigger}' créé";
        }

        private void OnNewClicked(object? sender, EventArgs e)
        {
            _currentSnippet = new Snippet
            {
                Id = Guid.NewGuid().ToString(),
                Trigger = "",
                Body = "",
                Language = "csharp",
                Author = "Custom"
            };

            TriggerEntry.Text = "";
            BodyEditor.Text = "";
            PreviewEditor.Text = "";
            VariablesContainer.Children.Clear();
            StatusLabel.Text = "Nouveau snippet";
        }
    }
}
