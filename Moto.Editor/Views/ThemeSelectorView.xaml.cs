using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Themes;

namespace Moto.Editor.Views
{
    public partial class ThemeSelectorView : ContentView
    {
        private ThemeManager? _themeManager;

        public ThemeSelectorView()
        {
            InitializeComponent();
        }

        public void SetThemeManager(ThemeManager manager)
        {
            _themeManager = manager;
            RefreshThemeList();
        }

        private void RefreshThemeList()
        {
            if (_themeManager == null) return;

            ThemeList.Children.Clear();
            var themes = _themeManager.GetInstalledThemes();

            foreach (var theme in themes)
            {
                ThemeList.Children.Add(BuildThemeCard(theme));
            }
        }

        private Border BuildThemeCard(ThemeDefinition theme)
        {
            var isCurrent = theme.Id == _themeManager?.CurrentTheme.Id;

            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = isCurrent
                    ? (Color)Application.Current.Resources["Accent"]
                    : (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = isCurrent ? 2 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(12)
            };

            var stack = new VerticalStackLayout { Spacing = 8 };

            // Preview des couleurs
            var preview = new HorizontalStackLayout { Spacing = 4 };
            preview.Children.Add(new BoxView
            {
                Color = Color.FromArgb(theme.Colors.Background),
                WidthRequest = 40, HeightRequest = 40
            });
            preview.Children.Add(new BoxView
            {
                Color = Color.FromArgb(theme.Colors.Accent),
                WidthRequest = 40, HeightRequest = 40
            });
            preview.Children.Add(new BoxView
            {
                Color = Color.FromArgb(theme.Colors.Text1),
                WidthRequest = 40, HeightRequest = 40
            });
            stack.Children.Add(preview);

            // Nom
            stack.Children.Add(new Label
            {
                Text = theme.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            });

            // Description
            stack.Children.Add(new Label
            {
                Text = theme.Description,
                FontSize = 11,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            });

            // Bouton appliquer
            if (!isCurrent)
            {
                var applyBtn = new Button
                {
                    Text = "Appliquer",
                    BackgroundColor = (Color)Application.Current.Resources["Accent"],
                    TextColor = Colors.White,
                    FontSize = 11
                };
                var themeId = theme.Id;
                applyBtn.Clicked += (s, e) => ApplyTheme(themeId);
                stack.Children.Add(applyBtn);
            }
            else
            {
                stack.Children.Add(new Label
                {
                    Text = "✓ Actif",
                    FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Success"]
                });
            }

            card.Content = stack;
            return card;
        }

        private void ApplyTheme(string themeId)
        {
            if (_themeManager == null) return;

            var theme = _themeManager.GetInstalledThemes()
                .FirstOrDefault(t => t.Id == themeId);

            if (theme != null)
            {
                _themeManager.ApplyTheme(theme);
                RefreshThemeList();
            }
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
