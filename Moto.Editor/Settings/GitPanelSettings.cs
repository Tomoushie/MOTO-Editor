// Moto.Editor/Settings/GitPanelSettings.cs
using System;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10) : lit et applique les réglages de la famille « Panneaux / Git Panel »
    /// (clés <c>gp_*</c>) — le VRAI panneau Git du dépôt
    /// (<see cref="Moto.Editor.Views.GitPanelView"/>, fenêtre spécialisée « Git » + palette
    /// <c>git.panel</c>), alimenté par le VRAI <c>Moto.Core.Services.GitService</c> (git CLI).
    ///
    /// POURQUOI CE FICHIER : ces 15 clés étaient déclarées au catalogue depuis l'origine mais
    /// lues par AUCUN code compilé — la fenêtre Réglages les affichait, rien ne les appliquait
    /// (constat mesuré par <c>scripts/settings-coverage.ps1</c> : seule occurrence du dépôt,
    /// le catalogue lui-même). Troisième famille de la même méthode, après
    /// <see cref="TabBarSettings"/> (28/09, <c>tabs_*</c>) et <see cref="PanelSettings"/>
    /// (01/10, <c>pp_*</c>) : un seul endroit qui traduit le catalogue en effets réels.
    ///
    /// ⚠️ PIÈGE PAYÉ DEUX FOIS (28/09, 01/10) — c'est LE piège de ce dépôt :
    /// <c>SettingsEngine.GetBool("clé")</c> SANS second argument renvoie <c>false</c> quand la
    /// clé est absente du store — le moteur ne consulte JAMAIS le catalogue
    /// (<c>SettingsEngineCore.cs</c> : <c>GetBool(key, false)</c>). Sur une installation neuve,
    /// appliquer <c>gp_diff_stats</c> ou <c>gp_button</c> sans défaut aurait donc MASQUÉ des
    /// statistiques et un bouton que le catalogue déclare à <c>true</c>. Toutes les lectures
    /// ci-dessous passent explicitement le défaut DÉCLARÉ, seule source de vérité.
    ///
    /// CE QUI EST RÉELLEMENT APPLIQUÉ (et où) :
    ///   - <c>gp_starts_open</c>, <c>gp_width</c>, <c>gp_dock</c> : état et géométrie de la
    ///     fenêtre « Git », dans <c>MainPage.ApplyLayoutSettings</c> (via
    ///     <c>ApplyPanelGeometrySettings</c>) — jamais dans la vue, qui ne possède ni la
    ///     fenêtre ni la colonne ;
    ///   - <c>gp_status_style</c>, <c>gp_sort</c>, <c>gp_group</c>, <c>gp_collapse_untracked</c>,
    ///     <c>gp_tree_view</c>, <c>gp_diff_stats</c>, <c>gp_commit_max_len</c>,
    ///     <c>gp_scrollbar</c> : affichage du panneau, dans <c>GitPanelView</c>, sur les données
    ///     réelles de <c>GitService.GetStatusAsync()</c> / <c>GetDiffStatsAsync()</c> ;
    ///   - <c>gp_click_behavior</c> : position du diff au clic sur un fichier (voir
    ///     <see cref="ResolveClickBehavior"/>, qui explique pourquoi « Project Diff » n'invente
    ///     rien).
    ///
    /// CE QUI RESTE VOLONTAIREMENT INERTE (une raison par clé, jamais un oubli) :
    ///   - <c>gp_button</c> : son libellé promet « Bouton git dans la barre de statut ». Une telle
    ///     puce n'existe pas dans <c>Views/StatusBarPanelView</c> (la vraie barre de statut), et
    ///     en ajouter une n'est pas « rendre un réglage opérant » mais construire un bouton —
    ///     qui devrait d'ailleurs décider quoi faire au clic, ce qu'aucun libellé ne dit ;
    ///   - <c>gp_fallback_branch</c> : câbler cette clé afficherait « main » dans un dossier qui
    ///     n'a AUCUNE branche (dossier vide, `git status` en échec). Ce ne serait pas une valeur
    ///     de repli mais une branche inventée, et exactement le cas d'école que la règle du
    ///     dépôt interdit. Le panneau dit déjà vrai en affichant « aucune branche détectée » ;
    ///   - <c>gp_count_badge</c> : la valeur serait <c>Staged+Unstaged+Untracked</c>, or git
    ///     liste un fichier à la fois dans « staged » et dans « unstaged » dès qu'il est indexé
    ///     PUIS remodifié (`git status --porcelain` renvoie « MM »), soit exactement l'état de
    ///     tous les commits de ce dépôt. Le badge ne peut donc PAS être posé sur le nombre de
    ///     fichiers modifiés sans devenir faux ; et le poser sur <c>Staged.Count</c> seul
    ///     (l'index, la définition VS Code) reste ambigu au regard du libellé, qui dit
    ///     « changements non commités » — c'est-à-dire tout le reste. Voir la note en fin de
    ///     fichier : laissé inerte, comme <c>pp_count_badge</c> au 01/10.
    /// </summary>
    internal static class GitPanelSettings
    {
        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage entier.</summary>
        internal static int DeclaredInt(string id) => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte/énuméré (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

        // ------------------------------------------------------------------
        // Accès typés
        // ------------------------------------------------------------------

        /// <summary>Vrai si le panneau git s'ouvre avec l'application (défaut déclaré : non).</summary>
        internal static bool StartsOpen(SettingsEngine s) => s.GetBool("gp_starts_open", DeclaredBool("gp_starts_open"));

        /// <summary>
        /// Largeur de la fenêtre Git, bornée aux bornes DÉCLARÉES au catalogue (200..1000).
        /// </summary>
        internal static int Width(SettingsEngine s)
        {
            var declared = DeclaredInt("gp_width");
            var value = s.GetInt("gp_width", declared);
            return Math.Clamp(value, 200, 1000);
        }

        /// <summary>
        /// Côté d'ancrage déclaré : "Left", "Right" (défaut) ou "Bottom". Toute autre valeur
        /// retombe sur le défaut DÉCLARÉ — la fenêtre ne peut donc jamais se retrouver dans
        /// une position que le catalogue n'annonce pas.
        /// </summary>
        internal static string Dock(SettingsEngine s)
        {
            var raw = s.GetString("gp_dock", DeclaredString("gp_dock"));
            if (string.Equals(raw, "Left", StringComparison.OrdinalIgnoreCase)) return "Left";
            if (string.Equals(raw, "Bottom", StringComparison.OrdinalIgnoreCase)) return "Bottom";
            return "Right";
        }

        /// <summary>
        /// Affiche la lettre d'état git en tête de ligne (« Style de statut » = Icon) ou le libellé
        /// complet (« Label »). Les deux valeurs sont des choix réels du catalogue, pas un
        /// synonyme : en « Label » on lit « indexé / modifié / non suivi » au lieu de « A / M / ? ».
        /// </summary>
        internal static bool StatusUsesIcons(SettingsEngine s)
            => !string.Equals(
                s.GetString("gp_status_style", DeclaredString("gp_status_style")),
                "Label",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Ordre des trois sections du panneau. Le catalogue propose Path (défaut), Name et Status.
        /// « Path » ne déplace rien : l'ordre actuel (Staged, Unstaged, Untracked) suit déjà la
        /// progression du cheminement d'un fichier dans git, et c'est l'ordre de référence.
        /// « Name » trie chaque section par NOM de fichier, « Status » par lettre d'état.
        /// </summary>
        internal static GitSortMode ResolveSort(SettingsEngine s)
        {
            var raw = s.GetString("gp_sort", DeclaredString("gp_sort"));
            if (string.Equals(raw, "Name", StringComparison.OrdinalIgnoreCase)) return GitSortMode.Name;
            if (string.Equals(raw, "Status", StringComparison.OrdinalIgnoreCase)) return GitSortMode.Status;
            return GitSortMode.Path;
        }

        /// <summary>
        /// Regroupement des entrées. Le catalogue propose Status (défaut) et Folder.
        /// « Status » correspond à l'état actuel du panneau (3 sections : indexé / modifié / non
        /// suivi) — le défaut déclaré décrit donc bien ce que le panneau affiche déjà.
        /// « Folder » ajoute le dossier parent en tête de chaque ligne : git ne renvoie QUE des
        /// chemins, donc regrouper par dossier voudrait dire inventer une arborescence que le
        /// panneau n'a pas — on choisit de montrer le dossier, pas de le fabriquer.
        /// </summary>
        internal static bool GroupByFolder(SettingsEngine s)
            => string.Equals(
                s.GetString("gp_group", DeclaredString("gp_group")),
                "Folder",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Replie la section « non suivis » (défaut déclaré : non repliée, donc visible).</summary>
        internal static bool CollapseUntracked(SettingsEngine s)
            => s.GetBool("gp_collapse_untracked", DeclaredBool("gp_collapse_untracked"));

        /// <summary>
        /// Affiche le chemin complet du fichier au lieu de son seul nom — « Vue arborescente ».
        /// ⚠️ Le panneau est une liste PLATE (trois CollectionView) : ce réglage ne peut pas
        /// inventer une arborescence. Il montre donc ce qui existe réellement et que la liste
        /// cachait jusqu'ici (le chemin relatif renvoyé par git) : « src/Views/Main.cs » au lieu
        /// de « Main.cs ». Voir la note de <c>ApplyEntryTransform</c> dans GitPanelView.
        /// </summary>
        internal static bool TreeView(SettingsEngine s)
            => s.GetBool("gp_tree_view", DeclaredBool("gp_tree_view"));

        /// <summary>Ajouts/suppressions par fichier (défaut déclaré : affichés).</summary>
        internal static bool DiffStats(SettingsEngine s)
            => s.GetBool("gp_diff_stats", DeclaredBool("gp_diff_stats"));

        /// <summary>
        /// Longueur maximale du TITRE de commit (0 = illimité, bornes déclarées 0..200).
        /// Appliquée en vrai sur le message envoyé à <c>git commit</c>, pas seulement à l'affichage.
        /// </summary>
        internal static int CommitMaxLength(SettingsEngine s)
        {
            var declared = DeclaredInt("gp_commit_max_len");
            var value = s.GetInt("gp_commit_max_len", declared);
            return Math.Clamp(value, 0, 200);
        }

        /// <summary>
        /// Visibilité de la barre de défilement de la liste git : "Auto" (défaut), "Always", "Never".
        /// Toute valeur inconnue retombe sur Auto (défaut déclaré).
        /// </summary>
        internal static ScrollBarVisibility ResolveScrollbar(SettingsEngine s)
        {
            var raw = s.GetString("gp_scrollbar", DeclaredString("gp_scrollbar"));
            if (string.Equals(raw, "Always", StringComparison.OrdinalIgnoreCase)) return ScrollBarVisibility.Always;
            if (string.Equals(raw, "Never", StringComparison.OrdinalIgnoreCase)) return ScrollBarVisibility.Never;
            return ScrollBarVisibility.Default; // "Auto"
        }

        /// <summary>
        /// ★ AJOUT (01/10) : applique la longueur maximale du TITRE de commit au message qui part
        /// réellement dans <c>git commit</c> (<paramref name="maxLength"/> = 0 : illimité, ce que
        /// le catalogue annonce par « 0 = illimité »). Tronquer seulement à l'écran aurait laissé
        /// dans l'historique un titre plus long que ce que le réglage promet.
        /// La coupe se fait sur une frontière de mot quand il y en a une avant la limite, pour ne
        /// pas produire un mot amputé ; sinon sur la limite exacte. Le message est nettoyé de ses
        /// retours à la ligne parce que <c>GitService.CommitAsync</c> place le message entre
        /// guillemets sur une seule ligne de commande — un titre multi-ligne n'y a pas sa place.
        /// </summary>
        internal static string TruncateCommitTitle(string message, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;
            var single = message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (maxLength <= 0 || single.Length <= maxLength) return single;

            var cut = single.LastIndexOf(' ', Math.Min(maxLength, single.Length - 1));
            return cut > 0 ? single.Substring(0, cut) : single.Substring(0, maxLength);
        }

        /// <summary>
        /// ★ AJOUT (01/10) : action au clic sur un fichier modifié.
        /// Le catalogue propose « Project Diff » (défaut) et « File Diff ».
        ///
        /// POURQUOI LES DEUX SONT CÂBLABLES SANS RIEN INVENTER : le dépôt a bien DEUX vues de
        /// diff réelles, mais elles ne s'opposent PAS comme le libellé le suggère —
        /// <c>Moto.Core.Services.InlineDiffPreviewService</c> (diff d'un fichier, déjà utilisé
        /// ailleurs) et <c>GitService.GetDiffAsync()</c> (le diff RÉEL de tout le dépôt modifié,
        /// fichier par fichier, via git CLI). On ne fabrique donc aucune des deux :
        ///   - « File Diff » ouvre le fichier cliqué dans l'éditeur (le diff du fichier — celui
        ///     que MOTO sait déjà afficher pour un fichier ouvert) ;
        ///   - tout le reste, dont le défaut « Project Diff », déplie le diff RÉEL du projet
        ///     dans le panneau (un bloc par fichier, contenu old/new renvoyé par git) — le seul
        ///     comportement qui corresponde à son libellé, et celui qui marche sans dépendre de
        ///     l'éditeur principal.
        /// </summary>
        internal static bool ClickOpensFileDiff(SettingsEngine s)
            => string.Equals(
                s.GetString("gp_click_behavior", DeclaredString("gp_click_behavior")),
                "File Diff",
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ordre d'affichage des entrées du panneau git (réglage <c>gp_sort</c>).</summary>
    internal enum GitSortMode
    {
        Path,
        Name,
        Status
    }

    // ------------------------------------------------------------------
    // gp_button et gp_count_badge : LAISSÉS INERTES (décision, pas oubli)
    // ------------------------------------------------------------------
    // gp_button — « Bouton git dans la barre de statut ». La barre de statut réelle
    // (Views/StatusBarPanelView.xaml) ne contient aucune puce « git » : y en ajouter une est
    // une fonctionnalité à concevoir (et dont le clic devrait être défini), pas un réglage
    // déclaré à rendre opérant. Même raisonnement que les 3 clés tb_* de compte utilisateur
    // (01/10) : le support n'existe pas, on ne simule pas.
    //
    // gp_count_badge — « Badge des changements non commités ». Le seul nombre non ambigu
    // accessible est Staged.Count (l'index). Or le libellé annonce les changements NON
    // COMMITÉS, c'est-à-dire aussi les modifications et les fichiers non suivis — un total
    // Staged+Unstaged+Untracked compte DEUX FOIS tout fichier indexé puis remodifié (« MM »
    // dans git status --porcelain), ce qui est précisément l'état de ce dépôt à chaque
    // commit. Un badge faux est pire qu'un badge absent : laissé inerte, comme
    // pp_count_badge (01/10) et tabs_git_status (28/09).
}
