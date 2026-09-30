// Moto.Editor/Settings/TabBarSettings.cs
using System;
using Moto.Core.Settings;
using Moto.Editor.Models;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (28/09) : lit et applique les réglages de la famille « Fenêtre &amp; Layout / Tab Bar »
    /// (clés <c>tabs_*</c>) à un onglet de l'éditeur.
    ///
    /// POURQUOI CE FICHIER : ces clés étaient déclarées dans le catalogue de Réglages mais lues par
    /// AUCUN code compilé — la fenêtre Réglages les affichait, rien ne les appliquait (constat mesuré
    /// par scripts/settings-coverage.ps1). Le mappage vit ici, en UN seul endroit, pour deux appelants :
    ///   - <see cref="Moto.Editor.Controls.EditorPaneView.ApplySettings"/> : tous les onglets déjà
    ///     ouverts, au démarrage et à chaque changement d'un réglage tabs_* ;
    ///   - <see cref="Moto.Editor.ViewModels.MainViewModel.OpenFilePath"/> : l'onglet qui vient
    ///     d'être créé, sinon un onglet ouvert APRÈS le réglage garderait les valeurs par défaut.
    ///
    /// ⚠️ PIÈGE PAYÉ (28/09) : <c>SettingsEngine.GetBool("clé")</c> SANS second argument renvoie
    /// <c>false</c> quand la clé est absente du store — le moteur ne consulte JAMAIS le catalogue
    /// (vérifié dans SettingsEngineCore.cs : <c>GetBool(key, false)</c> → <c>GetInt(key, 0)</c> →
    /// <c>GetString(key, "")</c>). Sur une installation neuve, appliquer <c>tabs_show</c> sans défaut
    /// aurait donc MASQUÉ la barre d'onglets, et <c>tabs_file_icons</c> aurait fait disparaître les
    /// icônes. Les lectures ci-dessous passent donc explicitement le défaut DÉCLARÉ au catalogue,
    /// qui reste ainsi la seule source de vérité.
    ///
    /// Ce qui n'est PAS fait ici (volontairement, voir le rapport) : <c>tabs_git_status</c> et
    /// <c>tabs_pinned_layout</c> — aucune donnée de statut git par onglet n'existe dans le dépôt, et
    /// aucun concept d'onglet épinglé n'existe pour l'éditeur. Afficher l'un ou l'autre obligerait à
    /// INVENTER une valeur, ce qui est pire qu'un réglage inerte.
    /// </summary>
    internal static class TabBarSettings
    {
        /// <summary>Applique les réglages tabs_* à un onglet (sans effet si <paramref name="doc"/> est nul).</summary>
        internal static void Apply(EditorDocument doc, SettingsEngine settings)
        {
            if (doc is null || settings is null) return;

            // Icône de type de fichier dans l'onglet.
            doc.ShowFileGlyph = settings.GetBool("tabs_file_icons", DeclaredBool("tabs_file_icons"));

            // Pastille erreurs/warnings : n'affiche jamais autre chose que les VRAIES erreurs
            // (EditorDocument.ErrorCount) — le réglage ne fait que l'autoriser ou non.
            doc.ShowDiagnosticsBadge = string.Equals(
                settings.GetString("tabs_show_diagnostics", DeclaredString("tabs_show_diagnostics")),
                "On", StringComparison.OrdinalIgnoreCase);

            // Position de la croix de fermeture (avant ou après le titre).
            doc.CloseOnLeft = string.Equals(
                settings.GetString("tabs_close_position", DeclaredString("tabs_close_position")),
                "Left", StringComparison.OrdinalIgnoreCase);

            // Comportement de la croix : Always / Hover / Hidden (EditorDocument borne les valeurs inconnues à Always).
            doc.CloseMode = settings.GetString("tabs_show_close", DeclaredString("tabs_show_close"));
        }

        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage entier.</summary>
        internal static int DeclaredInt(string id) => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte/énuméré (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;
    }
}
