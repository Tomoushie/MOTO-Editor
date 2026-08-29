// Moto.Editor/ViewModels/MainViewModel.cs (régénéré v2 avec lazy loading)
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Moto.Core.Performance;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor.ViewModels
{
    /// <summary>
    /// ViewModel principal v2.
    /// Intègre le lazy loading (idée #18) :
    /// - les onglets sont créés SANS contenu ;
    /// - le contenu est chargé à la sélection via LazyFileLoader ;
    /// - les documents inactifs sont évincés (sauvegardés si modifiés).
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly LazyFileLoader _loader = new(maxDocuments: 20);
        private readonly TerminalService _terminal = new();

        private EditorDocument _selectedDocument;
        private bool _isBeginnerMode = true;
        private bool _isTerminalVisible;
        private bool _isDiagnosticsVisible;
        private bool _isMiniMapVisible;
        private bool _isLearnVisible = true;
        private bool _isQuickActionsVisible = true;
        private string _terminalInput = string.Empty;
        private string _status = "MOTO prêt.";

        public ObservableCollection<FileItem> Files { get; } = new();
        public ObservableCollection<EditorDocument> Documents { get; } = new();
        public ObservableCollection<TerminalLine> TerminalLines { get; } = new();
        public ObservableCollection<DiagnosticItem> Diagnostics { get; } = new();
        public ObservableCollection<AiSuggestion> Suggestions { get; } = new();
        public ObservableCollection<AiQuickAction> QuickActions { get; } = new();

        public Command OpenFolderCommand { get; }
        public Command OpenFileCommand { get; }
        public Command SaveCommand { get; }
        public Command ToggleModeCommand { get; }
        public Command ToggleTerminalCommand { get; }
        public Command SendTerminalCommand { get; }
        public Command<AiQuickAction> RunQuickActionCommand { get; }

        public MainViewModel()
        {
            OpenFolderCommand = new Command(async () => await OpenFolderAsync());
            OpenFileCommand = new Command(async () => await OpenFilePickerAsync());
            SaveCommand = new Command(async () => await SaveActiveAsync());
            ToggleModeCommand = new Command(ToggleMode);
            ToggleTerminalCommand = new Command(() => IsTerminalVisible = !IsTerminalVisible);
            SendTerminalCommand = new Command(SendTerminal);
            RunQuickActionCommand = new Command<AiQuickAction>(RunQuickAction);

            _terminal.OutputReceived += OnTerminalOutput;

            // Un document évincé de la mémoire ne casse pas l'onglet :
            // il sera rechargé à la prochaine sélection.
            _loader.DocumentEvicted += path =>
                MainThread.BeginInvokeOnMainThread(() =>
                    Status = $"Mémoire : document déchargé ({Path.GetFileName(path)}).");

            SeedQuickActions();
            ApplyBeginnerMode();
        }

        public EditorDocument SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (SetField(ref _selectedDocument, value))
                {
                    // Lazy loading : charge le contenu à la sélection.
                    _ = LoadSelectedAsync();
                }
            }
        }

        public bool IsBeginnerMode { get => _isBeginnerMode; private set => SetField(ref _isBeginnerMode, value); }
        public bool IsTerminalVisible { get => _isTerminalVisible; set => SetField(ref _isTerminalVisible, value); }
        public bool IsDiagnosticsVisible { get => _isDiagnosticsVisible; set => SetField(ref _isDiagnosticsVisible, value); }
        public bool IsMiniMapVisible { get => _isMiniMapVisible; set => SetField(ref _isMiniMapVisible, value); }
        public bool IsLearnVisible { get => _isLearnVisible; set => SetField(ref _isLearnVisible, value); }
        public bool IsQuickActionsVisible { get => _isQuickActionsVisible; set => SetField(ref _isQuickActionsVisible, value); }
        public string TerminalInput { get => _terminalInput; set => SetField(ref _terminalInput, value); }
        public string Status { get => _status; private set => SetField(ref _status, value); }

        /// <summary>Charge le contenu du document sélectionné (à la demande).</summary>
        private async Task LoadSelectedAsync()
        {
            var doc = SelectedDocument;

            if (doc == null || string.IsNullOrWhiteSpace(doc.Path))
            {
                return;
            }

            try
            {
                var content = await _loader.GetContentAsync(doc.Path);

                if (ReferenceEquals(SelectedDocument, doc) && doc.Text != content)
                {
                    doc.Text = content;
                }

                Status = $"Chargé : {doc.Title} ({_loader.LoadedCount} doc(s) en mémoire).";
            }
            catch (Exception ex)
            {
                Status = $"Erreur de chargement : {ex.Message}";
            }
        }

        private async Task OpenFolderAsync()
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync();

                if (!result.IsSuccessful)
                {
                    return;
                }

                LoadFileNames(result.Folder.Path);
                _terminal.Start(result.Folder.Path);

                Status = $"Workspace ouvert : {result.Folder.Path}";
            }
            catch (Exception ex)
            {
                Status = $"Erreur : {ex.Message}";
            }
        }

        /// <summary>
        /// Liste uniquement les NOMS de fichiers (léger).
        /// Aucun contenu n'est lu : le lazy loading s'applique à l'ouverture.
        /// </summary>
        private void LoadFileNames(string rootPath)
        {
            Files.Clear();

            try
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".cs", ".md", ".json", ".txt", ".js", ".ts", ".py", ".xaml"
                };

                var stack = new System.Collections.Generic.Stack<string>();
                stack.Push(rootPath);

                while (stack.Count > 0 && Files.Count < 500)
                {
                    var current = stack.Pop();

                    string[] sub;
                    string[] files;

                    try
                    {
                        sub = Directory.GetDirectories(current);
                        files = Directory.GetFiles(current);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var dir in sub)
                    {
                        var name = Path.GetFileName(dir);

                        if (!name.StartsWith(".") && name != "bin" && name != "obj" && name != "node_modules")
                        {
                            stack.Push(dir);
                        }
                    }

                    foreach (var file in files)
                    {
                        if (allowed.Contains(Path.GetExtension(file)))
                        {
                            Files.Add(new FileItem { Name = Path.GetFileName(file), Path = file });
                        }
                    }
                }
            }
            catch
            {
                // Un scan partiel reste utilisable.
            }
        }

        private async Task OpenFilePickerAsync()
        {
            var result = await FilePicker.Default.PickAsync();

            if (result != null)
            {
                OpenFilePath(result.FullPath);
            }
        }

        /// <summary>Crée l'onglet SANS lire le fichier (lazy).</summary>
        public void OpenFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            var existing = Documents.FirstOrDefault(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                SelectedDocument = existing;
                return;
            }

            var doc = new EditorDocument
            {
                Path = path,
                Title = Path.GetFileName(path),
                Text = string.Empty // Contenu chargé à la sélection.
            };

            // Chaque frappe met à jour le cache mémoire, pas le disque.
            doc.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EditorDocument.Text))
                {
                    _loader.UpdateContent(doc.Path, doc.Text);
                }
            };

            Documents.Add(doc);
            SelectedDocument = doc;
        }

        public void OpenFile(FileItem file)
        {
            if (file != null)
            {
                OpenFilePath(file.Path);
            }
        }

        /// <summary>Sauvegarde via le loader (écrit seulement si modifié).</summary>
        private async Task SaveActiveAsync()
        {
            var doc = SelectedDocument;

            if (doc == null || string.IsNullOrWhiteSpace(doc.Path))
            {
                Status = "Aucun fichier à sauvegarder.";
                return;
            }

            try
            {
                _loader.UpdateContent(doc.Path, doc.Text);
                await _loader.SaveAsync(doc.Path);
                Status = $"Sauvegardé : {doc.Title}";
            }
            catch (Exception ex)
            {
                Status = $"Erreur de sauvegarde : {ex.Message}";
            }
        }

        private void ToggleMode()
        {
            IsBeginnerMode = !IsBeginnerMode;
            ApplyBeginnerMode();
        }

        private void ApplyBeginnerMode()
        {
            if (IsBeginnerMode)
            {
                IsTerminalVisible = false;
                IsDiagnosticsVisible = false;
                IsMiniMapVisible = false;
                IsLearnVisible = true;
                IsQuickActionsVisible = true;
                Status = "Vue Débutant.";
            }
            else
            {
                IsTerminalVisible = true;
                IsDiagnosticsVisible = true;
                IsMiniMapVisible = true;
                IsLearnVisible = false;
                Status = "Vue Expert.";
            }
        }

        private void SendTerminal()
        {
            if (string.IsNullOrWhiteSpace(TerminalInput))
            {
                return;
            }

            TerminalLines.Add(new TerminalLine { Text = $"> {TerminalInput}" });
            _terminal.SendInput(TerminalInput);
            TerminalInput = string.Empty;
        }

        private void OnTerminalOutput(string line, bool isError)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                TerminalLines.Add(new TerminalLine { Text = line, IsError = isError }));
        }

        private void RunQuickAction(AiQuickAction action)
        {
            try
            {
                action?.Action?.Invoke();
            }
            catch (Exception ex)
            {
                Status = $"Erreur action : {ex.Message}";
            }
        }

        private void SeedQuickActions()
        {
            QuickActions.Add(new AiQuickAction
            {
                Id = "add-method",
                Title = "Ajouter une méthode",
                Description = "Insère une méthode vide.",
                Action = () => InsertSnippet("\nprivate void NewMethod()\n{\n    // TODO\n}\n")
            });

            QuickActions.Add(new AiQuickAction
            {
                Id = "add-class",
                Title = "Ajouter une classe",
                Description = "Insère une classe vide.",
                Action = () => InsertSnippet("\npublic class NewClass\n{\n    // TODO\n}\n")
            });
        }

        private void InsertSnippet(string snippet)
        {
            if (SelectedDocument == null)
            {
                Status = "Ouvre d'abord un fichier.";
                return;
            }

            SelectedDocument.Text += snippet;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
