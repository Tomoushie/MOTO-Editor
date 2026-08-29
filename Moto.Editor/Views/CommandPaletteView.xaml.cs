// Moto.Editor/Views/CommandPaletteView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Commands;

namespace Moto.Editor.Views
{
    public partial class CommandPaletteView : ContentView
    {
        private readonly CommandPaletteEngine _engine;
        private ActionContext? _currentContext;
        private List<PaletteCommand> _currentResults = new();
        private int _selectedIndex = -1;

        /// <summary>Déclenché quand une commande est sélectionnée.</summary>
        public event Action<string>? CommandInvoked;

        public CommandPaletteView(CommandPaletteEngine engine)
        {
            InitializeComponent();
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>Ouvre la palette avec le contexte courant.</summary>
        public void Open(ActionContext? context = null)
        {
            _currentContext = context;
            _selectedIndex = -1;
            SearchEntry.Text = string.Empty;
            UpdateResults(string.Empty);
            IsVisible = true;
            SearchEntry.Focus();
        }

        /// <summary>Ferme la palette.</summary>
        public void Close()
        {
            IsVisible = false;
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateResults(e.NewTextValue);
        }

        private void OnSearchCompleted(object? sender, EventArgs e)
        {
            // Exécute la commande sélectionnée (ou la première)
            if (_selectedIndex >= 0 && _selectedIndex < _currentResults.Count)
            {
                ExecuteCommand(_currentResults[_selectedIndex]);
            }
            else if (_currentResults.Count > 0)
            {
                ExecuteCommand(_currentResults[0]);
            }
        }

        private void UpdateResults(string query)
        {
            ResultsList.Children.Clear();
            _currentResults = _engine.Search(query, _currentContext).ToList();
            _selectedIndex = _currentResults.Count > 0 ? 0 : -1;

            if (_currentResults.Count == 0)
            {
                ResultsList.Children.Add(new Label
                {
                    Text = "Aucune commande trouvée.",
                    TextColor = (Color)Application.Current.Resources["Txt2"],
                    FontSize = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                });
                return;
            }

            CommandCategory? lastCategory = null;

            for (int i = 0; i < _currentResults.Count; i++)
            {
                var cmd = _currentResults[i];

                // Header de catégorie
                if (lastCategory != cmd.Category)
                {
                    lastCategory = cmd.Category;
                    ResultsList.Children.Add(new Label
                    {
                        Text = GetCategoryLabel(cmd.Category),
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = (Color)Application.Current.Resources["Accent"],
                        Margin = new Thickness(8, i == 0 ? 0 : 12, 0, 4)
                    });
                }

                var row = BuildCommandRow(cmd, i == _selectedIndex);
                ResultsList.Children.Add(row);
            }
        }

        private Grid BuildCommandRow(PaletteCommand cmd, bool isSelected)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                BackgroundColor = isSelected
                    ? (Color)Application.Current.Resources["BgHover"]
                    : Colors.Transparent,
                Padding = new Thickness(10, 8),
                ColumnSpacing = 12
            };

            // Colonne gauche : titre + description
            var leftStack = new VerticalStackLayout { Spacing = 2 };
            leftStack.Children.Add(new Label
            {
                Text = cmd.Title,
                FontSize = 13,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            });
            if (!string.IsNullOrWhiteSpace(cmd.Description))
            {
                leftStack.Children.Add(new Label
                {
                    Text = cmd.Description,
                    FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Txt2"]
                });
            }
            Grid.SetColumn(leftStack, 0);
            row.Children.Add(leftStack);

            // Colonne droite : raccourci
            if (!string.IsNullOrWhiteSpace(cmd.Shortcut))
            {
                var shortcutBorder = new Border
                {
                    BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                    Stroke = (Color)Application.Current.Resources["BgHover"],
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    Padding = new Thickness(6, 3),
                    VerticalOptions = LayoutOptions.Center
                };
                shortcutBorder.Content = new Label
                {
                    Text = cmd.Shortcut,
                    FontSize = 10,
                    TextColor = (Color)Application.Current.Resources["Txt2"]
                };
                Grid.SetColumn(shortcutBorder, 1);
                row.Children.Add(shortcutBorder);
            }

            // Interaction
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => ExecuteCommand(cmd);
            row.GestureRecognizers.Add(tap);

            // Hover
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (_, _) =>
                row.BackgroundColor = (Color)Application.Current.Resources["BgHover"];
            pointer.PointerExited += (_, _) =>
                row.BackgroundColor = Colors.Transparent;
            row.GestureRecognizers.Add(pointer);

            return row;
        }

        private void ExecuteCommand(PaletteCommand cmd)
        {
            Close();
            CommandInvoked?.Invoke(cmd.CommandText);
        }

        private static string GetCategoryLabel(CommandCategory category) => category switch
        {
            CommandCategory.Menu => "📋 Menu",
            CommandCategory.Action => "💡 Actions contextuelles",
            CommandCategory.Slash => "⚡ Commandes slash",
            CommandCategory.Plugin => "🧩 Plugins",
            CommandCategory.Navigation => "🧭 Navigation",
            CommandCategory.Settings => "⚙️ Paramètres",
            _ => category.ToString()
        };
    }
}
