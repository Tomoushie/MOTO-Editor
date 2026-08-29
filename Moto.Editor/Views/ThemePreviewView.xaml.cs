// Moto.Editor/Views/ThemePreviewView.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Themes;

namespace Moto.Editor.Views
{
    public partial class ThemePreviewView : ContentView
    {
        private ThemeManager? _themeManager;
        private ThemeDefinition? _previewTheme;
        private ThemeDefinition? _originalTheme;
        private readonly Dictionary<string, Color> _originalColors = new();

        public event Action<ThemeDefinition>? ThemeApplied;

        public ThemePreviewView()
        {
            InitializeComponent();
        }

        public void SetThemeManager(ThemeManager manager)
        {
            _themeManager = manager;
        }

        /// <summary>
        /// Affiche la preview d'un thème avec application temporaire.
        /// </summary>
        public void PreviewTheme(ThemeDefinition theme)
        {
            _previewTheme = theme;
            _originalTheme = _themeManager?.CurrentTheme;

            // Sauvegarder les couleurs actuelles
            SaveCurrentColors();

            // Appliquer temporairement
            ApplyThemeTemporarily(theme);

            // Mettre à jour l'UI
            ThemeNameLabel.Text = $"🎨 Preview : {theme.Name}";
            IsVisible = true;
        }

        private void SaveCurrentColors()
        {
            if (Application.Current?.Resources == null) return;

            var colorKeys = new[] { "BgPanel", "BgSide", "BgHover", "Txt1", "Txt2", "Accent", "Success", "Warning", "Error", "Info" };
            foreach (var key in colorKeys)
            {
                if (Application.Current.Resources.TryGetValue(key, out var value) && value is Color color)
                    _originalColors[key] = color;
            }
        }

        private void ApplyThemeTemporarily(ThemeDefinition theme)
        {
            if (Application.Current?.Resources == null) return;

            var resources = Application.Current.Resources;
            resources["BgPanel"] = Color.FromArgb(theme.Colors.Background);
            resources["BgSide"] = Color.FromArgb(theme.Colors.BackgroundSide);
            resources["BgHover"] = Color.FromArgb(theme.Colors.BackgroundHover);
            resources["Txt1"] = Color.FromArgb(theme.Colors.Text1);
            resources["Txt2"] = Color.FromArgb(theme.Colors.Text2);
            resources["Accent"] = Color.FromArgb(theme.Colors.Accent);
            resources["Success"] = Color.FromArgb(theme.Colors.Success);
            resources["Warning"] = Color.FromArgb(theme.Colors.Warning);
            resources["Error"] = Color.FromArgb(theme.Colors.Error);
            resources["Info"] = Color.FromArgb(theme.Colors.Info);

            // Forcer le refresh visuel
            PreviewContainer.BackgroundColor = Color.FromArgb(theme.Colors.BackgroundSide);
            PreviewTitle.TextColor = Color.FromArgb(theme.Colors.Text1);
            PreviewText1.TextColor = Color.FromArgb(theme.Colors.Text1);
            PreviewText2.TextColor = Color.FromArgb(theme.Colors.Text2);
            PreviewBtnAccent.BackgroundColor = Color.FromArgb(theme.Colors.Accent);
            PreviewBtnSide.BackgroundColor = Color.FromArgb(theme.Colors.BackgroundSide);
            PreviewBtnSide.TextColor = Color.FromArgb(theme.Colors.Text1);
            PreviewCodeBlock.BackgroundColor = Color.FromArgb(theme.Colors.Background);
            PreviewCode.TextColor = Color.FromArgb(theme.Colors.Text1);
        }

        private void RestoreOriginalColors()
        {
            if (Application.Current?.Resources == null) return;

            foreach (var (key, color) in _originalColors)
                Application.Current.Resources[key] = color;
        }

        private void OnApplyClicked(object? sender, EventArgs e)
        {
            if (_previewTheme == null || _themeManager == null) return;

            _themeManager.ApplyTheme(_previewTheme);
            ThemeApplied?.Invoke(_previewTheme);
            IsVisible = false;
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            RestoreOriginalColors();
            IsVisible = false;
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            RestoreOriginalColors();
            IsVisible = false;
        }
    }
}
