// Moto.Editor/Views/FileExplorerView.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Explorateur de fichiers avec arborescence dépliable.
    /// Émet FileOpened quand l'utilisateur sélectionne un fichier.
    /// Émet SideToggleRequested pour changer de côté dans MainPage.
    /// </summary>
    public partial class FileExplorerView : ContentView
    {
        private readonly FileTreeService _treeService = new FileTreeService();
        private readonly ObservableCollection<FileNode> _visibleNodes = new ObservableCollection<FileNode>();
        private FileNode _root;

        /// <summary>Chemin racine actuellement affiché.</summary>
        public string CurrentRoot { get; private set; } = string.Empty;

        /// <summary>Déclenché quand un fichier est sélectionné.</summary>
        public event Action<string> FileOpened;

        /// <summary>Déclenché quand l'utilisateur veut changer le côté.</summary>
        public event Action SideToggleRequested;

        public FileExplorerView()
        {
            InitializeComponent();
            TreeList.ItemsSource = _visibleNodes;
        }

        /// <summary>Charge un dossier racine dans l'explorateur.</summary>
        public void LoadFolder(string rootPath)
        {
            CurrentRoot = rootPath;
            _root = _treeService.CreateRoot(rootPath);
            _treeService.LoadChildren(_root);
            Refresh();
        }

        private void Refresh()
        {
            _visibleNodes.Clear();

            foreach (var node in _treeService.Flatten(_root))
            {
                _visibleNodes.Add(node);
            }
        }

        private async void OnOpenFolderClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync();

                if (result.IsSuccessful)
                {
                    LoadFolder(result.Folder.Path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Explorer error: {ex.Message}");
            }
        }

        private void OnRefreshClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(CurrentRoot))
            {
                LoadFolder(CurrentRoot);
            }
        }

        private void OnToggleSideClicked(object sender, EventArgs e)
        {
            SideToggleRequested?.Invoke();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0)
            {
                return;
            }

            if (e.CurrentSelection[0] is FileNode node)
            {
                if (node.IsDirectory)
                {
                    // Déplie / replie le dossier.
                    if (!node.IsLoaded)
                    {
                        _treeService.LoadChildren(node);
                    }

                    node.IsExpanded = !node.IsExpanded;
                    Refresh();
                }
                else
                {
                    // Ouvre le fichier dans l'éditeur.
                    FileOpened?.Invoke(node.Path);
                }
            }

            // Permet de re-sélectionner le même nœud ensuite.
            TreeList.SelectedItem = null;
        }
    }
}
