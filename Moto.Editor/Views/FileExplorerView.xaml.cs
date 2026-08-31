// Moto.Editor/Views/FileExplorerView.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Moto.Editor.Controls;
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

            // ★ AJOUT (31/08, points 1/3/17) : zone grise arrondie au survol/clic.
            HoverEffects.Attach(BtnOpenFolder);
            HoverEffects.Attach(BtnNewFile);
            HoverEffects.Attach(BtnRefresh);
            HoverEffects.Attach(BtnToggleSide);
        }

        /// <summary>Charge un dossier racine dans l'explorateur.</summary>
        public void LoadFolder(string rootPath)
        {
            CurrentRoot = rootPath;
            _root = _treeService.CreateRoot(rootPath);
            _treeService.LoadChildren(_root);
            Refresh();
            RefreshProjectInfo(rootPath);
        }

        /// <summary>
        /// ★ AJOUT (31/08, point 8) : nom du dossier ouvert + branche Git courante,
        /// affichés en haut de l'explorateur (Tom : "que 'main'/'master' apparaisse
        /// [...] dans l'explorateur à droite"). Lecture directe de .git/HEAD — pas
        /// besoin d'appeler l'exécutable git, juste un fichier texte au format
        /// "ref: refs/heads/<branche>" (ou un hash brut en HEAD détachée).
        /// </summary>
        private void RefreshProjectInfo(string rootPath)
        {
            ProjectNameLabel.Text = "📁 " + Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var branch = ReadGitBranch(rootPath);
            BranchLabel.Text = branch is null ? "" : $"🌿 {branch}";
            ((Border)BranchLabel.Parent).IsVisible = branch is not null; // masque juste la puce si pas un dépôt Git
            ProjectInfoBar.IsVisible = true;
        }

        private static string? ReadGitBranch(string rootPath)
        {
            try
            {
                var headPath = Path.Combine(rootPath, ".git", "HEAD");
                if (!File.Exists(headPath)) return null;

                var content = File.ReadAllText(headPath).Trim();
                const string refPrefix = "ref: refs/heads/";
                if (content.StartsWith(refPrefix, StringComparison.Ordinal))
                    return content.Substring(refPrefix.Length);

                // HEAD détachée : le fichier contient directement un hash de commit.
                return content.Length >= 7 ? content.Substring(0, 7) + " (détaché)" : content;
            }
            catch
            {
                return null; // dossier sans accès/.git corrompu : pas grave, on masque juste la branche.
            }
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

        /// <summary>
        /// ★ AJOUT (31/08) : "Nouveau fichier" — confirmé jamais construit (pas un bug).
        /// Demande un nom, crée un fichier vide à la racine ouverte, l'ouvre et
        /// rafraîchit l'arborescence.
        /// </summary>
        private async void OnNewFileClicked(object sender, EventArgs e)
        {
            var page = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
            if (page is null) return;

            if (string.IsNullOrWhiteSpace(CurrentRoot))
            {
                await page.DisplayAlert("Nouveau fichier", "Ouvre d'abord un dossier.", "OK");
                return;
            }

            var name = await page.DisplayPromptAsync(
                "Nouveau fichier", "Nom du fichier (avec extension) :", initialValue: "nouveau.txt");
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var path = Path.Combine(CurrentRoot, name);
                if (!File.Exists(path))
                    File.WriteAllText(path, string.Empty);

                LoadFolder(CurrentRoot);
                FileOpened?.Invoke(path);
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Nouveau fichier", $"Impossible de créer le fichier : {ex.Message}", "OK");
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
