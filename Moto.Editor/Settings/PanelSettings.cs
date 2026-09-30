// Moto.Editor/Settings/PanelSettings.cs
using System;
using System.IO;
using System.Linq;
using Moto.Core.Settings;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10) : lit et applique les réglages de la famille « Panneaux / Project Panel »
    /// (clés <c>pp_*</c>) — l'explorateur de fichiers.
    ///
    /// POURQUOI CE FICHIER : ces 13 clés étaient déclarées au catalogue mais lues par AUCUN code
    /// compilé (seule occurrence du dépôt : un commentaire dans MainPage.UI.cs) — la fenêtre
    /// Réglages les affichait, rien ne les appliquait. Même chantier, même méthode que
    /// TabBarSettings (28/09) pour la famille <c>tabs_*</c>.
    ///
    /// ⚠️ PIÈGE (28/09, re-payé ici) : <c>SettingsEngine.GetBool("clé")</c> SANS second argument
    /// renvoie <c>false</c> quand la clé est absente du store — le moteur ne consulte JAMAIS le
    /// catalogue. Sur une installation neuve, appliquer <c>pp_file_icons</c> ou
    /// <c>pp_git_status</c> sans défaut aurait donc MASQUÉ les icônes et le statut git, alors que
    /// le catalogue les déclare à <c>true</c>. Toutes les lectures ci-dessous passent donc
    /// explicitement le défaut DÉCLARÉ.
    ///
    /// PÉRIMÈTRE VOLONTAIRE — un réglage est câblé seulement si la donnée qu'il promet EXISTE :
    ///   - <c>pp_hide_hidden</c> / <c>pp_hide_gitignore</c> : le filtrage réel vit dans
    ///     <see cref="Moto.Editor.Services.FileTreeService"/> (qui lisait jusqu'ici des règles
    ///     figées en dur) — voir le commentaire de <c>ApplyVisibilitySettings</c> ;
    ///   - <c>pp_git_status</c> / <c>pp_git_indicator</c> : branchés sur le VRAI
    ///     <c>GitService.GetStatusAsync()</c> (git CLI), jamais sur une valeur devinée ;
    ///   - <c>pp_count_badge</c> reste volontairement INERTE, voir la note en fin de fichier.
    /// </summary>
    internal static class PanelSettings
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
        // Lectures typées, défaut déclaré appliqué (une seule fois par clé).
        // ------------------------------------------------------------------

        /// <summary>Côté d'ancrage de l'explorateur : "Right" (défaut) ou "Left".</summary>
        internal static bool DockLeft(SettingsEngine s)
            => string.Equals(s.GetString("pp_dock", DeclaredString("pp_dock")), "Left", StringComparison.OrdinalIgnoreCase);

        /// <summary>Largeur du panneau projet, bornée aux bornes DÉCLARÉES au catalogue (120..800).</summary>
        internal static double Width(SettingsEngine s)
        {
            var declared = DeclaredInt("pp_width");
            var value = s.GetInt("pp_width", declared);
            return Math.Clamp(value, 120, 800);
        }

        /// <summary>Indentation en pixels par niveau d'imbrication (bornes déclarées 8..40).</summary>
        internal static double Indent(SettingsEngine s)
        {
            var declared = DeclaredInt("pp_indent");
            var value = s.GetInt("pp_indent", declared);
            return Math.Clamp(value, 8, 40);
        }

        /// <summary>"Comfortable" (défaut) = lignes hautes ; "Standard" = densité VS Code.</summary>
        internal static bool ComfortableSpacing(SettingsEngine s)
            => !string.Equals(s.GetString("pp_entry_spacing", DeclaredString("pp_entry_spacing")), "Standard", StringComparison.OrdinalIgnoreCase);

        internal static bool FileIcons(SettingsEngine s) => s.GetBool("pp_file_icons", DeclaredBool("pp_file_icons"));
        internal static bool FolderIcons(SettingsEngine s) => s.GetBool("pp_folder_icons", DeclaredBool("pp_folder_icons"));
        internal static bool AutoReveal(SettingsEngine s) => s.GetBool("pp_auto_reveal", DeclaredBool("pp_auto_reveal"));
        internal static bool HorizontalScroll(SettingsEngine s) => s.GetBool("pp_horizontal_scroll", DeclaredBool("pp_horizontal_scroll"));
        internal static bool HideHidden(SettingsEngine s) => s.GetBool("pp_hide_hidden", DeclaredBool("pp_hide_hidden"));
        internal static bool HideGitIgnore(SettingsEngine s) => s.GetBool("pp_hide_gitignore", DeclaredBool("pp_hide_gitignore"));

        // ------------------------------------------------------------------
        // Filtrage de l'arborescence (consommé par FileTreeService).
        // ------------------------------------------------------------------

        /// <summary>
        /// Règles de visibilité passées à <see cref="Moto.Editor.Services.FileTreeService"/>.
        /// Les DEUX booléens sont indépendants, comme les deux réglages : <c>pp_hide_hidden</c>
        /// gouverne les entrées commençant par un point (règle qui était jusqu'ici figée à
        /// "toujours masquées" dans le service), <c>pp_hide_gitignore</c> gouverne les entrées
        /// listées par le <c>.gitignore</c> du dossier ouvert.
        /// </summary>
        internal readonly struct VisibilityRules
        {
            internal VisibilityRules(bool hideHidden, bool hideGitIgnore)
            {
                HideHidden = hideHidden;
                HideGitIgnore = hideGitIgnore;
            }

            internal bool HideHidden { get; }
            internal bool HideGitIgnore { get; }
        }
        internal static VisibilityRules Visibility(SettingsEngine s)
            => new VisibilityRules(HideHidden(s), HideGitIgnore(s));

        // ------------------------------------------------------------------
        // Rendu des lignes (consommé par FileExplorerView).
        // ------------------------------------------------------------------

        /// <summary>
        /// ★ AJOUT (01/10) : applique les réglages d'AFFICHAGE d'une ligne de l'arborescence.
        /// Prend la ligne en paramètres (pas de dépendance à la vue) pour rester testable et
        /// pour que la règle « chevron des DOSSIERS seulement » soit écrite à un seul endroit.
        ///
        /// <paramref name="isDirectory"/> : un fichier n'a ni chevron ni glyphe de dossier,
        /// quels que soient les réglages — <c>pp_folder_icons</c> ne concerne QUE les dossiers
        /// (c'est ce que dit son libellé : « Icônes ou chevrons »), il ne peut pas décider de
        /// l'icône des fichiers, qui reste gouvernée par <c>pp_file_icons</c>.
        /// </summary>
        internal static void ApplyRowVisuals(
            SettingsEngine s,
            bool isDirectory,
            bool isExpanded,
            string name,
            int depth,
            out string chevron,
            out string glyph,
            out Microsoft.Maui.Graphics.Color glyphColor,
            out double indent)
        {
            indent = depth * Indent(s);

            // Un seul appel par clé et par ligne : la valeur est réutilisée juste en dessous.
            var folderIcons = s.GetBool("pp_folder_icons", DeclaredBool("pp_folder_icons"));
            var fileIcons = s.GetBool("pp_file_icons", DeclaredBool("pp_file_icons"));

            if (isDirectory)
            {
                // « Icônes de dossiers » décoché = ni chevron NI glyphe : le libellé du réglage
                // annonce « Icônes ou chevrons », les deux relèvent donc de la même clé.
                chevron = folderIcons
                    ? (isExpanded ? Moto.Editor.Controls.MotoIcons.ChevronDownSmall : Moto.Editor.Controls.MotoIcons.ChevronRightSmall)
                    : string.Empty;

                glyph = folderIcons
                    ? (isExpanded ? Moto.Editor.Controls.MotoIcons.FolderOpen : Moto.Editor.Controls.MotoIcons.Folder)
                    : string.Empty;
                glyphColor = FolderTint;
            }
            else
            {
                // Un fichier n'a jamais de chevron, et son icône dépend de pp_file_icons.
                chevron = string.Empty;
                glyph = fileIcons ? Moto.Editor.Controls.FileTypeVisual.Glyph(name) : string.Empty;
                glyphColor = fileIcons ? Moto.Editor.Controls.FileTypeVisual.Tint(name) : Microsoft.Maui.Graphics.Colors.Transparent;
            }
        }

        private static readonly Microsoft.Maui.Graphics.Color FolderTint = Microsoft.Maui.Graphics.Color.FromArgb("#C8A45A");

        // ------------------------------------------------------------------
        // pp_count_badge : LAISSÉ INERTE (décision, pas oubli)
        // ------------------------------------------------------------------
        // Le libellé promet « Badge du nombre de terminaux ». Or MOTO Editor n'a qu'UN SEUL
        // TerminalPanelView (MainPage.xaml, x:Name="TerminalPanel") : il n'existe aucune
        // collection de terminaux, donc aucun « nombre » à afficher. Ce que le ViewModel
        // expose, c'est TerminalLines (le nombre de LIGNES de sortie du terminal unique) —
        // un chiffre réel, mais qui ne répond pas à ce que le réglage annonce. Afficher
        // « 42 » à côté du mot « terminaux » alors qu'il s'agit de lignes de log serait
        // exactement le « réglage qui affiche une valeur fausse » que la règle du dépôt
        // interdit. Laissé inerte, comme tabs_git_status au 28/09.
    }
}
