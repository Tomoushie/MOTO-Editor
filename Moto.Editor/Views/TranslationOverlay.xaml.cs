// Moto.Editor/Views/TranslationOverlay.xaml.cs
// Overlay de traduction documentaire avec suggestions proactives.
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.I18n;

namespace Moto.Editor.Views
{
    public partial class TranslationOverlay : ContentView
    {
        private DocumentTranslationAdvisor? _advisor;
        private AiTranslationEngine? _translationEngine;

        public TranslationOverlay()
        {
            InitializeComponent();
        }

        public void SetServices(DocumentTranslationAdvisor advisor, AiTranslationEngine engine)
        {
            _advisor = advisor;
            _translationEngine = engine;

            if (_advisor != null)
                _advisor.SuggestionGenerated += OnSuggestionGenerated;
        }

        /// <summary>
        /// Analyse le document actuel et affiche les suggestions.
        /// </summary>
        public async void AnalyzeDocument(string filePath, string content)
        {
            if (_advisor == null) return;

            StatusLabel.Text = "🔍 Analyse du document…";
            var suggestions = await _advisor.AnalyzeFileAsync(filePath, content);

            SuggestionsContainer.Children.Clear();

            if (suggestions.Count == 0)
            {
                StatusLabel.Text = "Aucune suggestion de traduction.";
                return;
            }

            StatusLabel.Text = $"💡 {suggestions.Count} suggestion(s) de traduction";

            foreach (var suggestion in suggestions)
            {
                SuggestionsContainer.Children.Add(BuildSuggestionCard(suggestion));
            }
        }

        private Border BuildSuggestionCard(TranslationSuggestion suggestion)
        {
            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(12)
            };

            var stack = new VerticalStackLayout { Spacing = 6 };

            // Titre
            stack.Children.Add(new Label
            {
                Text = $"🌐 {suggestion.DetectedLanguage} → {suggestion.SuggestedTargetLanguage}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Accent"]
            });

            // Raison
            stack.Children.Add(new Label
            {
                Text = suggestion.Reason,
                FontSize = 11,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            });

            // Confiance
            stack.Children.Add(new Label
            {
                Text = $"Confiance : {suggestion.Confidence:P0}",
                FontSize = 10,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            });

            // Bouton traduire
            var translateBtn = new Button
            {
                Text = "🔄 Traduire",
                BackgroundColor = (Color)Application.Current.Resources["Accent"],
                TextColor = Colors.White,
                FontSize = 11
            };

            var targetLang = suggestion.SuggestedTargetLanguage;
            var filePath = suggestion.FilePath;
            translateBtn.Clicked += async (s, e) =>
            {
                translateBtn.IsEnabled = false;
                translateBtn.Text = "Traduction…";

                if (_translationEngine != null)
                {
                    var content = System.IO.File.ReadAllText(filePath);
                    var translated = await _translationEngine.TranslateDocumentAsync(content, targetLang);

                    // Afficher le résultat dans une boîte de dialogue
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Traduction terminée",
                        translated.Length > 500
                            ? translated.Substring(0, 500) + "…"
                            : translated,
                        "OK");
                }

                translateBtn.IsEnabled = true;
                translateBtn.Text = "🔄 Traduire";
            };

            stack.Children.Add(translateBtn);
            card.Content = stack;
            return card;
        }

        private void OnSuggestionGenerated(TranslationSuggestion suggestion)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SuggestionsContainer.Children.Add(BuildSuggestionCard(suggestion));
            });
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
