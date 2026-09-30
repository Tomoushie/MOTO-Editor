// Moto.Editor/Models/FileNode.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Models
{
    /// <summary>
    /// Nœud de l'arborescence (dossier ou fichier).
    /// Chargement paresseux : les enfants ne sont lus qu'au premier dépliage.
    /// </summary>
    public class FileNode : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public int Depth { get; set; }
        public bool IsLoaded { get; set; }

        public ObservableCollection<FileNode> Children { get; } = new ObservableCollection<FileNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Icon));
                    OnPropertyChanged(nameof(Chevron));
                    OnPropertyChanged(nameof(Glyph));
                }
            }
        }

        private bool _isActive;

        /// <summary>★ AJOUT (25/09) : fichier affiché dans l'éditeur (ligne surlignée dans l'explorateur, comme VS Code).</summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Indentation en pixels pour la vue aplatie (16 px par défaut, ou pp_indent).</summary>
        public double Indent => _indentOverride >= 0 ? _indentOverride : Depth * 16;

        // ★ AJOUT (01/10) : les visuels de la ligne (chevron, glyphe, couleur, indentation)
        // étaient calculés en PROPRIÉTÉS FIGÉES ici — l'indentation valait toujours 16 px et le
        // glyphe était toujours affiché, si bien que pp_indent / pp_file_icons / pp_folder_icons
        // n'avaient aucune prise. Ils sont désormais posés par FileExplorerView.ApplySettings via
        // cette méthode, appelée à chaque rafraîchissement de l'arborescence.
        // Les propriétés ci-dessus (Icon, Chevron, Glyph, GlyphColor, Indent) sont CONSERVÉES
        // telles quelles : d'autres vues peuvent s'en servir, on ne retire rien.
        // Valeurs de repli nulles assumées : tant qu'ApplyVisuals n'a pas été appelée, les
        // propriétés retombent sur leur ancien calcul (voir les getters) — d'où le `?`.
        private string? _chevronOverride;
        private string? _glyphOverride;
        private Microsoft.Maui.Graphics.Color? _glyphColorOverride;
        private double _indentOverride = -1;

        private double _rowHeight = 24;

        /// <summary>
        /// ★ AJOUT (01/10) : hauteur de la ligne, pilotée par pp_entry_spacing (Standard 24 /
        /// Comfortable 28). Portée par le nœud plutôt que par la vue : le Grid du DataTemplate
        /// est instancié une fois par ligne, un x:Name posé dedans n'est pas atteignable depuis
        /// le code-behind de FileExplorerView.
        /// </summary>
        public double RowHeight
        {
            get => _rowHeight;
            set
            {
                if (Math.Abs(_rowHeight - value) < 0.01) return;
                _rowHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>★ AJOUT (01/10) : fige les visuels de cette ligne d'après les réglages pp_*.</summary>
        public void ApplyVisuals(string chevron, string glyph, Microsoft.Maui.Graphics.Color glyphColor, double indent)        {
            _chevronOverride = chevron;
            _glyphOverride = glyph;
            _glyphColorOverride = glyphColor;
            _indentOverride = indent;

            OnPropertyChanged(nameof(Chevron));
            OnPropertyChanged(nameof(Glyph));
            OnPropertyChanged(nameof(GlyphColor));
            OnPropertyChanged(nameof(Indent));
            OnPropertyChanged(nameof(HasGlyph));
            OnPropertyChanged(nameof(HasChevron));
            OnPropertyChanged(nameof(ChevronColumnWidth));
            OnPropertyChanged(nameof(GlyphColumnWidth));
        }

        /// <summary>Icône affichée selon le type et l'état (ancienne version emoji, gardée pour les vues qui s'en servent encore).</summary>
        public string Icon => IsDirectory
            ? (IsExpanded ? "▼ 📂" : "▶ 📁")
            : "📄";

        // ★ AJOUT (25/09, passe « moyen → élevé ») : chevron + glyphe Segoe Fluent Icons coloré par type (Controls/FileTypeVisual).
        private static readonly Microsoft.Maui.Graphics.Color FolderTint = Microsoft.Maui.Graphics.Color.FromArgb("#C8A45A");

        public string Chevron => _chevronOverride ?? (IsDirectory ? (IsExpanded ? Controls.MotoIcons.ChevronDownSmall : Controls.MotoIcons.ChevronRightSmall) : string.Empty);
        public string Glyph => _glyphOverride ?? (IsDirectory ? (IsExpanded ? Controls.MotoIcons.FolderOpen : Controls.MotoIcons.Folder) : Controls.FileTypeVisual.Glyph(Name));
        public Microsoft.Maui.Graphics.Color GlyphColor => _glyphColorOverride ?? (IsDirectory ? FolderTint : Controls.FileTypeVisual.Tint(Name));

        /// <summary>★ AJOUT (01/10) : vrai si un glyphe doit occuper la colonne d'icône (pp_file_icons / pp_folder_icons).</summary>
        public bool HasGlyph => !string.IsNullOrEmpty(Glyph);

        /// <summary>★ AJOUT (01/10) : vrai si un chevron doit occuper la colonne de pliage (pp_folder_icons).</summary>
        public bool HasChevron => !string.IsNullOrEmpty(Chevron);

        /// <summary>
        /// ★ AJOUT (01/10) : largeur de la colonne chevron. Une colonne « Auto » ne se replie pas
        /// quand son enfant est masqué (son WidthRequest reste mesuré) — il faut donc mettre la
        /// largeur à 0 explicitement, sinon déco cher pp_folder_icons laisserait 16 px de vide.
        /// </summary>
        public GridLength ChevronColumnWidth
            => HasChevron ? new GridLength(16) : new GridLength(0);

        /// <summary>★ AJOUT (01/10) : même raison que ChevronColumnWidth, pour la colonne d'icône (20 px).</summary>
        public GridLength GlyphColumnWidth
            => HasGlyph ? new GridLength(20) : new GridLength(0);

        // ★ AJOUT (01/10) : statut git PAR FICHIER (réglages pp_git_status / pp_git_indicator).
        // Rempli uniquement à partir du VRAI GitService.GetStatusAsync() — jamais deviné. Reste
        // vide tant qu'aucun statut n'a été fourni, auquel cas la colonne n'est pas affichée.
        private string _gitMark = string.Empty;
        private string _gitColor = "#9CA3AF";

        /// <summary>Lettre d'état git : "M" modifié, "A" indexé, "?" non suivi, "" si propre/inconnu.</summary>
        public string GitMark
        {
            get => _gitMark;
            set
            {
                if (_gitMark == value) return;
                _gitMark = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasGitMark));
                OnPropertyChanged(nameof(GitColumnWidth));
            }
        }

        /// <summary>Couleur de la lettre d'état (M orange, A vert, ? gris).</summary>
        public string GitColor
        {
            get => _gitColor;
            set
            {
                if (_gitColor == value) return;
                _gitColor = value ?? "#9CA3AF";
                OnPropertyChanged();
            }
        }

        /// <summary>Vrai si une lettre d'état doit être affichée pour cette ligne.</summary>
        public bool HasGitMark => !string.IsNullOrEmpty(_gitMark);

        /// <summary>Largeur de la colonne d'état git (0 quand rien à afficher, même piège que les autres colonnes).</summary>
        public GridLength GitColumnWidth
            => HasGitMark ? new GridLength(14) : new GridLength(0);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
