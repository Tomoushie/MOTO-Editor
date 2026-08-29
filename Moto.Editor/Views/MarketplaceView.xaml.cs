// Moto.Editor/Views/MarketplaceView.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Plugins.Marketplace;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Overlay marketplace : liste les plugins communautaires et permet l'installation.
    /// Cohérent avec MotoTheme.xaml et les autres overlays (Export, Platform…).
    /// </summary>
    public partial class MarketplaceView : ContentView
    {
        private readonly MarketplaceClient _client;
        private readonly string _pluginsDirectory;

        /// <summary>Déclenché après une installation réussie pour recharger les plugins.</summary>
        public event Action? PluginInstalled;

        public MarketplaceView(MarketplaceClient client, string pluginsDirectory)
        {
            InitializeComponent();
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _pluginsDirectory = pluginsDirectory;
        }

        /// <summary>Charge le catalogue et peuple la liste.</summary>
        public async void LoadCatalog()
        {
            StatusLabel.Text = "Chargement du catalogue…";
            try
            {
                var entries = await _client.GetCatalogAsync();
                PopulateList(entries);
                StatusLabel.Text = $"{entries.Count} plugin(s) disponible(s).";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"❌ Impossible de contacter le marketplace : {ex.Message}";
            }
        }

        private void PopulateList(IReadOnlyList<MarketplaceEntry> entries)
        {
            PluginsList.Children.Clear();

            foreach (var entry in entries)
            {
                var card = new Border
                {
                    BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                    Stroke = (Color)Application.Current.Resources["BgHover"],
                    StrokeThickness = 1,
                    Padding = new Thickness(12),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 }
                };

                var title = new Label
                {
                    Text = $"{entry.Name} v{entry.Version}",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = (Color)Application.Current.Resources["Txt1"]
                };

                var desc = new Label
                {
                    Text = entry.Description,
                    FontSize = 12,
                    TextColor = (Color)Application.Current.Resources["Txt2"]
                };

                var meta = new Label
                {
                    Text = $"par {entry.Author} · ⬇ {entry.DownloadCount} · ★ {entry.Rating:0.0}",
                    FontSize = 11,
                    TextColor = (Color)Application.Current.Resources["Accent"]
                };

                var installBtn = new Button
                {
                    Text = "Installer",
                    BackgroundColor = (Color)Application.Current.Resources["Accent"],
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.End
                };
                installBtn.Clicked += async (s, e) => await InstallAsync(entry, installBtn);

                var stack = new VerticalStackLayout { Spacing = 4 };
                stack.Children.Add(title);
                stack.Children.Add(desc);
                stack.Children.Add(meta);
                stack.Children.Add(installBtn);

                card.Content = stack;
                PluginsList.Children.Add(card);
            }
        }

        private async System.Threading.Tasks.Task InstallAsync(MarketplaceEntry entry, Button button)
        {
            button.IsEnabled = false;
            StatusLabel.Text = $"Installation de {entry.Name}…";

            var result = await _client.InstallAsync(entry, _pluginsDirectory);

            StatusLabel.Text = result.Success ? $"✅ {result.Message}" : $"❌ {result.Message}";

            if (result.Success)
            {
                PluginInstalled?.Invoke();
            }

            button.IsEnabled = true;
        }

        private async void OnSearchCompleted(object sender, EventArgs e)
        {
            var query = SearchEntry.Text?.Trim();
            StatusLabel.Text = $"Recherche : {query}…";
            try
            {
                var entries = await _client.GetCatalogAsync(query);
                PopulateList(entries);
                StatusLabel.Text = $"{entries.Count} résultat(s).";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"❌ Recherche échouée : {ex.Message}";
            }
        }

        private void OnCloseClicked(object sender, EventArgs e) => IsVisible = false;
    }
}
