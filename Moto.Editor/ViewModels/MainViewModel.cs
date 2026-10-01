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
// ★ AJOUT (28/09) : réglages tabs_max / tabs_activate_on_close et état visuel d'onglet.
using Moto.Core.Settings;
using Moto.Editor.Models;
using Moto.Editor.Services;
using Moto.Editor.Settings;

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
                    // ★ AJOUT (28/09) : ordre d'activation des onglets, le plus récent en tête.
                    // Sert au réglage tabs_activate_on_close = « History » (revenir à l'onglet
                    // précédemment actif), qui ne peut pas se déduire de l'ordre d'ouverture.
                    if (value != null)
                    {
                        _activationOrder.Remove(value);
                        _activationOrder.Insert(0, value);
                    }

                    // Lazy loading : charge le contenu à la sélection.
                    _ = LoadSelectedAsync();
                }
            }
        }

        /// <summary>★ AJOUT (28/09) : onglets par ordre d'activation décroissant (réglage tabs_activate_on_close).</summary>
        private readonly List<EditorDocument> _activationOrder = new();

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
                    // ★ CORRECTIF (03/09, bug réel trouvé par Tom via le panneau
                    // Documentation, mais touche TOUT fichier fraîchement ouvert) :
                    // LoadDocumentIntoEditor (MainPage.UI.cs) lit doc.Text UNE SEULE
                    // FOIS, de façon synchrone, au moment où SelectedDocument change
                    // (MainPage.xaml.cs:226-233). Comme ce chargement est asynchrone,
                    // cette lecture arrivait TOUJOURS avant que le contenu réel soit
                    // prêt (doc.Text valait encore "") — rien ne redéclenchait
                    // LoadDocumentIntoEditor une fois le contenu réellement chargé.
                    // Re-lève PropertyChanged(SelectedDocument) pour réutiliser TEL
                    // QUEL l'abonnement existant qui sait déjà recharger l'éditeur.
                    // Forcé sur le thread UI (même précaution déjà prise par
                    // DocumentEvicted juste au-dessus dans ce fichier) : rien ne
                    // garantit que la continuation d'une méthode async revient sur le
                    // thread UI. ★ 2e cause trouvée, plus profonde (voir CodeEditorView
                    // .PushContentAsync/_pushGate) : LoadDocumentIntoEditor est en fait
                    // appelée PLUSIEURS FOIS pour un seul fichier (texte vide pendant le
                    // chargement, puis le vrai texte) — sans sérialisation, l'appel
                    // "vide" pouvait s'appliquer APRÈS le vrai contenu et l'écraser.
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        doc.Text = content;
                        // Re-lève PropertyChanged(SelectedDocument) (même référence,
                        // valeur différente : SetField ne l'aurait pas fait) pour
                        // réutiliser TEL QUEL l'abonnement existant qui sait déjà
                        // recharger l'éditeur — pas un 2e mécanisme à maintenir.
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDocument)));
                    });
                }

                Status = $"Chargé : {doc.Title} ({_loader.LoadedCount} doc(s) en mémoire).";
            }
            catch (Exception ex)
            {
                Moto.Editor.App.Breadcrumb($"LoadSelectedAsync EXCEPTION path={doc.Path} : {ex}");
                Status = $"Erreur de chargement : {ex.Message}";
            }
        }

        private async Task OpenFolderAsync()
        {
            try
            {
                // Microsoft.Maui.Storage n'expose pas de FolderPicker cross-plateforme ;
                // on utilise le picker natif Windows directement (voir méthode ci-dessous).
                var path = await PickFolderPathAsync();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                LoadFileNames(path);
                _terminal.Start(path);

                Status = $"Workspace ouvert : {path}";
            }
            catch (Exception ex)
            {
                Status = $"Erreur : {ex.Message}";
            }
        }

        /// <summary>Ouvre le sélecteur de dossier natif (Windows). Retourne null si annulé/indisponible.</summary>
        private static async Task<string?> PickFolderPathAsync()
        {
#if WINDOWS
            var picker = new global::Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.ComputerFolder,
            };
            picker.FileTypeFilter.Add("*");

            var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
            if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
