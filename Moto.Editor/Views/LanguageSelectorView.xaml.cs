// Moto.Editor/Views/LanguageSelectorView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.I18n;

namespace Moto.Editor.Views
{
    public partial class LanguageSelectorView : ContentView
    {
        private LanguageManager? _languageManager;
        private MarketplaceLanguageClient? _marketplaceClient;
        private enum TabKind { Installed, Marketplace, Contribute }
        private TabKind _currentTab = TabKind.Installed;

        public LanguageSelectorView()
        {
            InitializeComponent();
        }

        public void SetServices(LanguageManager languageManager, MarketplaceLanguageClient marketplaceClient)
        {
            _languageManager = languageManager;
            _marketplaceClient = marketplaceClient;
            RefreshView();
        }

        private void RefreshView()
        {
            ContentArea.Children.Clear();
            switch (_currentTab)
            {
                case TabKind.Installed: RenderInstalled(); break;
                case TabKind.Marketplace: _ = RenderMarketplaceAsync(); break;
                case TabKind.Contribute: RenderContribute(); break;
            }
        }

        private void RenderInstalled()
        {
            if (_languageManager == null) return;

            var languages = _languageManager.AvailableLanguages;
            foreach (var lang in languages)
            {
                var isCurrent = lang.Code == _languageManager.CurrentLanguageCode;
                ContentArea.Children.Add(BuildLanguageCard(lang, isCurrent));
            }
        }

        private async System.Threading.Tasks.Task RenderMarketplaceAsync()
        {
            if (_marketplaceClient == null) return;

            StatusLabel.Text = "Chargement…";
            var catalog = await _marketplaceClient.GetCatalogAsync();
            StatusLabel.Text = $"{catalog.Count} langue(s) disponible(s).";

            foreach (var lang in catalog)
            {
                ContentArea.Children.Add(BuildMarketplaceCard(lang));
            }
        }

        private void RenderContribute()
        {
            ContentArea.Children.Add(new Label
            {
                Text = "✍️ Contribuer une traduction",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"]
            });

            ContentArea.Children.Add(new Label
            {
                Text = "Vous parlez une langue non supportée ? Proposez votre traduction et aidez la communauté !",
                FontSize = 12,
                TextColor = (Color)Application.Current.Resources["Txt2"],
                Margin = new Thickness(0, 8, 0, 0)
            });

            var contributeBtn = new Button
            {
                Text = "📤 Proposer une traduction",
                BackgroundColor = (Color)Application.Current.Resources["Accent"],
                TextColor = Colors.White,
                Margin = new Thickness(0, 16, 0, 0)
            };
            contributeBtn.Clicked += OnContributeClicked;
            ContentArea.Children.Add(contributeBtn);
        }

