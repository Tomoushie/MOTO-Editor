// Moto.Editor/Settings/StatusBarSettings.cs
// ★ AJOUT (01/10) : mappage des réglages de la famille « Fenêtre & Layout / Status Bar »
// (clés sb_*). Ces réglages étaient déclarés au catalogue mais lus par AUCUN code :
// StatusBarPanelView.ApplySettings(SettingsEngine) était une méthode VIDE, conservée
// comme point d'extension depuis l'origine (« Rien à appliquer pour l'instant »).
//
// ⚠️ PORTÉE VOLONTAIREMENT RÉDUITE — 2 clés sur 10 seulement, et c'est un constat
// VÉRIFIÉ le 01/10, pas un abandon :
//
//   • 5 clés (sb_project_panel, sb_language, sb_terminal, sb_debugger, sb_search)
//     prétendent configurer « un bouton dans la barre de statut ». Or
//     StatusBarPanelView.xaml n'en contient AUCUN (seuls StatusLabel, RightChips,
//     ErrorsLabel, WarningsLabel, StateChips, SandboxLabel, LockedLabel,
//     AiStatusLabel). Les câbler demanderait de CRÉER les boutons : c'est un ajout
//     de fonctionnalité, pas un câblage. Hors périmètre ici.
//
//   • 2 clés se heurtent à une ABSENCE DE DONNÉE dans le dépôt :
//     - sb_encoding : aucune notion d'encodage de fichier n'existe dans Moto.Editor
//       (0 occurrence de LineEnding/EOL/Encoding sur le contenu des documents).
//     - sb_line_endings : idem, aucune notion de fins de ligne.
//     - sb_cursor_position : l'éditeur principal est CodeEditorView, un WebView —
//       la position du curseur vit côté JavaScript, il n'y en a aucune copie
//       consultable dans un modèle C#. GutterQuickActions.CurrentLine existe mais
//       appartient à l'ancien éditeur Skia, qui n'est plus celui utilisé.
//     Afficher l'une de ces trois valeurs obligerait à INVENTER un contenu, ce qui
//     est pire qu'un réglage inerte (doctrine du dépôt).
//
// Les 2 clés câblées ici ont, elles, une donnée RÉELLE disponible :
//   • sb_diagnostics  : les compteurs d'erreurs/avertissements existent déjà
//                       (ErrorsLabel / WarningsLabel, alimentés par SetCounts).
//   • sb_active_file  : le nom du fichier actif existe (MainViewModel.SelectedDocument).
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// Lecture des réglages <c>sb_*</c> avec leur valeur par défaut DÉCLARÉE au
    /// catalogue. Même patron que <see cref="TabBarSettings"/> / <see cref="PanelSettings"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ne JAMAIS lire ces clés avec <c>SettingsEngine.GetBool(clé)</c> sans second
    /// argument : cet appel ne consulte pas le catalogue et retombe sur <c>false</c>
    /// pour une clé absente du store — l'élément DISPARAÎTRAIT sur une installation
    /// neuve (piège constaté le 28/09). D'où les trois helpers ci-dessous.
    /// </remarks>
    public static class StatusBarSettings
    {
        /// <summary>Défaut booléen déclaré au catalogue pour cette clé.</summary>
        public static bool DeclaredBool(string id)
        {
            var item = SettingsCatalog.ById(id);
            if (item?.Default is bool b) return b;
            return false;
        }

        /// <summary>Réglage <c>sb_diagnostics</c> : compteurs d'erreurs/avertissements.</summary>
        public static bool ShowDiagnostics(SettingsEngine s)
            => s.GetBool("sb_diagnostics", DeclaredBool("sb_diagnostics"));

        /// <summary>Réglage <c>sb_active_file</c> : nom du fichier affiché dans l'éditeur.</summary>
        public static bool ShowActiveFile(SettingsEngine s)
            => s.GetBool("sb_active_file", DeclaredBool("sb_active_file"));
    }
}
