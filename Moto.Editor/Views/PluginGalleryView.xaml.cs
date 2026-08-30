// Moto.Editor/Views/PluginGalleryView.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;
using Moto.Core.Plugins;
using Moto.Core.Plugins.Marketplace;

namespace Moto.Editor.Views
{
    public partial class PluginGalleryView : ContentView
    {
        private PluginRegistry? _registry;
        private MarketplaceClient? _marketplace;
        private string _pluginsDirectory;

        public event Action? PluginsChanged;

        public PluginGalleryView(
            PluginRegistry? registry,
            MarketplaceClient? marketplace,
            string pluginsDirectory)
        {
            InitializeComponent();

            _registry = registry;
            _marketplace = marketplace;
            _pluginsDirectory = pluginsDirectory;
        }

        public void SetServices(
            PluginRegistry registry,
            MarketplaceClient marketplace,
            string pluginsDirectory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));
            _pluginsDirectory = pluginsDirectory;
        }

        public async void LoadGallery()
        {
            PluginsList.Children.Clear();

            if (_registry is null || _marketplace is null)
            {
                StatusLabel.Text = "❌ Services plugins non initialisés.";
                return;
            }

            StatusLabel.Text = "Chargement de la galerie…";

            try
            {
                AddSection("📦 Plugins installés");

                var installed = _registry.GetActivePlugins();
                if (installed.Count == 0)
                    AddHint("Aucun plugin actif.");

                foreach (var plugin in installed)
                {
                    AddCard(plugin.DisplayName, plugin.Version, plugin.Description, isInstalled: true);
                }

                AddSection("🛒 Marketplace");

                var catalog = await _marketplace.GetCatalogAsync();
                if (catalog.Count == 0)
                    AddHint("Aucun plugin distant disponible.");

                foreach (var entry in catalog)
                {
                    AddCard(entry.Name, entry.Version, entry.Description, isInstalled: false, entry);
                }

                StatusLabel.Text = $"{installed.Count} installé(s) · {catalog.Count} distant(s).";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"❌ Erreur : {ex.Message}";
            }
        }

        private void AddSection(string title)
        {
            PluginsList.Children.Add(new Label
            {
                Text = title,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Accent"],
                Margin = new Thickness(0, 8, 0, 2)
            });
        }

        private void AddHint(string text)
        {
            PluginsList.Children.Add(new Label
            {
                Text = text,
                FontSize = 12,
                TextColor = (Color)Application.Current.Resources["Txt2"],
                Margin = new Thickness(6, 0, 0, 0)
            });
        }

        private void AddCard(
            string name,
            string version,
            string description,
            bool isInstalled,
            MarketplaceEntry? marketplaceEntry = null)
        {
            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                Padding = new Thickness(10),
                StrokeShape = new RoundRectangle { CornerRadius = 8 }
            };

            var title = new Label
            {
                Text = $"{name}  v{version}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            };

            var desc = new Label
            {
                Text = description,
                FontSize = 12,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            };

            var action = new Button
            {
                Text = isInstalled ? "✓ Installé" : "Installer",
                IsEnabled = !isInstalled,
                BackgroundColor = isInstalled
                    ? (Color)Application.Current.Resources["BgHover"]
                    : (Color)Application.Current.Resources["Accent"],
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.End
            };

            if (!isInstalled && marketplaceEntry != null)
            {
                var entry = marketplaceEntry;

                action.Clicked += async (s, e) =>
                {
                    if (_marketplace is null)
                        return;

                    action.IsEnabled = false;
                    StatusLabel.Text = $"Installation de {entry.Name}…";

                    var result = await _marketplace.InstallAsync(entry, _pluginsDirectory);

                    StatusLabel.Text = result.Success
                        ? $"✅ {result.Message}"
                        : $"❌ {result.Message}";

                    if (result.Success)
                        PluginsChanged?.Invoke();

                    action.IsEnabled = true;
                };
            }

            var stack = new VerticalStackLayout { Spacing = 4 };
            stack.Children.Add(title);
            stack.Children.Add(desc);
            stack.Children.Add(action);

            card.Content = stack;
            PluginsList.Children.Add(card);
        }

        private async void OnSearchCompleted(object sender, EventArgs e)
        {
            if (_marketplace is null)
                return;

            var query = SearchEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                LoadGallery();
                return;
            }

            StatusLabel.Text = $"Recherche : {query}…";

            var results = await _marketplace.GetCatalogAsync(query);

            PluginsList.Children.Clear();
            AddSection($"🔍 Résultats pour « {query} »");

            foreach (var entry in results)
                AddCard(entry.Name, entry.Version, entry.Description, false, entry);

            StatusLabel.Text = $"{results.Count} résultat(s).";
        }

        private void OnCloseClicked(object sender, EventArgs e)
            => IsVisible = false;
    }
}
