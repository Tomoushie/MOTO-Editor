// Moto.Editor/Models/GitChangeNode.cs
using System;
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
    internal sealed class GitChangeNode
    {
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
        }

        /// <summary>
        /// Construit une ligne depuis le chemin RENVOYÉ PAR GIT (jamais deviné). Les chemins de git
        /// utilisent toujours « / », y compris sous Windows.
        /// </summary>
        internal static GitChangeNode For(string path, GitChangeKind kind)
            => new(path, kind);

        /// <summary>
        /// Applique les réglages d'affichage : style de statut (<c>gp_status_style</c>) et
        /// groupement (<c>gp_group</c>). Appelée une fois par ligne et par rafraîchissement,
        /// jamais à chaque rendu de cellule.
        /// </summary>
        internal void ApplyDisplay(bool labeledStatus, bool groupByFolder)
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
        }

        /// <summary>
        /// Renseigne les statistiques de diff (<c>gp_diff_stats</c>), ou les efface quand le
        /// réglage est décoché / que git n'a rien renvoyé pour ce fichier. Une chaîne vide fait
        /// aussi se replier la colonne (voir <see cref="StatsColumnWidth"/>).
        /// </summary>
        internal void SetDiffStats(int additions, int removals)
            => DiffStats = $"+{additions} −{removals}";
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
