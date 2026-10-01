// Moto.Editor/Views/SearchView.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
using Moto.Editor.Services;
using Moto.Editor.Settings;

namespace Moto.Editor.Views
{
    /// <summary>Un résultat de recherche affiché dans SearchView.</summary>
    public sealed class SearchResultItem
    {
        public string Name { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        // ★ AJOUT (01/10, décision C item 5) : icône du type de fichier (FileTypeVisual),
        // affichée seulement si le réglage file_finder_icons est actif.
        public string Glyph => Controls.FileTypeVisual.Glyph(Name);
        public Microsoft.Maui.Graphics.Color GlyphColor => Controls.FileTypeVisual.Tint(Name);
        public bool ShowIcons { get; init; } = true;
    }

    /// <summary>
    /// Recherche de fichiers par nom dans le projet actuellement ouvert.
    /// Ancrée dans le dock IA (voir SearchView.xaml pour le contexte).
    /// </summary>
    public partial class SearchView : ContentView
    {
        private readonly FileTreeService _treeService = new();
        private readonly ObservableCollection<SearchResultItem> _results = new();
        private string _root = string.Empty;

        /// <summary>Déclenché quand un résultat est sélectionné (chemin complet).</summary>
        public event Action<string>? FileOpened;

        public SearchView()
        {
            InitializeComponent();
            ResultsList.ItemsSource = _results;
        }

        /// <summary>Définit le dossier de projet actuellement ouvert (voir MainPage.Panels.cs, LoadWorkspace).</summary>
        public void SetRoot(string root)
        {
            _root = root ?? string.Empty;
            _results.Clear();
            QueryEntry.Text = string.Empty;
            StatusLabel.Text = string.IsNullOrWhiteSpace(_root)
                ? "Aucun dossier ouvert."
                : $"Prêt à chercher dans « {Path.GetFileName(_root.TrimEnd('\\', '/'))} ».";
            ApplyVisibilityRules();
        }

        /// <summary>
        /// ★ AJOUT (01/10) : applique les règles de visibilité de la recherche —
        /// le réglage <c>search_include_ignored</c> (famille search_*, le seul câblé)
        /// et les exclusions techniques du service. Cette vue possède SA propre
        /// instance de <see cref="FileTreeService"/> (champ ci-dessus) : sans cet
        /// appel, elle ne recevait jamais ni les règles ni le .gitignore, et
        /// affichait donc TOUJOURS les fichiers gitignorés — quel que soit le
        /// réglage, qui affichait pourtant « OFF » à l'époque (faux état corrigé
        /// le 01/10 : défaut passé à true au catalogue, voir SettingsCatalog.cs).
        ///
        /// <c>hideHidden: true</c> en dur = comportement historique de cette vue
        /// (le service démarre avec <c>_hideHidden = true</c>, FileTreeService.cs:37).
        /// Les réglages <c>pp_hide_*</c> pilotent l'explorateur, pas la recherche.
        /// </summary>
        private void ApplyVisibilityRules()
        {
            var hideGitIgnore = !SearchSettings.IncludeIgnored(SettingsEngine.Shared);
            _treeService.ApplyVisibilitySettings(
                new PanelSettings.VisibilityRules(hideHidden: true, hideGitIgnore: hideGitIgnore));

            if (!string.IsNullOrWhiteSpace(_root))
            {
                _treeService.LoadGitIgnore(_root);
            }
        }

        /// <summary>
        /// ★ AJOUT (01/10) : ré-applique les règles et rejoue la requête en cours —
        /// appelé par le dispatch <c>search_</c> (MainPage.RealSettingChanged) quand
        /// l'utilisateur bascule un réglage de la famille pendant que le panneau
        /// affiche des résultats. Sans ce refresh, le réglage ne serait visible qu'à
        /// la prochaine frappe = « inerte » au sens du dépôt.
        /// </summary>
        public void RefreshVisibility()
        {
            ApplyVisibilityRules();
            OnQueryChanged(this, new TextChangedEventArgs(
                QueryEntry.Text, QueryEntry.Text));
        }

        private void OnQueryChanged(object? sender, TextChangedEventArgs e)
        {
            _results.Clear();

            var query = e.NewTextValue?.Trim();
            if (string.IsNullOrWhiteSpace(_root))
            {
                StatusLabel.Text = "Aucun dossier ouvert.";
                return;
            }
            if (string.IsNullOrWhiteSpace(query))
            {
                StatusLabel.Text = "Tape un nom de fichier (ou une partie).";
                return;
            }

            var matches = _treeService.SearchFiles(_root, query);
            var showIcons = Settings.FileFinderSettings.ShowIcons(SettingsEngine.Shared);
            foreach (var path in matches)
            {
                _results.Add(new SearchResultItem
                {
                    Name = Path.GetFileName(path),
                    RelativePath = Path.GetRelativePath(_root, path),
                    FullPath = path,
                    ShowIcons = showIcons
                });
            }

            StatusLabel.Text = matches.Count == 0
                ? "Aucun fichier trouvé."
                : $"{matches.Count} résultat(s).";
        }

        private void OnResultSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is SearchResultItem item)
            {
                FileOpened?.Invoke(item.FullPath);
            }
            ResultsList.SelectedItem = null;
        }
    }
}
