// Moto.Editor/Models/GitChangeNode.cs
using System;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Models
{
    /// <summary>
    /// ★ AJOUT (01/10, chantier <c>gp_*</c>) : une ligne du panneau Git — les trois sections
    /// (indexé / modifié / non suivi) affichaient jusqu'ici des <c>string</c> bruts, donc AUCUN
    /// réglage d'affichage ne pouvait les atteindre : ni le tri, ni le style de statut, ni les
    /// statistiques de diff, ni le clic. Même nécessité déjà rencontrée pour l'explorateur
    /// (<c>FileNode</c>) : un <c>DataTemplate</c> est instancié une fois PAR ligne et son
    /// <c>x:Name</c> est inatteignable depuis le code-behind, donc toute valeur par ligne doit
    /// transiter par une propriété du modèle, jamais par un champ de la vue.
    ///
    /// ⚠️ <c>GridLength</c> vit dans <c>Microsoft.Maui.Controls</c> (piège déjà payé le 01/10 sur
    /// les colonnes de l'explorateur) : une colonne <c>Auto</c> ne se replie PAS quand son enfant
    /// passe à <c>IsVisible=false</c>, elle continue de mesurer son <c>WidthRequest</c>. La
    /// colonne de statistiques de diff est donc repliée en exposant une largeur de 0 depuis ce
    /// modèle, pas en masquant le libellé.
    /// </summary>
    /// <remarks>
    /// ★ MODIFIÉ (01/10, chantier <c>git_*</c>) : la classe implémente maintenant
    /// <see cref="INotifyPropertyChanged"/>. Sans elle, <c>ApplyDisplay</c> et
    /// <c>SetDiffStats</c> mutaient des propriétés liées sans que le <c>DataTemplate</c>
    /// réagisse — seuls les changements déclenchés par un remplacement de collection
    /// (<c>Replace</c> : Clear + Add) ou un déplacement (<c>Move</c> du tri) se voyaient
    /// à l'écran, et un réglage d'affichage changé dans la fenêtre Réglages ne s'appliquait
    /// qu'au prochain rafraîchissement git. Même modèle que <c>FileNode</c> (explorateur),
    /// qui expose déjà <c>GitColumnWidth</c> de cette façon.
    /// </remarks>
    internal sealed class GitChangeNode : INotifyPropertyChanged
    {
        /// <summary>★ AJOUT (01/10) : requis pour que les propriétés liées se rafraîchissent après un changement de réglage.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>★ AJOUT (01/10) : lève <see cref="PropertyChanged"/> pour la propriété indiquée.</summary>
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Clé de comparaison envoyée à <c>git</c> — le chemin relatif, toujours en « / » :
        /// c'est exactement ce que git a renvoyé et ce qu'attendent <c>git add</c> /
        /// <c>git restore --staged</c>. Jamais réécrit, jamais normalisé : une commande git est
        /// lancée avec cette chaîne telle quelle.
        /// </summary>
        internal string Path { get; }

        /// <summary>Section d'origine — sert au tri par statut et à l'enregistrement du clic.</summary>
        internal GitChangeKind Kind { get; }

        /// <summary>Nom seul du fichier (dernier segment du chemin).</summary>
        internal string Name { get; }

        /// <summary>Dossier parent, chaîne vide si le fichier est à la racine.</summary>
        internal string Folder { get; }

        /// <summary>
        /// ★ AJOUT (01/10, <c>git_path_style</c>) : texte réellement AFFICHÉ pour le fichier,
        /// piloté par le réglage « Style de chemin » — « File Name First » (défaut déclaré) =
        /// <c>Nom (dossier)</c>, « Path First » = le chemin complet en « / » renvoyé par git
        /// (l'affichage d'avant ce chantier). <see cref="Path"/> reste, lui, intact : c'est la
        /// clé envoyée à git pour stage/unstage, jamais réécrite.
        /// </summary>
        internal string DisplayPath { get; private set; }


        /// <summary>Lettre d'état façon git : A = indexé, M = modifié, ? = non suivi.</summary>
        internal string StatusLetter { get; }

        /// <summary>Lettre seule, ou libellé complet (« indexé », « modifié », « non suivi »).</summary>
        internal string Status { get; private set; }

        /// <summary>Dossier parent en tête de ligne, ou chaîne vide (<c>gp_group</c> = Status).</summary>
        internal string FolderBadge { get; private set; }

        /// <summary>Texte réellement affiché en tête de ligne : statut, dossier, ou les deux.</summary>
        internal string Lead { get; private set; }

        /// <summary>Teinte du statut, dans les jetons du thème déjà utilisés par l'explorateur.</summary>
        internal Color StatusColor { get; }

        /// <summary>« +12 −3 », ou chaîne vide tant que les statistiques ne sont pas chargées.</summary>
        internal string DiffStats { get; private set; } = string.Empty;

        /// <summary>
        /// Largeur de la colonne de statistiques : 0 tant qu'il n'y a rien à afficher, pour que la
        /// colonne se replie réellement (voir la note de classe).
        /// </summary>
        internal GridLength StatsColumnWidth =>
            string.IsNullOrEmpty(DiffStats) ? new GridLength(0) : GridLength.Auto;

        /// <summary>
        /// ★ AJOUT (01/10, <c>git_stage_restore_buttons</c>) : largeur de la colonne des boutons
        /// stage/restore. Décoché = colonne à 0, même mécanisme (et même piège documenté) que
        /// <see cref="StatsColumnWidth"/> et <c>FileNode.GitColumnWidth</c> de l'explorateur :
        /// une colonne <c>Auto</c> ne se replie PAS d'elle-même quand son enfant est masqué, la
        /// largeur se joue donc sur la Colonne elle-même (binding <c>ColumnDefinition Width</c>).
        /// </summary>
        internal GridLength StageButtonsColumnWidth =>
            _showStageButtons ? GridLength.Auto : new GridLength(0);

        /// <summary>★ AJOUT (01/10) : dernière valeur appliquée pour <c>git_stage_restore_buttons</c>.</summary>
        private bool _showStageButtons = true;


        private GitChangeNode(string path, GitChangeKind kind)
        {
            Path = path;
            Kind = kind;
            var lastSeparator = path.LastIndexOf('/');
            Name = lastSeparator >= 0 ? path.Substring(lastSeparator + 1) : path;
            Folder = lastSeparator > 0 ? path.Substring(0, lastSeparator) : string.Empty;
            StatusLetter = kind switch
            {
                GitChangeKind.Staged => "A",
                GitChangeKind.Unstaged => "M",
                _ => "?"
            };
            StatusColor = kind switch
            {
                GitChangeKind.Staged => Color.FromArgb("#4EC9B0"),
                GitChangeKind.Unstaged => Color.FromArgb("#D7A65B"),
                _ => Color.FromArgb("#8A8F98")
            };
            // Valeurs par défaut : statut seul, sans dossier — l'affichage actuel du panneau.
            Status = StatusLetter;
            Lead = StatusLetter;
            FolderBadge = string.Empty;
            // ★ AJOUT (01/10) : chemin affiché par défaut = chemin git complet (comportement
            // d'avant git_path_style) ; ApplyDisplay le recalcule dès le premier rafraîchissement.
            DisplayPath = path;
        }

        /// <summary>
        /// Construit une ligne depuis le chemin RENVOYÉ PAR GIT (jamais deviné). Les chemins de git
        /// utilisent toujours « / », y compris sous Windows.
        /// </summary>
        internal static GitChangeNode For(string path, GitChangeKind kind)
            => new(path, kind);

        /// <summary>
        /// Applique les réglages d'affichage : style de statut (<c>gp_status_style</c>),
        /// groupement (<c>gp_group</c>), style de chemin (<c>git_path_style</c>) et visibilité
        /// des boutons stage/restore (<c>git_stage_restore_buttons</c>). Appelée une fois par
        /// ligne et par rafraîchissement, jamais à chaque rendu de cellule — et, depuis
        /// l'ajout d'<see cref="INotifyPropertyChanged"/>, ses effets se voient IMMÉDIATEMENT
        /// sans remplacer la collection.
        /// </summary>
        internal void ApplyDisplay(bool labeledStatus, bool groupByFolder, bool pathNameFirst, bool showStageButtons)
        {
            Status = labeledStatus
                ? Kind switch
                {
                    GitChangeKind.Staged => "indexé",
                    GitChangeKind.Unstaged => "modifié",
                    _ => "non suivi"
                }
                : StatusLetter;

            FolderBadge = groupByFolder && Folder.Length > 0 ? Folder + "/" : string.Empty;
            Lead = FolderBadge.Length == 0 ? Status : FolderBadge + "  " + Status;

            // ★ AJOUT (01/10, git_path_style) : « Path First » = chemin complet (l'affichage
            // historique) ; « File Name First » (défaut déclaré) = nom d'abord, dossier entre
            // parenthèses — un dossier vide ne produit jamais « Nom () ».
            DisplayPath = pathNameFirst || Folder.Length == 0
                ? Path
                : $"{Name} ({Folder})";

            _showStageButtons = showStageButtons;
            Raise(nameof(Status));
            Raise(nameof(Lead));
            Raise(nameof(FolderBadge));
            Raise(nameof(DisplayPath));
            Raise(nameof(StageButtonsColumnWidth));
        }

        /// <summary>
        /// Renseigne les statistiques de diff (<c>gp_diff_stats</c>), ou les efface quand le
        /// réglage est décoché / que git n'a rien renvoyé pour ce fichier. Une chaîne vide fait
        /// aussi se replier la colonne (voir <see cref="StatsColumnWidth"/>).
        /// </summary>
        internal void SetDiffStats(int additions, int removals)
        {
            DiffStats = $"+{additions} −{removals}";
            Raise(nameof(DiffStats));
            Raise(nameof(StatsColumnWidth));
        }
    }

    /// <summary>Section d'appartenance d'une ligne du panneau Git (l'ordre suit le cheminement
    /// réel d'un fichier : indexé → modifié → non suivi).</summary>
    internal enum GitChangeKind
    {
        Staged,
        Unstaged,
        Untracked
    }
}
