// Moto.Editor/Views/ProactivePanel.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.AI.Suggestions;

namespace Moto.Editor.Views
{
    public partial class ProactivePanel : ContentView
    {
        private readonly ProactiveSuggestionsEngine _engine;

        /// <summary>Déclenché quand l'utilisateur clique sur une suggestion.</summary>
        public event Action<string>? SuggestionInvoked;

        public ProactivePanel(ProactiveSuggestionsEngine engine)
        {
            InitializeComponent();
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>Met à jour l'affichage avec les suggestions courantes.</summary>
        public void UpdateSuggestions(IReadOnlyList<ProactiveSuggestion> suggestions)
        {
            SuggestionsList.Children.Clear();

            if (suggestions == null || suggestions.Count == 0)
            {
                IsVisible = false;
                return;
            }

            foreach (var suggestion in suggestions)
            {
                var card = BuildSuggestionCard(suggestion);
                SuggestionsList.Children.Add(card);
            }

            IsVisible = true;
        }

        private Border BuildSuggestionCard(ProactiveSuggestion suggestion)
        {
            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                Padding = new Thickness(10),
                StrokeShape = new RoundRectangle { CornerRadius = 8 }
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };

            // Icône
            var icon = new Label
            {
                Text = suggestion.Icon,
                FontSize = 18,
                VerticalOptions = LayoutOptions.Start
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            // Contenu
            var contentStack = new VerticalStackLayout { Spacing = 2 };
            contentStack.Children.Add(new Label
            {
                Text = suggestion.Title,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            });
            contentStack.Children.Add(new Label
            {
                Text = suggestion.Description,
                FontSize = 11,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            });
            Grid.SetColumn(contentStack, 1);
            grid.Children.Add(contentStack);

            card.Content = grid;

            // Interaction : clic pour exécuter
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                _engine.RecordExecution(suggestion);
                SuggestionInvoked?.Invoke(suggestion.Command);
                IsVisible = false;
            };
            card.GestureRecognizers.Add(tap);

            // Hover
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (_, _) =>
                card.BackgroundColor = (Color)Application.Current.Resources["BgHover"];
            pointer.PointerExited += (_, _) =>
                card.BackgroundColor = (Color)Application.Current.Resources["BgSide"];
            card.GestureRecognizers.Add(pointer);

            return card;
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
