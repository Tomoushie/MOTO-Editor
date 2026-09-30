// Moto.Editor/Views/ProactivePanel.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;
using Moto.Core.AI.Suggestions;

namespace Moto.Editor.Views
{
    public partial class ProactivePanel : ContentView
    {
        private readonly ProactiveSuggestionsEngine _engine;

        /// <summary>Déclenché quand l'utilisateur clique sur une suggestion.</summary>
        public event Action<string>? SuggestionInvoked;

        /// <summary>★ AJOUT (26/09) : l'utilisateur a fermé la carte avec la croix (MainPage retient ce choix d'une session à l'autre).</summary>
        public event Action? ClosedByUser;

        /// <summary>
        /// ★ AJOUT (26/09, retour de Tom : fermée, la carte revenait 30 s à 1 min plus tard) : faux une fois la carte fermée à la main — les
        /// mises à jour toutes les 30 s remplissent encore la liste, mais ne la rouvrent plus. Seule la palette (« Suggestions ») la rouvre.
        /// </summary>
        public bool AutoShow { get; set; } = true;

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

            if (AutoShow) IsVisible = true;
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

            // Icône. ★ (25/09) : glyphe Segoe Fluent Icons dérivé de l'emoji fourni par le moteur (Moto.Core reste inchangé).
            var icon = new Label
            {
                Text = GlyphFor(suggestion.Icon),
                FontFamily = Moto.Editor.Controls.MotoIcons.FontFamily,
                FontSize = 16,
                TextColor = (Color)Application.Current.Resources["Txt2"],
                Margin = new Thickness(0, 1, 2, 0),
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
            AutoShow = false;
            ClosedByUser?.Invoke();
        }

        /// <summary>
        /// ★ AJOUT (25/09) : emoji du moteur de suggestions → glyphe du thème (même famille d'icônes que le reste de
        /// l'interface). Une icône inconnue retombe sur l'ampoule plutôt que d'afficher l'emoji en couleur.
        /// </summary>
        private static string GlyphFor(string? emoji) => (emoji ?? string.Empty).Replace("\uFE0F", string.Empty) switch
        {
            "📐" => Moto.Editor.Controls.MotoIcons.Tiles,
            "💻" => Moto.Editor.Controls.MotoIcons.Terminal,
            "✏" => Moto.Editor.Controls.MotoIcons.Edit,
            "🤖" => Moto.Editor.Controls.MotoIcons.Robot,
            "🏗" => Moto.Editor.Controls.MotoIcons.Processing,
            "🔧" => Moto.Editor.Controls.MotoIcons.Repair,
            _ => Moto.Editor.Controls.MotoIcons.Bulb
        };
    }
}
