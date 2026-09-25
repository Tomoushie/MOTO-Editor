// Moto.Editor/Models/FileNode.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

        /// <summary>Indentation en pixels pour la vue aplatie.</summary>
        public double Indent => Depth * 16;

        /// <summary>Icône affichée selon le type et l'état (ancienne version emoji, gardée pour les vues qui s'en servent encore).</summary>
        public string Icon => IsDirectory
            ? (IsExpanded ? "▼ 📂" : "▶ 📁")
            : "📄";

        // ★ AJOUT (25/09, passe « moyen → élevé ») : chevron + glyphe Segoe Fluent Icons coloré par type (Controls/FileTypeVisual).
        private static readonly Microsoft.Maui.Graphics.Color FolderTint = Microsoft.Maui.Graphics.Color.FromArgb("#C8A45A");

        public string Chevron => IsDirectory ? (IsExpanded ? Controls.MotoIcons.ChevronDownSmall : Controls.MotoIcons.ChevronRightSmall) : string.Empty;
        public string Glyph => IsDirectory ? (IsExpanded ? Controls.MotoIcons.FolderOpen : Controls.MotoIcons.Folder) : Controls.FileTypeVisual.Glyph(Name);
        public Microsoft.Maui.Graphics.Color GlyphColor => IsDirectory ? FolderTint : Controls.FileTypeVisual.Tint(Name);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
