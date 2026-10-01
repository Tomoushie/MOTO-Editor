// Moto.Editor/Settings/StatusBarSettings.cs
// ★ AJOUT (01/10) : mappage des réglages de la famille « Fenêtre & Layout / Status Bar »
// (clés sb_*) et des boutons d'action de la barre de statut (gp_button/cp_button/ap_button).
// Ces réglages étaient déclarés au catalogue mais lus par AUCUN code :
// StatusBarPanelView.ApplySettings(SettingsEngine) était une méthode VIDE, conservée
// comme point d'extension depuis l'origine (« Rien à appliquer pour l'instant »).
//
// ★ ÉVOLUTION (01/10, décision C item 1 tranche 1) : les 7 boutons d'action ont été
// CRÉÉS dans StatusBarPanelView.xaml (voir ce fichier), donc leurs clés de visibilité
// deviennent câblables — ce fichier lit maintenant 9 clés au total :
//   • sb_diagnostics / sb_active_file : déjà câblées avant cette tranche (donnée réelle).
//   • sb_project_panel / sb_terminal / sb_search / sb_debugger / gp_button / cp_button /
//     ap_button : visibilité du bouton correspondant (le CLIC est câblé par MainPage).
//
// ⚠️ RESTENT INERTES (hors périmètre de cette tranche, constat VÉRIFIÉ) :
//   • les 4 puces de DONNÉES sb_language / sb_encoding / sb_line_endings /
//     sb_cursor_position : aucune donnée réelle n'existe dans le dépôt
//     (pas de notion d'encodage / de fins de ligne ; la position du curseur vit dans
//     le WebView CodeEditorView, sans copie C# consultable). Afficher ces valeurs
//     obligerait à INVENTER un contenu, ce qui est pire qu'un réglage inerte.
//   • op_button : le panneau outline n'existe pas — c'est l'item 3 de la feuille de
//     route (décision C), pas un câblage.
//
// Les clés câblées ont chacune une donnée/action RÉELLE disponible :
//   • sb_diagnostics : compteurs d'erreurs/avertissements (ErrorsLabel/WarningsLabel).
//   • sb_active_file : nom du fichier actif (MainViewModel.SelectedDocument).
//   • les 7 boutons : actions réelles vérifiées dans MainPage.Routing.cs /
//     MainPage.Extensions.cs (toggle explorateur/recherche/IA/collab/terminal, fenêtres
//     « debug » et « git »).
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

        /// <summary>Réglage <c>sb_line_endings</c> : puce « CRLF/LF » (fins de ligne du fichier actif).</summary>
        /// <remarks>★ AJOUT (01/10, tranche 2) : la donnée est maintenant RÉELLE — détectée
        /// depuis <c>EditorDocument.Text</c> (le texte tel que chargé, dont <c>File.ReadAllText</c>
        /// préserve les fins de ligne), jamais inventée.</remarks>
        public static bool ShowLineEndings(SettingsEngine s)
            => s.GetBool("sb_line_endings", DeclaredBool("sb_line_endings"));

        /// <summary>Réglage <c>sb_language</c> : puce « langage » (nom lisible du langage du fichier actif).</summary>
        /// <remarks>★ AJOUT (01/10, tranche 2) : la donnée est RÉELLE — nom lisible détecté
        /// par <c>CodeEditorView.LanguageDisplayName(path)</c>, jamais inventé.</remarks>
        public static bool ShowLanguage(SettingsEngine s)
            => s.GetBool("sb_language", DeclaredBool("sb_language"));

        /// <summary>Réglage <c>sb_cursor_position</c> : puce « L x, C y » (position du curseur).</summary>
        /// <remarks>★ AJOUT (01/10, tranche 2) : la donnée est RÉELLE — position du curseur
        /// poussée par le WebView (CodeEditorView.SelectionChanged) et convertie en
        /// ligne:colonne par MainPage, jamais inventée.</remarks>
        public static bool ShowCursorPosition(SettingsEngine s)
            => s.GetBool("sb_cursor_position", DeclaredBool("sb_cursor_position"));

        // ★ AJOUT (01/10, décision C item 1 tranche 1) : visibilité des 7 boutons d'action.
        // Chaque clé est lue AVEC son défaut déclaré au catalogue (tous à true pour ces
        // 7 clés — voir SettingsCatalog.cs) : sans ce second argument, GetBool retomberait
        // sur false pour une clé absente du store et MASQUERAIT le bouton sur une
        // installation neuve (piège documenté plus haut). Le clic lui-même est câblé par
        // MainPage (StatusBar.XxxTapped), pas ici — ce fichier ne gère que l'AFFICHAGE.

        /// <summary>Réglage <c>sb_project_panel</c> : bouton « Projet » (toggle explorateur).</summary>
        public static bool ShowProjectPanel(SettingsEngine s)
            => s.GetBool("sb_project_panel", DeclaredBool("sb_project_panel"));

        /// <summary>Réglage <c>sb_terminal</c> : bouton « Terminal » (toggle dock bas).</summary>
        public static bool ShowTerminal(SettingsEngine s)
            => s.GetBool("sb_terminal", DeclaredBool("sb_terminal"));

        /// <summary>Réglage <c>sb_search</c> : bouton « Recherche » (toggle panneau de recherche).</summary>
        public static bool ShowSearch(SettingsEngine s)
            => s.GetBool("sb_search", DeclaredBool("sb_search"));

        /// <summary>Réglage <c>sb_debugger</c> : bouton « Debug » (fenêtre spécialisée « debug »).</summary>
        public static bool ShowDebugger(SettingsEngine s)
            => s.GetBool("sb_debugger", DeclaredBool("sb_debugger"));

        /// <summary>Réglage <c>gp_button</c> : bouton « Git » (fenêtre spécialisée « git »).</summary>
        public static bool ShowGit(SettingsEngine s)
            => s.GetBool("gp_button", DeclaredBool("gp_button"));

        /// <summary>Réglage <c>cp_button</c> : bouton « Collab » (toggle panneau collaboration).</summary>
        public static bool ShowCollab(SettingsEngine s)
            => s.GetBool("cp_button", DeclaredBool("cp_button"));

        /// <summary>Réglage <c>ap_button</c> : bouton « IA » (toggle panneau de chat IA).</summary>
        public static bool ShowAiPanel(SettingsEngine s)
            => s.GetBool("ap_button", DeclaredBool("ap_button"));
    }
}
