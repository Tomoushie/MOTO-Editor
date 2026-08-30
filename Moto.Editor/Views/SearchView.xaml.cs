// Moto.Editor/Views/SearchView.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Maui.Controls;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>Un résultat de recherche affiché dans SearchView.</summary>
    public sealed class SearchResultItem
    {
        public string Name { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
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
            foreach (var path in matches)
            {
                _results.Add(new SearchResultItem
                {
                    Name = Path.GetFileName(path),
                    RelativePath = Path.GetRelativePath(_root, path),
                    FullPath = path
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