        private Border BuildLanguageCard(LanguageInfo lang, bool isCurrent)
        {
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

            var stack = new VerticalStackLayout { Spacing = 4 };

            var header = new HorizontalStackLayout { Spacing = 8 };
            header.Children.Add(new Label { Text = lang.Flag, FontSize = 20 });
            header.Children.Add(new Label
            {
                Text = $"{lang.NativeName} ({lang.Name})",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"],
                VerticalOptions = LayoutOptions.Center
            });

            if (isCurrent)
            {
                header.Children.Add(new Label
                {
                    Text = "✅",
                    FontSize = 14,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            stack.Children.Add(header);

            if (!lang.IsBuiltIn)
            {
                stack.Children.Add(new Label
                {
                    Text = $"par {lang.Author ?? "Communauté"}",
                    FontSize = 10,
                    TextColor = (Color)Application.Current.Resources["Txt2"]
                });
            }

            if (!isCurrent)
            {
                var selectBtn = new Button
                {
                    Text = "Utiliser cette langue",
                    BackgroundColor = (Color)Application.Current.Resources["Accent"],
                    TextColor = Colors.White,
                    FontSize = 11,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var langCode = lang.Code;
                selectBtn.Clicked += (s, e) =>
                {
                    _languageManager?.SetLanguage(langCode);
                    RefreshView();
                };
                stack.Children.Add(selectBtn);
            }

            card.Content = stack;
            return card;
        }

        private Border BuildMarketplaceCard(MarketplaceLanguageInfo lang)
        {
            var card = new Border
            {
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                Stroke = (Color)Application.Current.Resources["BgHover"],
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(12)
            };

            var stack = new VerticalStackLayout { Spacing = 4 };

            var header = new HorizontalStackLayout { Spacing = 8 };
            header.Children.Add(new Label { Text = lang.Flag, FontSize = 20 });
            header.Children.Add(new Label
            {
                Text = $"{lang.NativeName} ({lang.Name})",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current.Resources["Txt1"],
                VerticalOptions = LayoutOptions.Center
            });
            stack.Children.Add(header);

            stack.Children.Add(new Label
            {
                Text = $"par {lang.Author} · ⬇️ {lang.DownloadCount:N0} · ⭐ {lang.Rating:F1}",
                FontSize = 10,
                TextColor = (Color)Application.Current.Resources["Txt2"]
            });

            var installBtn = new Button
            {
                Text = "📥 Installer",
                BackgroundColor = (Color)Application.Current.Resources["Accent"],
                TextColor = Colors.White,
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var langCode = lang.Code;
            installBtn.Clicked += async (s, e) => await InstallLanguageAsync(langCode, installBtn);
            stack.Children.Add(installBtn);

            card.Content = stack;
            return card;
        }

        private async System.Threading.Tasks.Task InstallLanguageAsync(string code, Button button)
        {
            if (_marketplaceClient == null || _languageManager == null) return;

            button.IsEnabled = false;
            button.Text = "Installation…";
            StatusLabel.Text = $"Téléchargement de {code}…";

            var pack = await _marketplaceClient.DownloadPackAsync(code);
            if (pack != null)
            {
                // Sauvegarder le pack localement
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var path = System.IO.Path.Combine(appData, "MotoEditor", "languages", $"{code}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(pack);
                await System.IO.File.WriteAllTextAsync(path, json);

                StatusLabel.Text = $"✅ {pack.Info.NativeName} installé.";
                button.Text = "✅ Installé";
            }
            else
            {
                StatusLabel.Text = "❌ Erreur d'installation.";
                button.Text = "📥 Installer";
                button.IsEnabled = true;
            }
        }

        private async void OnContributeClicked(object? sender, EventArgs e)
        {
            await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(
                new Uri("https://translate.moto-editor.dev"));
        }

        private void OnTabInstalledClicked(object? sender, EventArgs e)
        {
            _currentTab = TabKind.Installed;
            UpdateTabStyles();
            RefreshView();
        }

        private void OnTabMarketplaceClicked(object? sender, EventArgs e)
        {
            _currentTab = TabKind.Marketplace;
            UpdateTabStyles();
            RefreshView();
        }

        private void OnTabContributeClicked(object? sender, EventArgs e)
        {
            _currentTab = TabKind.Contribute;
            UpdateTabStyles();
            RefreshView();
        }

        private void UpdateTabStyles()
        {
            var accent = (Color)Application.Current.Resources["Accent"];
            var side = (Color)Application.Current.Resources["BgSide"];
            var txt1 = (Color)Application.Current.Resources["Txt1"];

            TabInstalledBtn.BackgroundColor = _currentTab == TabKind.Installed ? accent : side;
            TabInstalledBtn.TextColor = _currentTab == TabKind.Installed ? Colors.White : txt1;
            TabMarketplaceBtn.BackgroundColor = _currentTab == TabKind.Marketplace ? accent : side;
            TabMarketplaceBtn.TextColor = _currentTab == TabKind.Marketplace ? Colors.White : txt1;
            TabContributeBtn.BackgroundColor = _currentTab == TabKind.Contribute ? accent : side;
            TabContributeBtn.TextColor = _currentTab == TabKind.Contribute ? Colors.White : txt1;
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
