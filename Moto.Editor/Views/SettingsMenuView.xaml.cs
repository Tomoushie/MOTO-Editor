// Moto.Editor/Views/SettingsMenuView.xaml.cs (ajouts)
using Moto.Core.Plugins;
using Moto.Core.Settings.Profiles;

namespace Moto.Editor.Views
{
    public partial class SettingsMenuView : ContentView
    {
        private readonly PluginRegistry _plugins;
        private readonly ProfileManager _profiles;
        private readonly SettingsImporterExporter _importerExporter;

        public SettingsMenuView(
            PluginRegistry plugins,
            ProfileManager profiles,
            SettingsImporterExporter importerExporter)
        {
            InitializeComponent();
            _plugins = plugins;
            _profiles = profiles;
            _importerExporter = importerExporter;

            LoadProfiles();
            LoadPlugins();
        }

        private void LoadProfiles()
        {
            var profiles = _profiles.LoadCustomProfiles();
            ProfilesList.Children.Clear();

            foreach (var profile in profiles)
            {
                var button = new Button
                {
                    Text = profile.DisplayName,
                    BackgroundColor = (Color)Application.Current.Resources["BgPanel"],
                    TextColor = (Color)Application.Current.Resources["Txt1"]
                };
                button.Clicked += (s, e) =>
                {
                    _profiles.ApplyProfile(profile);
                    StatusBar.SetStatus($"✅ Profil appliqué : {profile.DisplayName}");
                };
                ProfilesList.Children.Add(button);
            }

            // Bouton "Sauvegarder comme profil"
            var saveButton = new Button
            {
                Text = "💾 Sauvegarder comme profil",
                BackgroundColor = (Color)Application.Current.Resources["Accent"]
            };
            saveButton.Clicked += async (s, e) =>
            {
                var name = await Application.Current.MainPage.DisplayPromptAsync(
                    "Profil", "Nom du profil :");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _profiles.SaveCurrentAsProfile(name);
                    LoadProfiles();
                }
            };
            ProfilesList.Children.Add(saveButton);
        }

        private void LoadPlugins()
        {
            var plugins = _plugins.GetActivePlugins();
            PluginsList.Children.Clear();

            foreach (var plugin in plugins)
            {
                var toggle = new Switch
                {
                    IsToggled = _plugins.IsPluginEnabled(plugin.Id)
                };
                toggle.Toggled += async (s, e) =>
                {
                    await _plugins.TogglePluginAsync(plugin.Id, e.Value, /* services */ null!);
                };

                var label = new Label
                {
                    Text = $"{plugin.DisplayName} v{plugin.Version}",
                    VerticalOptions = LayoutOptions.Center
                };

                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(toggle);
                row.Children.Add(label);
                PluginsList.Children.Add(row);
            }
        }

        private async void OnExportClicked(object sender, EventArgs e)
        {
            var path = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Exporter les paramètres",
                FileTypes = FilePickerFileType.Json
            });

            if (path != null)
            {
                _importerExporter.Export(path.FullPath);
                StatusBar.SetStatus($"✅ Paramètres exportés : {path.FileName}");
            }
        }

        private async void OnImportClicked(object sender, EventArgs e)
        {
            var path = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Importer les paramètres",
                FileTypes = FilePickerFileType.Json
            });

            if (path != null)
            {
                var result = _importerExporter.Import(path.FullPath);
                if (result.Success)
                    StatusBar.SetStatus($"✅ Importé : {result.AppliedCount} paramètres");
                else
                    StatusBar.SetStatus($"❌ Erreur : {result.ErrorMessage}");
            }
        }
    }
}
