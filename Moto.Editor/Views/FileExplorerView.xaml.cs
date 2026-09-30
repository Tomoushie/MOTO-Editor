// Moto.Editor/Views/FileExplorerView.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Moto.Editor.Controls;
using Moto.Editor.Models;
using Moto.Editor.Services;
using Moto.Editor.Settings;
using Moto.Core.Settings;

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

        // ★ AJOUT (01/10) : réglages pp_* déjà résolus (icônes, indentation, densité).
        // Lus UNE fois par ApplySettings plutôt qu'à chaque ligne : l'arborescence peut contenir
        // des centaines de nœuds, on ne veut pas relire le moteur de réglages par nœud.
        private SettingsEngine _settings = SettingsEngine.Shared;

        /// <summary>★ AJOUT (01/10) : hauteur d'une ligne, pilotée par pp_entry_spacing.</summary>
        private double _rowHeight = 24;

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
        }

        /// <summary>Charge un dossier racine dans l'explorateur.</summary>
        public void LoadFolder(string rootPath)
        {
            CurrentRoot = rootPath;

            // ★ AJOUT (01/10) : les règles de visibilité (pp_hide_hidden / pp_hide_gitignore)
            // doivent être posées AVANT la lecture des enfants — le service les applique au
            // moment où il énumère le disque, donc les changer après n'aurait aucun effet tant
            // qu'on n'a pas relu l'arborescence (d'où l'appel à LoadFolder depuis ApplySettings).
            _treeService.ApplyVisibilitySettings(PanelSettings.Visibility(_settings));
            _treeService.LoadGitIgnore(rootPath);

            _root = _treeService.CreateRoot(rootPath);
            _treeService.LoadChildren(_root);
            Refresh();
            RefreshProjectInfo(rootPath);
        }

        /// <summary>
        /// ★ AJOUT (01/10) : applique les réglages de la famille « Panneaux / Project Panel »
        /// (clés <c>pp_*</c>). Appelée par MainPage.ApplyLayoutSettings — donc au démarrage, à
        /// chaque changement de réglage et au retour de plein écran, exactement comme
        /// EditorPane.ApplySettings pour les onglets.
        ///
        /// La largeur (pp_width) et le côté (pp_dock) ne sont PAS traités ici : ils concernent la
        /// colonne de la grille racine, que cette vue ne possède pas — c'est MainPage qui les
        /// applique (ApplySidePanelLayout / la poignée de redimensionnement).
        /// </summary>
        public void ApplySettings(SettingsEngine settings)
        {
            if (settings is null) return;
            _settings = settings;

            // Densité des lignes (pp_entry_spacing) : 24 px façon VS Code en « Standard »,
            // 28 px en « Comfortable » (défaut déclaré). Portée par le nœud (RowHeight) et non
            // par cette vue : le Grid du DataTemplate est instancié une fois PAR ligne, un
            // x:Name dedans n'est donc pas atteignable depuis ce code-behind.
            _rowHeight = PanelSettings.ComfortableSpacing(settings) ? 28 : 24;

            // Défilement horizontal (pp_horizontal_scroll) : décoché = le nom long est tronqué
            // et aucun défilement latéral n'est possible (même sens que dans VS Code).
            TreeList.HorizontalScrollBarVisibility = PanelSettings.HorizontalScroll(settings)
                ? ScrollBarVisibility.Default
                : ScrollBarVisibility.Never;

            // Les règles de visibilité et les visuels par ligne changent : on relit l'arborescence
            // si un dossier est déjà ouvert (sinon il n'y a rien à rafraîchir).
            if (!string.IsNullOrWhiteSpace(CurrentRoot))
            {
                LoadFolder(CurrentRoot);
            }
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

        /// <summary>
        /// ★ AJOUT (01/10) : la lecture de branche était privée à cette vue ; la barre de
        /// titre a maintenant besoin de la MÊME donnée (réglage <c>tb_branch_name</c>).
        /// Exposée en interne plutôt que dupliquée — une 2e lecture de <c>.git/HEAD</c>
        /// écrite ailleurs pourrait diverger (nom de branche affiché différemment entre
        /// l'explorateur et la barre de titre). Aucun changement de comportement ici.
        /// </summary>
        internal static string? ReadGitBranchShared(string rootPath) => ReadGitBranch(rootPath);

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
                node.IsActive = IsActivePath(node);

                // ★ AJOUT (01/10) : chevron / glyphe / couleur / indentation de la ligne, calculés
                // à partir des réglages pp_file_icons, pp_folder_icons et pp_indent (voir
                // PanelSettings.ApplyRowVisuals). Sans cet appel, ces trois réglages n'avaient
                // aucune prise : FileNode renvoyait des visuels figés.
                PanelSettings.ApplyRowVisuals(
                    _settings,
                    node.IsDirectory,
                    node.IsExpanded,
                    node.Name,
                    node.Depth,
                    out var chevron,
                    out var glyph,
                    out var glyphColor,
                    out var indent);

                node.ApplyVisuals(chevron, glyph, glyphColor, indent);
                node.RowHeight = _rowHeight;

                _visibleNodes.Add(node);
            }

            UpdateEmptyState();
        }

        private string? _activePath;

        /// <summary>
        /// ★ AJOUT (25/09) : surligne la ligne du fichier affiché dans l'éditeur (null = aucun). Appelée par MainPage à chaque
        /// changement de document ; les lignes créées plus tard (dossier déplié) sont marquées dans Refresh.
        ///
        /// ★ AJOUT (01/10) : applique aussi <c>pp_auto_reveal</c> (« Révèle le fichier actif dans
        /// l'explorateur »). Jusqu'ici cette méthode ne pouvait surligner qu'une ligne DÉJÀ
        /// visible : ouvrir un fichier dont le dossier parent était replié n'affichait rien, et
        /// le réglage n'existait donc pas. « Révéler » = déplier la chaîne de dossiers parents,
        /// en s'appuyant sur le chargement paresseux déjà en place (LoadChildren). Aucune donnée
        /// inventée : on ne fait qu'ouvrir des dossiers réels qui contiennent le fichier.
        /// </summary>
        public void SetActiveFile(string? path)
        {
            _activePath = path;

            var revealed = false;
            if (PanelSettings.AutoReveal(_settings) && !string.IsNullOrWhiteSpace(path) && _root is not null)
            {
                revealed = RevealInTree(path);
            }

            // Un dossier déplié change la liste aplatie : il faut relire _visibleNodes AVANT de
            // poser IsActive, sinon la ligne révélée n'existerait pas encore dans la collection.
            if (revealed) Refresh();

            foreach (var node in _visibleNodes) node.IsActive = IsActivePath(node);
        }

        /// <summary>
        /// ★ AJOUT (01/10) : déplie les dossiers parents de <paramref name="path"/> pour que sa
        /// ligne devienne visible. Chemin purement textuel (le fichier est sous CurrentRoot), donc
        /// aucun accès disque supplémentaire en dehors du chargement paresseux habituel.
        /// Retourne vrai si l'arborescence a changé (donc s'il faut relire _visibleNodes).
        /// </summary>
        private bool RevealInTree(string path)
        {
            if (string.IsNullOrWhiteSpace(CurrentRoot)) return false;

            var root = CurrentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return false; // fichier hors du dossier ouvert : rien à révéler, surtout ne pas inventer.

            var relative = path.Substring(root.Length + 1);
            var segments = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1) return false; // fichier directement à la racine : déjà visible.

            var changed = false;
            var current = _root;

            // On parcourt les dossiers parents (tous les segments sauf le nom du fichier).
            for (var i = 0; i < segments.Length - 1 && current is not null; i++)
            {
                if (!current.IsLoaded) _treeService.LoadChildren(current);

                var next = current.Children.FirstOrDefault(c =>
                    c.IsDirectory && string.Equals(c.Name, segments[i], StringComparison.OrdinalIgnoreCase));

                if (next is null) return changed; // le dossier n'existe pas dans l'arbre : on s'arrête là.

                if (!next.IsExpanded)
                {
                    next.IsExpanded = true;
                    changed = true;
                }

                current = next;
            }

            return changed;
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
