// Moto.Editor/Settings/GitSettings.cs
using System;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10, chantier <c>git_*</c>) : lit les réglages de la famille
    /// « Version Control » (les 16 clés <c>git_*</c> déclarées dans
    /// <c>SettingsCatalog.Extensions.cs</c>). Jusqu'ici, AUCUNE de ces clés n'était lue par
    /// du code compilé — la fenêtre Réglages les affichait, rien ne les appliquait (constat
    /// mesuré par <c>scripts/settings-coverage.ps1</c> : seules occurrences, le catalogue et
    /// la documentation). Quatrième famille de la même méthode, après
    /// <see cref="TabBarSettings"/> (28/09, <c>tabs_*</c>), <see cref="PanelSettings"/> et
    /// <see cref="GitPanelSettings"/> (01/10, <c>pp_*</c> / <c>gp_*</c>).
    ///
    /// ⚠️ PIÈGE PAYÉ TROIS FOIS (28/09, 01/10) — c'est LE piège de ce dépôt :
    /// <c>SettingsEngine.GetBool("clé")</c> SANS second argument renvoie <c>false</c> quand la
    /// clé est absente du store — le moteur ne consulte JAMAIS le catalogue
    /// (<c>SettingsEngineCore.cs</c> : <c>GetBool(key, false)</c>). Sur une installation neuve,
    /// appliquer <c>git_integration</c> sans défaut déclaré aurait donc FERMIÉ le panneau Git
    /// que le catalogue déclare à <c>true</c>. Toutes les lectures ci-dessous passent
    /// explicitement le défaut DÉCLARÉ, seule source de vérité.
    ///
    /// CE QUI EST RÉELLEMENT APPLIQUÉ (4 clés sur 16, et où) :
    ///   - <c>git_integration</c> : gâchette d'ouverture de la fenêtre « Git » —
    ///     <c>MainPage.OpenSpecializedWindow</c> (case <c>"git"</c>, qui sert aussi la palette
    ///     <c>git.panel</c> et l'ouverture au démarrage) refuse d'ouvrir quand la gâchette est
    ///     décochée, et <c>MainPage.ApplyLayoutSettings</c> FERME la fenêtre si elle était déjà
    ///     ouverte au moment du changement (sinon l'affichage mentirait : réglage décoché,
    ///     panneau visible). Portée annoncée : « panneau, gutter, blame » — le panneau existe ;
    ///     le gutter git et le blame sont des fonctionnalités absentes du produit (voir inertes
    ///     plus bas), la gâchette ne peut donc pas les toucher. Les indicateurs git de
    ///     l'explorateur restent gouvernés par leur propre famille <c>pp_git_*</c> — une
    ///     seconde gâchette sur les mêmes afficheurs créerait deux interrupteurs concurrents ;
    ///   - <c>git_path_style</c> : libellé affiché pour chaque fichier des trois listes du
    ///     panneau Git — <c>GitChangeNode.DisplayPath</c>, « File Name First » (défaut déclaré)
    ///     = <c>Nom (dossier)</c>, « Path First » = le chemin complet renvoyé par git (l'affichage
    ///     d'avant ce chantier) ;
    ///   - <c>git_stage_restore_buttons</c> : visibilité des boutons stage/restore du panneau —
    ///     colonne de boutons de <c>GitPanelView.xaml</c> pilotée par
    ///     <c>GitChangeNode.StageButtonsColumnWidth</c>. ⚠️ DIVERGENCE DOCUMENTÉE : le
    ///     descriptif annonce « sur les hunks de diff », or aucun hunk n'existe dans le produit
    ///     (voir <c>git_hunk_style</c>) ; câbler sur les seuls widgets RÉELLEMENT présents —
    ///     les boutons stage/restore PAR FICHIER, qui ont exactement ce rôle — en documentant
    ///     l'écart, même décision que <c>ap_*</c> (libellé pointant un panneau inexistant,
    ///     câblage sur le vrai équivalent) ;
    ///   - <c>git_diff_base</c> : base du « Diff du projet » (section dépliée au clic,
    ///     <c>GitPanelView.ShowProjectDiffAsync</c>) — « Head » (défaut déclaré) =
    ///     <c>git diff</c> sans argument, comportement inchangé ; « Default Branch » =
    ///     <c>GitService.GetDefaultBranchAsync()</c> (<c>git symbolic-ref refs/remotes/origin/HEAD</c>),
    ///     la base étant annoncée dans la ligne de statut. Branche par défaut introuvable
    ///     (dépôt local sans remote) = repli sur HEAD + message explicite, jamais une branche
    ///     devinée.
    ///
    /// CE QUI RESTE VOLONTAIREMENT INERTE (12 clés, une raison par clé, jamais un oubli —
    /// détail en fin de fichier) : le gutter git (2), le blame inline (7), le branch picker (1),
    /// le visualiseur de diff complet (1) et le style de hunks (1) — cinq fonctionnalités
    /// TOTALEMENT absentes du produit : câbler ces clés ne serait pas « rendre un réglage
    /// opérant » mais construire une fonctionnalité, et afficher un réglage de blame alors
    /// qu'aucun blame n'existe serait exactement le faux état que la règle du dépôt interdit.
    ///
    /// Note de recouvrement : la clé legacy <c>git.enabled</c> (pointée, autre famille —
    /// <c>SettingsCatalog.Git.cs</c>) est lue dans <c>GitService.InitAsync</c> et ne porte QUE
    /// sur <c>git init</c> ; aucun conflit avec <c>git_integration</c>.
    /// </summary>
    internal static class GitSettings
    {
        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte/énuméré (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

        /// <summary>Défaut déclaré au catalogue pour un réglage entier.</summary>
        internal static int DeclaredInt(string id) => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

        // ------------------------------------------------------------------
        // Accès typés
        // ------------------------------------------------------------------

        /// <summary>Vrai si les fonctionnalités Git (fenêtre « Git ») sont activées (défaut déclaré : oui).</summary>
        internal static bool Integration(SettingsEngine s)
            => s.GetBool("git_integration", DeclaredBool("git_integration"));

        /// <summary>
        /// Style de chemin du panneau Git : « Path First » = chemin complet
        /// (comportement historique), « File Name First » (défaut déclaré) = nom du fichier
        /// d'abord, dossier entre parenthèses.
        /// </summary>
        internal static bool PathIsPathFirst(SettingsEngine s)
            => string.Equals(
                s.GetString("git_path_style", DeclaredString("git_path_style")),
                "Path First",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Vrai si les boutons stage/restore du panneau sont visibles (défaut déclaré : oui).</summary>
        internal static bool StageRestoreButtons(SettingsEngine s)
            => s.GetBool("git_stage_restore_buttons", DeclaredBool("git_stage_restore_buttons"));

        /// <summary>
        /// Base du « Diff du projet » : « Default Branch » = diff contre la branche par défaut
        /// du remote ; « Head » (défaut déclaré) = <c>git diff</c> sans argument.
        /// </summary>
        internal static bool DiffBaseIsDefaultBranch(SettingsEngine s)
            => string.Equals(
                s.GetString("git_diff_base", DeclaredString("git_diff_base")),
                "Default Branch",
                StringComparison.OrdinalIgnoreCase);

        // ★ AJOUT (01/10, décision C git gutter) : les 2 clés du gutter git, désormais RÉELLES
        // (voir GitService.GetChangedLineNumbersAsync + CodeEditorView.SetGitChangedLines).

        /// <summary>Réglage <c>git_gutter_visibility</c> : affiche les marqueurs git dans le gutter.</summary>
        internal static bool ShowGutter(SettingsEngine s)
            => s.GetBool("git_gutter_visibility", DeclaredBool("git_gutter_visibility"));

        /// <summary>Réglage <c>git_gutter_debounce</c> : délai (ms) avant de rafraîchir les marqueurs.</summary>
        internal static int GutterDebounce(SettingsEngine s)
            => s.GetInt("git_gutter_debounce", DeclaredInt("git_gutter_debounce"));

        // ★ AJOUT (01/10, décision C git blame) : les clés du blame inline, désormais RÉELLES
        // (voir GitService.GetBlameAsync + la puce BlameLabel de la barre de statut).

        /// <summary>Réglage <c>git_blame_enabled</c> : affiche le blame sur la ligne focus.</summary>
        internal static bool ShowBlame(SettingsEngine s)
            => s.GetBool("git_blame_enabled", DeclaredBool("git_blame_enabled"));

        /// <summary>Réglage <c>git_blame_delay</c> : délai (ms) avant d'afficher le blame.</summary>
        internal static int BlameDelay(SettingsEngine s)
            => s.GetInt("git_blame_delay", DeclaredInt("git_blame_delay"));

        /// <summary>Réglage <c>git_blame_commit_summary</c> : inclut le résumé dans le blame.</summary>
        internal static bool BlameCommitSummary(SettingsEngine s)
            => s.GetBool("git_blame_commit_summary", DeclaredBool("git_blame_commit_summary"));
    }

    // =====================================================================
    // Clés git_* VOLONTAIREMENT LAISSÉES INERTES (décision, pas oubli)
    // =====================================================================
    //
    // 12 des 16 clés visent des fonctionnalités qui N'EXISTENT PAS dans le produit.
    // Le grep exhaustif `git_[a-z_]*` ne trouve ces clés QUE dans le catalogue et la doc ;
    // et aucun des concepts annoncés n'a de code, de vue ni de commande git associés.
    // Les câbler afficherait des réglages sans effet (pire : un réglage de blame alors
    // qu'aucun blame n'existe) — même décision que gp_button / gp_count_badge (01/10)
    // et pp_count_badge (01/10).
    //
    // • git_gutter_visibility, git_gutter_debounce — « statut Git dans la gouttière » :
    //   le gutter de l'éditeur (CodeEditorView, WebView) ne contient que des numéros de
    //   ligne (paint() écrit gut.textContent) ; aucune commande git ne calcule un statut
    //   PAR LIGNE (git status --porcelain ne renvoie que des fichiers), et le seul mécanisme
    //   d'overlay du gutter est une TODO vide (EditorPaneView.Lsp). Construire cette clé =
    //   construire la fonctionnalité gutter, pas l'activer.
    //
    // • git_blame_enabled, git_blame_location, git_blame_delay, git_blame_padding,
    //   git_blame_min_column, git_blame_commit_summary, git_blame_avatar — « blame inline » :
    //   aucune commande git blame n'est jamais invoquée (grep = 0 dans le code produit),
    //   aucune vue de blame n'existe, GitCommit ne porte même pas l'auteur (git log --oneline),
    //   et la documentation du projet annonce le blame « À venir (v1.0) ».
    //
    // • git_branch_author — « auteur dans le branch picker » : il n'y a PAS de branch picker
    //   (le bouton 🌿 Branches écrit les noms dans la ligne de statut), CheckoutAsync n'a
    //   aucun appelant, et la donnée auteur n'est lue nulle part.
    //
    // • git_diff_full_file — « ouvre le diff complet au lieu des seuls changements » :
    //   aucun visualiseur de diff n'existe — le clic ouvre le fichier dans l'éditeur ou
    //   déplie une liste de résumés « +n −m » ; il n'y a rien à basculer entre deux vues.
    //
    // • git_hunk_style — « style des hunks » : aucun rendu de hunks nulle part (le seul
    //   coloriage +/−/@@ est ConfirmationOverlay, réservé aux confirmations d'agents, sans
    //   notion staged/solid/pattern).
}
