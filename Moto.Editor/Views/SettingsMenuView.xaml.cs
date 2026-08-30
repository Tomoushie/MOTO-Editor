// Moto.Editor/Views/SettingsMenuView.xaml.cs
// Reconstruit (30/08) : ce fichier ne correspondait pas du tout à SettingsMenuView.xaml
// (il référençait ProfilesList/PluginsList, des éléments qui n'existent nulle part
// dans le vrai .xaml — celui-ci est en fait complet et valide : Thème/Puissance/
// Police/Mini-map/Terminal/3 boutons). Réécrit pour correspondre au vrai XAML.
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Views
{
    public partial class SettingsMenuView : ContentView
    {
        /// <summary>
        /// Déclenché quand un réglage change. Clés reconnues par MainPage.OnSettingChanged :
        /// "theme" (int), "minimap" (bool), "terminal" (bool), "openproviders" (bool).
        /// </summary>
        public event Action<string, object>? SettingChanged;

        public SettingsMenuView()
        {
            InitializeComponent();

            // Valeurs initiales depuis les réglages persistés.
            var theme = SettingsEngine.Shared.GetString("theme_mode");
            ThemePicker.SelectedIndex = theme switch { "Dark" => 0, "Light" => 1, _ => 2 };

            var power = SettingsEngine.Shared.GetString("power_mode", "Balanced");
            PowerPicker.SelectedIndex = power switch { "Eco" => 0, "Balanced" => 1, "Turbo" => 2, "Ultra" => 3, _ => 1 };

            FontSlider.Value = SettingsEngine.Shared.GetInt("buffer_font_size", 14);
            MiniMapSwitch.IsToggled = SettingsEngine.Shared.GetBool("minimap_show", defaultValue: true);
            TerminalSwitch.IsToggled = SettingsEngine.Shared.GetBool("terminal_show");
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            var mode = ThemePicker.SelectedIndex switch { 0 => "Dark", 1 => "Light", _ => "System" };
            SettingsEngine.Shared.Set("theme_mode", mode);
            SettingChanged?.Invoke("theme", ThemePicker.SelectedIndex);
        }

        private void OnPowerChanged(object sender, EventArgs e)
        {
            var mode = PowerPicker.SelectedItem as string ?? "Balanced";
            SettingsEngine.Shared.Set("power_mode", mode);
        }

        private void OnFontChanged(object sender, ValueChangedEventArgs e)
        {
            SettingsEngine.Shared.Set("buffer_font_size", (int)e.NewValue);
        }

        private void OnMiniMapToggled(object sender, ToggledEventArgs e)
        {
            SettingsEngine.Shared.Set("minimap_show", e.Value);
            SettingChanged?.Invoke("minimap", e.Value);
        }

        private void OnTerminalToggled(object sender, ToggledEventArgs e)
        {
            SettingsEngine.Shared.Set("terminal_show", e.Value);
            SettingChanged?.Invoke("terminal", e.Value);
        }

        private void OnProvidersClicked(object sender, EventArgs e)
            => SettingChanged?.Invoke("openproviders", true);

        /// <summary>Time Machine (historique de snapshots) : pas encore relié à une UI dédiée.</summary>
        private void OnSnapshotClicked(object sender, EventArgs e)
            => System.Diagnostics.Debug.WriteLine("[SettingsMenu] Snapshot Time Machine demandé.");

        /// <summary>Santé du projet : pas encore relié à une UI dédiée.</summary>
        private void OnHealthClicked(object sender, EventArgs e)
            => System.Diagnostics.Debug.WriteLine("[SettingsMenu] Analyse de santé demandée.");
    }
}
