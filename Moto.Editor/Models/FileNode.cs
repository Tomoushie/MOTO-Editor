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
                }
            }
        }

        /// <summary>Indentation en pixels pour la vue aplatie.</summary>
        public double Indent => Depth * 16;

        /// <summary>Icône affichée selon le type et l'état.</summary>
        public string Icon => IsDirectory
            ? (IsExpanded ? "▼ 📂" : "▶ 📁")
            : "📄";

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
