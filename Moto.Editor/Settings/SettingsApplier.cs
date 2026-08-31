// Moto.Editor/Settings/SettingsApplier.cs (régénéré — application étendue)
using System;
using Microsoft.Maui.Controls;
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
            // ★ CORRECTION (31/08) : "Light"/"System" appelaient réellement
            // ThemeService.SetLight()/FollowSystem() ici — un CHEMIN SÉPARÉ de celui
            // déjà corrigé dans MainPage.UI.cs (OnSettingChanged, déclenché par
            // l'ancien SettingsMenuView). La nouvelle fenêtre de Réglages passe par
            // CE chemin-ci (SettingsEngine.Set → SettingChanged → ApplyAll) : sans ce
            // correctif, le bug "texte noir sur fond noir" revenait par cette porte.
            // Aucune palette claire n'existe réellement — voir l'explication déjà
            // donnée à Tom.
            switch (s.GetString("theme_mode"))
            {
                case "Dark": ThemeService.SetDark(); break;
                default: ThemeService.SetDark(); break;
            }

            // Éditeur
            var bufferFontSize = s.GetInt("buffer_font_size");
            editor.FontSizeMode = bufferFontSize;
            vm.IsMiniMapVisible = s.GetBool("minimap_show");
            vm.IsDiagnosticsVisible = s.GetBool("lsp_diagnostics");

            // ★ AJOUT (31/08) : le curseur "taille de police" (Réglages) ne changeait
            // que le contenu des fichiers ouverts — demandé par Tom (point 9) : qu'il
            // change aussi le texte du menu et de l'interface. Dérivé de la même
            // valeur (buffer_font_size, taille de référence = 14), avec des bornes
            // raisonnables pour rester lisible/utilisable aux extrêmes du curseur.
            if (bufferFontSize > 0 && Application.Current is not null)
            {
                var scale = bufferFontSize / 14.0;
                Application.Current.Resources["FontSizeUiText"] = Math.Clamp(13 * scale, 10.0, 22.0);
                Application.Current.Resources["FontSizeUiSmall"] = Math.Clamp(11 * scale, 8.0, 18.0);
            }

            // Terminal (consommé par TerminalView)
            // Les vues lisent SettingsEngine.Shared au rendu.
        }

        public static void Subscribe(MainViewModel vm, CodeEditorView editor, SettingsEngine s)
        {
            s.SettingChanged += (id, _) => ApplyAll(vm, editor, s);
        }
    }
}
