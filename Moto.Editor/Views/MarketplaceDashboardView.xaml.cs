// Moto.Editor/Views/MarketplaceDashboardView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Plugins;
using Moto.Core.Plugins.Marketplace;

namespace Moto.Editor.Views
{
    public partial class MarketplaceDashboardView : ContentView
    {
        private MarketplaceServerClient? _marketplaceClient;
        private string? _pluginsDirectory;
        private readonly Dictionary<string, string> _installedVersions = new();

        public event Action<string>? PluginInstalled;
        public event Action<string>? PluginUpdated;

        public MarketplaceDashboardView()
        {
            InitializeComponent();
            CategoryPicker.SelectedIndex = 0;
            SortPicker.SelectedIndex = 0;
        }

        public void SetClient(MarketplaceServerClient client, string pluginsDirectory,
            IReadOnlyDictionary<string, string> installed)
        {
            _marketplaceClient = client;
            _pluginsDirectory = pluginsDirectory;

            foreach (var kv in installed)
                _installedVersions[kv.Key] = kv.Value;

            _ = LoadPluginsAsync();
        }

        private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
        {
            await LoadPluginsAsync(e.NewTextValue);
        }

        private async Task LoadPluginsAsync(string? search = null)
        {
            if (_marketplaceClient == null)
            {
                StatusLabel.Text = "Client marketplace non disponible.";
                return;
            }

            StatusLabel.Text = "Chargement…";
            PluginsList.Children.Clear();

            try
            {
                var category = CategoryPicker.SelectedIndex switch
                {
                    1 => PluginCategory.Productivity,
                    2 => PluginCategory.Language,
                    3 => PluginCategory.Theme,
                    4 => PluginCategory.Debugger,
                    5 => PluginCategory.Collaboration,
                    _ => (PluginCategory?)null
                };

                var result = await _marketplaceClient.SearchAsync(
                    query: search,
                    category: category,
                    page: 1,
                    pageSize: 20);

                if (result.Plugins.Count == 0)
                {
                    PluginsList.Children.Add(new Label
                    {
                        Text = "Aucun plugin trouvé.",
                        TextColor = (Color)Application.Current.Resources["Txt2"],
                        HorizontalOptions = LayoutOptions.Center,
                        Margin = new Thickness(0, 20)
                    });
                }
                else
                {
                    foreach (var plugin in result.Plugins)
                        PluginsList.Children.Add(BuildPluginCard(plugin));
                }

                StatusLabel.Text = $"{result.TotalCount} plugin(s) trouvé(s).";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Erreur : {ex.Message}";
            }
        }

        private Border BuildPluginCard(PluginManifestPro plugin)
        {
            var installed = _installedVersions.TryGetValue(plugin.Id, out var v) ? v : null;
            var hasUpdate = installed != null && string.Compare(plugin.Version, installed, StringComparison.Ordinal) > 0;

            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(12)
            };

            var stack = new VerticalStackLayout { Spacing = 6 };

            // Titre + badges
            var header = new HorizontalStackLayout { Spacing = 8 };
            header.Children.Add(new Label
            {
                Text = plugin.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            });

            if (plugin.IsVerified)
            {
                header.Children.Add(new Label
                {
                    Text = "✅",
                    FontSize = 12,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            stack.Children.Add(header);

            // Description
            stack.Children.Add(new Label
            {
                Text = plugin.Description,
                FontSize = 11,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            });

            // Métadonnées
            var meta = new Label
            {
                Text = $"v{plugin.Version} · 👤 {plugin.Author} · ⬇️ {plugin.Analytics.TotalDownloads:N0} · ⭐ {plugin.Analytics.AverageRating:F1}",
                FontSize = 10,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            };
            stack.Children.Add(meta);

            // Dépendances
            if (plugin.Dependencies.Count > 0)
            {
                stack.Children.Add(new Label
                {
                    Text = $"🔗 {plugin.Dependencies.Count} dépendance(s)",
                    FontSize = 10,
                    TextColor = (Color)Application.Current.Resources["Accent"]
                });
            }

            // Actions
            var actions = new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };

            if (installed == null)
            {
                var installBtn = new Button
                {
                    Text = "📥 Installer",
                    BackgroundColor = (Color)Application.Current.Resources["Accent"],
                    TextColor = Colors.White,
                    FontSize = 11
                };
                installBtn.Clicked += async (s, e) => await InstallPluginAsync(plugin, installBtn);
                actions.Children.Add(installBtn);
            }
            else if (hasUpdate)
            {
                var updateBtn = new Button
                {
                    Text = $"🔄 Mettre à jour ({installed} → {plugin.Version})",
                    BackgroundColor = Color.FromArgb("#F59E0B"),
                    TextColor = Colors.White,
                    FontSize = 11
                };
                updateBtn.Clicked += async (s, e) => await UpdatePluginAsync(plugin, updateBtn);
                actions.Children.Add(updateBtn);
            }
            else
            {
                actions.Children.Add(new Label
                {
                    Text = "✅ Installé",
                    TextColor = Color.FromArgb("#10B981"),
                    FontSize = 11,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            stack.Children.Add(actions);
            card.Content = stack;
            return card;
        }

        private async Task InstallPluginAsync(PluginManifestPro plugin, Button button)
        {
            if (_marketplaceClient == null || _pluginsDirectory == null) return;

            button.IsEnabled = false;
            button.Text = "Installation…";
            StatusLabel.Text = $"Installation de {plugin.Name}…";

            try
            {
                var result = await _marketplaceClient.InstallAsync(plugin, _pluginsDirectory);

                if (result.Success)
                {
                    _installedVersions[plugin.Id] = plugin.Version;
                    StatusLabel.Text = $"✅ {plugin.Name} installé.";
                    button.Text = "✅ Installé";
                    PluginInstalled?.Invoke(plugin.Id);
                    await _marketplaceClient.RecordInstallAsync(plugin.Id);
                }
                else
                {
                    StatusLabel.Text = $"❌ {result.Message}";
                    button.Text = "📥 Installer";
                    button.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Erreur : {ex.Message}";
                button.Text = "📥 Installer";
                button.IsEnabled = true;
            }
        }

        private async Task UpdatePluginAsync(PluginManifestPro plugin, Button button)
        {
            await InstallPluginAsync(plugin, button);
            PluginUpdated?.Invoke(plugin.Id);
        }

        private async void OnCheckUpdatesClicked(object? sender, EventArgs e)
        {
            if (_marketplaceClient == null) return;

            StatusLabel.Text = "Vérification des mises à jour…";
            var updates = await _marketplaceClient.CheckUpdatesAsync(_installedVersions);

            if (updates.Count == 0)
            {
                StatusLabel.Text = "✅ Tous les plugins sont à jour.";
            }
            else
            {
                StatusLabel.Text = $"🔄 {updates.Count} mise(s) à jour disponible(s).";
                await LoadPluginsAsync();
            }
        }

        private void OnAnalyticsClicked(object? sender, EventArgs e)
        {
            StatusLabel.Text = "📊 Analytics : fonctionnalité à venir.";
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