#else
            await Task.CompletedTask;
            return null;
#endif
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
        /// <summary>
        /// ★ AJOUT (31/08) : ferme un document ouvert. Aucun moyen de fermer un onglet
        /// n'existait auparavant — une fois un fichier ouvert, impossible de revenir à
        /// l'Accueil (Home ne se réaffiche que quand Documents est vide). Repéré par
        /// Tom : "impossible de revenir au menu principal une fois qu'un fichier est
        /// ouvert".
        /// </summary>
        public void RemoveDocument(EditorDocument doc)
        {
            if (doc is null) return;
            var wasSelected = ReferenceEquals(SelectedDocument, doc);
            var closedIndex = Documents.IndexOf(doc);
            _activationOrder.Remove(doc);
            Documents.Remove(doc);

            if (wasSelected)
                SelectedDocument = PickDocumentAfterClose(closedIndex);
        }

        /// <summary>
        /// ★ AJOUT (28/09) : choisit l'onglet activé après une fermeture — réglage
        /// <c>tabs_activate_on_close</c> ("History", "Neighbour", "Left Neighbour").
        /// Avant cet ajout, le code choisissait TOUJOURS le dernier onglet de la liste
        /// (ordre d'ouverture), quel que soit le réglage affiché.
        /// </summary>
        private EditorDocument PickDocumentAfterClose(int closedIndex)
        {
            if (Documents.Count == 0) return null;

            switch (SettingsEngine.Shared.GetString("tabs_activate_on_close", TabBarSettings.DeclaredString("tabs_activate_on_close")))
            {
                // Voisin de droite (celui qui glisse à la place de l'onglet fermé) ; à défaut, celui de gauche.
                case "Neighbour":
                    return Documents[Math.Min(closedIndex, Documents.Count - 1)];

                // Voisin de gauche ; à défaut (onglet fermé en tête), celui de droite.
                case "Left Neighbour":
                    return Documents[Math.Max(0, closedIndex - 1)];

                // "History" (défaut) : l'onglet actif précédent, s'il est encore ouvert.
                default:
                    return _activationOrder.FirstOrDefault(d => Documents.Contains(d))
                           ?? Documents[Math.Min(closedIndex, Documents.Count - 1)];
            }
        }

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

            // ★ AJOUT (28/09) : réglage tabs_max — 0 = illimité. La limite est appliquée ICI
            // (logique d'ouverture), comme demandé : refuser un onglet au-delà de la limite,
            // en le disant dans la barre de statut, plutôt qu'un onglet de plus en silence.
            // ⚠️ Second argument = défaut DÉCLARÉ au catalogue : GetInt(clé) sans défaut renvoie 0
            // pour une clé absente du store (le moteur ne connaît pas le catalogue). Voir TabBarSettings.
            var maxDocuments = SettingsEngine.Shared.GetInt("tabs_max", TabBarSettings.DeclaredInt("tabs_max"));
            if (maxDocuments > 0 && Documents.Count >= maxDocuments)
            {
                Status = $"Limite de {maxDocuments} onglet(s) atteinte (Réglages ▸ Fenêtre & Layout ▸ Onglets maximum). Fermez un onglet pour en ouvrir un autre.";
                return;
            }

            var doc = new EditorDocument
            {
                Path = path,
                Title = Path.GetFileName(path),
                Text = string.Empty // Contenu chargé à la sélection.
            };

            // ★ AJOUT (28/09) : l'onglet créé reçoit tout de suite l'état visuel courant
            // (réglages tabs_*), sinon un onglet ouvert APRÈS un changement de réglage
            // garderait les valeurs par défaut jusqu'au prochain ApplyLayoutSettings.
            TabBarSettings.Apply(doc, SettingsEngine.Shared);

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

        /// <summary>
        /// ★ AJOUT (24/09, agent v2) : des fichiers ont changé sur le disque hors de l'éditeur (l'agent les a écrits, ou
        /// l'utilisateur a annulé un run). Vide leur cache mémoire — MÊME pour un onglet fermé, sinon rouvrir le fichier
        /// montrerait l'ancien texte et son enregistrement (ou son éviction du cache) écraserait le travail de l'agent —
        /// puis remet à jour depuis le disque l'onglet qui l'affiche. Retourne les onglets dont le texte a changé : l'appelant
        /// recharge l'éditeur si l'un d'eux est celui qui est affiché. À appeler sur le thread UI.
        /// </summary>
        public IReadOnlyList<EditorDocument> ReloadFromDisk(IEnumerable<string> fullPaths)
        {
            var changed = new List<EditorDocument>();

            foreach (var raw in fullPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var path = SafeFullPath(raw);
                _loader.Invalidate(path);

                var doc = Documents.FirstOrDefault(d =>
                    !string.IsNullOrWhiteSpace(d.Path)
                    && string.Equals(SafeFullPath(d.Path), path, StringComparison.OrdinalIgnoreCase));
                if (doc is null) continue;

                if (!File.Exists(path))
                {
                    // Le fichier n'existe plus (annulation d'un run qui l'avait créé) : l'onglet n'a plus rien à montrer.
                    RemoveDocument(doc);
                    continue;
                }

                string text;
                try { text = File.ReadAllText(path); }
                catch (IOException) { continue; }                 // verrouillé : l'onglet garde son texte
                catch (UnauthorizedAccessException) { continue; }

                if (doc.Text == text) continue;
                doc.Text = text;
                changed.Add(doc);
            }

            return changed;
        }

        private static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch (Exception) { return path; }
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

        /// <summary>
        /// ★ AJOUT (01/09, revue croisée — dock du bas Terminal) : démarre le
        /// shell s'il ne tourne pas déjà. IsTerminalVisible peut devenir vrai par
        /// plusieurs chemins qui ne démarrent jamais _terminal (mode Expert,
        /// menu Affichage > Terminal...) sans qu'un dossier ait été ouvert au
        /// préalable — sans cet appel, le panneau avait l'air actif (en-tête,
        /// zone de saisie) mais taper une commande n'avait silencieusement
        /// aucun effet (SendInput ignore tout en silence si IsRunning est faux).
        /// Appelé par TerminalPanelView dès que le dock devient visible.
        /// TerminalService.Start(null) démarre dans le dossier utilisateur par
        /// défaut (voir TerminalService.cs) — un shell général reste utile même
        /// sans projet ouvert.
        /// </summary>
        public void EnsureTerminalRunning()
        {
            if (!_terminal.IsRunning)
                _terminal.Start();
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
            // ★ AJOUT (01/10, famille terminal_*) : le caractère BEL (\a) que certains
            // programmes émettent est invisible dans un affichage ligne par ligne —
            // on le retire de la ligne affichée et, si terminal_audible_bell est
            // activé (défaut déclaré : Off), on joue le son. Console.Beep : API du
            // paquet System.Console, disponible dans un projet MAUI Windows, contrairement
            // à System.Media.SystemSounds (paquet Windows Desktop, absent ici).
            var hasBell = line.IndexOf('\a') >= 0;
            if (hasBell)
                line = line.Replace("\a", string.Empty);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (hasBell && TerminalSettings.AudibleBell(SettingsEngine.Shared))
                    Console.Beep();

                TerminalLines.Add(new TerminalLine { Text = line, IsError = isError });

                // terminal_max_scroll_lines (0 = illimité, convention du catalogue) :
                // on retire une ligne à la fois tant qu'on dépasse la limite —
                // ObservableCollection lève CollectionChanged par retrait, la
                // CollectionView se réarrange donc AU PLUS une fois par ligne reçue
                // (un retrait en bloc n'est pas exposé par ce type).
                var limit = TerminalSettings.MaxScrollLines(SettingsEngine.Shared);
                if (limit > 0)
                {
                    while (TerminalLines.Count > limit)
                        TerminalLines.RemoveAt(0);
                }
            });
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
