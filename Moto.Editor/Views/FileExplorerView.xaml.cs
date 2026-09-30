// Moto.Editor/Views/FileExplorerView.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
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
            UpdateEmptyState();

            // ★ AJOUT (31/08, points 1/3/17) : zone grise arrondie au survol/clic.
            HoverEffects.Attach(BtnOpenFolder);
            HoverEffects.Attach(BtnNewFile);
            HoverEffects.Attach(BtnRefresh);
            HoverEffects.Attach(BtnToggleSide);

            // ★ AJOUT (26/09) : la surveillance du dossier suit la vie de la vue (retirée de l'arbre visuel → plus aucun rappel).
            Loaded += (_, _) => { if (_watcher is null && !string.IsNullOrWhiteSpace(CurrentRoot)) StartWatching(CurrentRoot); };
            Unloaded += (_, _) => StopWatching();
        }

        /// <summary>Charge un dossier racine dans l'explorateur.</summary>
        public void LoadFolder(string rootPath)
        {
            CurrentRoot = rootPath;
            _root = _treeService.CreateRoot(rootPath);
            _treeService.LoadChildren(_root);
            Refresh();
            RefreshProjectInfo(rootPath);
            StartWatching(rootPath);
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (26/09, retour de Tom : « quand j'ai créé Fichierdetest.txt via l'explorateur de fichiers Windows, j'ai dû cliquer sur
        // refresh pour qu'il apparaisse ; dans Zed ou VS Code c'est instantané ») : le dossier ouvert est surveillé. Un fichier ou dossier
        // créé, supprimé ou renommé ailleurs (Explorateur Windows, terminal, git…) apparaît ou disparaît de lui-même, sans replier
        // l'arborescence ni la faire défiler. bin, obj, .git… sont ignorés AVANT tout traitement : une compilation y écrit des milliers de
        // fichiers. Plusieurs changements rapprochés ne donnent qu'une seule relecture (300 ms après le dernier).
        // ------------------------------------------------------------------

        private FileSystemWatcher? _watcher;
        private IDispatcherTimer? _watchDebounce;

        private void StartWatching(string rootPath)
        {
            StopWatching();
            if (!Directory.Exists(rootPath)) return;

            try
            {
                var watcher = new FileSystemWatcher(rootPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName, // pas les écritures : un enregistrement ne change pas l'arbre
                    InternalBufferSize = 64 * 1024,
                };
                FileSystemEventHandler onChange = (_, e) => OnWatchedChange(watcher, e.FullPath);
                watcher.Created += onChange;
                watcher.Deleted += onChange;
                watcher.Renamed += (_, e) => { OnWatchedChange(watcher, e.OldFullPath); OnWatchedChange(watcher, e.FullPath); };
                // Trop de changements d'un coup (git checkout…) : Windows en perd le détail — on relit tout ce qui est déplié.
                watcher.Error += (_, _) => ScheduleTreeReload(watcher);
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // Dossier réseau, lecteur retiré… : l'explorateur marche comme avant, avec « Actualiser ».
                System.Diagnostics.Debug.WriteLine($"Explorer watcher: {ex.Message}");
            }
        }

        private void StopWatching()
        {
            var watcher = _watcher;
            _watcher = null;
            if (watcher is null) return;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        /// <summary>Thread du système de fichiers : filtre, puis relecture différée sur le thread de l'interface.</summary>
        private void OnWatchedChange(FileSystemWatcher source, string fullPath)
        {
            if (!ReferenceEquals(source, _watcher)) return; // ancien dossier : rappel tardif d'une surveillance arrêtée
            if (IsIgnoredPath(source.Path, fullPath)) return;
            ScheduleTreeReload(source);
        }

        private static bool IsIgnoredPath(string root, string fullPath)
        {
            string relative;
            try { relative = Path.GetRelativePath(root, fullPath); }
            catch (ArgumentException) { return true; }
            foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                if (segment.Length > 0 && FileTreeService.IsHiddenName(segment)) return true;
            return false;
        }

        private void ScheduleTreeReload(FileSystemWatcher source)
        {
            Dispatcher.Dispatch(() =>
            {
                if (!ReferenceEquals(source, _watcher)) return;
                if (_watchDebounce is null)
                {
                    _watchDebounce = Dispatcher.CreateTimer();
                    _watchDebounce.Interval = TimeSpan.FromMilliseconds(300);
                    _watchDebounce.IsRepeating = false;
                    _watchDebounce.Tick += (_, _) => { if (_watcher is not null) ReloadTree(); };
                }
                _watchDebounce.Stop();
                _watchDebounce.Start();
            });
        }

        /// <summary>Relit ce qui est déplié (fichiers apparus ou disparus) sans replier l'arborescence.</summary>
        private void ReloadTree()
        {
            if (_root is null) return;
            _treeService.Reload(_root);
            Refresh();
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
            // ★ (25/09) : glyphes dans le XAML (dossier, ⎇) au lieu des emojis 📁/🌿 collés au texte.
            ProjectNameLabel.Text = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var branch = ReadGitBranch(rootPath);
            BranchLabel.Text = branch is null ? "" : $"⎇ {branch}";
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

        /// <summary>
        /// ★ CHANGÉ (26/09) : la liste affichée est mise à jour en place (retraits, insertions) au lieu d'être vidée puis remplie — elle garde
        /// sa position de défilement quand un dossier se déplie ou qu'un fichier apparaît.
        /// </summary>
        private void Refresh()
        {
            var target = _treeService.Flatten(_root);
            foreach (var node in target) node.IsActive = IsActivePath(node);

            var keep = new HashSet<FileNode>(target, ReferenceEqualityComparer.Instance);
            for (var i = _visibleNodes.Count - 1; i >= 0; i--)
                if (!keep.Contains(_visibleNodes[i])) _visibleNodes.RemoveAt(i);

            for (var i = 0; i < target.Count; i++)
            {
                if (i < _visibleNodes.Count && ReferenceEquals(_visibleNodes[i], target[i])) continue;
                var at = _visibleNodes.IndexOf(target[i]);
                if (at >= 0) _visibleNodes.Move(at, i);
                else _visibleNodes.Insert(i, target[i]);
            }

            UpdateEmptyState();
        }

        private string? _activePath;

        /// <summary>
        /// ★ AJOUT (25/09) : surligne la ligne du fichier affiché dans l'éditeur (null = aucun). Appelée par MainPage à chaque
        /// changement de document ; les lignes créées plus tard (dossier déplié) sont marquées dans Refresh.
        /// </summary>
        public void SetActiveFile(string? path)
        {
            _activePath = path;
            foreach (var node in _visibleNodes) node.IsActive = IsActivePath(node);
        }

        private bool IsActivePath(FileNode node)
            => !node.IsDirectory && _activePath is not null && string.Equals(node.Path, _activePath, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// ★ CORRECTION (02/09) : remplace CollectionView.EmptyView (voir commentaire
        /// XAML) — bascule un panneau frère de TreeList au lieu du mécanisme interne
        /// de CollectionView. Appelée après chaque changement de _visibleNodes.
        /// </summary>
        private void UpdateEmptyState()
        {
            EmptyStatePanel.IsVisible = _visibleNodes.Count == 0;
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

                ReloadTree(); // ★ (26/09) sans replier l'arborescence, comme « Actualiser »
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
                // ★ CHANGÉ (26/09) : relit ce qui est déplié au lieu de tout recharger (qui repliait chaque dossier ouvert).
                ReloadTree();
                RefreshProjectInfo(CurrentRoot);
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
