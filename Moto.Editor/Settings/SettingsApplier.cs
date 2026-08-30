// Moto.Editor/Settings/SettingsApplier.cs (régénéré — application étendue)
using Moto.Core.Settings;
using Moto.Editor.Controls;
using Moto.Editor.Services;
using Moto.Editor.ViewModels;
using Moto.Editor.Views;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// Applique les paramètres en direct.
    /// Les paramètres de layout (docks, onglets, status bar) sont
    /// appliqués par MainPage via ApplyLayout().
    /// </summary>
    public static class SettingsApplier
    {
        public static void ApplyAll(MainViewModel vm, CodeEditorView editor, SettingsEngine s)
        {
            // Thème
            switch (s.GetString("theme_mode"))
            {
                case "Light": ThemeService.SetLight(); break;
                case "Dark": ThemeService.SetDark(); break;
                default: ThemeService.FollowSystem(); break;
            }

            // Éditeur
            editor.FontSizeMode = s.GetInt("buffer_font_size");
            vm.IsMiniMapVisible = s.GetBool("minimap_show");
            vm.IsDiagnosticsVisible = s.GetBool("lsp_diagnostics");

            // Terminal (consommé par TerminalView)
            // Les vues lisent SettingsEngine.Shared au rendu.
        }

        public static void Subscribe(MainViewModel vm, CodeEditorView editor, SettingsEngine s)
        {
            s.SettingChanged += (id, _) => ApplyAll(vm, editor, s);
        }
    }
}
