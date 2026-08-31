// Moto.Editor/MainPage.xaml.cs (v31 — orchestrateur compressé, résolution DI centralisée)
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Builders;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Workspace;
using Moto.Core.Collab;
using Moto.Core.Doc;
using Moto.Core.Export;
using Moto.Core.Remote;
using Moto.Core.Security;
using Moto.Core.Services;
using Moto.Core.Settings;
using Moto.Editor.Controls;
using Moto.Editor.Services;
using Moto.Editor.Settings;
using Moto.Editor.ViewModels;
using Moto.Editor.Views;
#if WINDOWS
using Moto.Editor.Platforms.Windows;
#endif

namespace Moto.Editor
{
    /// <summary>
    /// MainPage v31 : orchestrateur minimal.
    /// Handlers UI → MainPage.UI.cs · Routeurs → MainPage.Routing.cs
    /// Panneaux IA → MainPage.Panels.cs · Extensions → MainPage.Extensions.cs
    /// </summary>
    public partial class MainPage : ContentPage
    {
        // ── Services & moteurs ──
        private readonly MainViewModel _viewModel = new();
        private readonly MotoAiService _aiService = new();
        private readonly AutoProjectBuilder _projectBuilder = new();
        private readonly ProjectImportEngine _import = new();
        private readonly BuildEngine _build = new();
        private readonly RunEngine _run = new();
        private readonly SandboxEngine _sandbox = new();
        private readonly ProjectLockEngine _lock = new();
        private readonly LicenseGeneratorEngine _license = new();
        private readonly ExportEngine _exportEngine = new();
        private readonly PresentationEngine _presentationEngine = new();
        private readonly ChatService _chatService;
        private readonly CollabSession _collabSession = new();
        private readonly Moto.Core.Platform.PlatformEngine _platformEngine = new();
        // ★ AJOUT (30/08, 3e passe) : moteur de prévisualisation (existait déjà dans
        // Moto.Core, complet, jamais instancié nulle part — voir OnPreviewRequested).
        private readonly Moto.Core.Preview.PreviewEngine _previewEngine = new();
        private RemoteClient _remoteClient;

        // ── Panneaux & moteurs IA ──
        private PlatformView _platformPanel;
        private AiMonitoringView _aiMonitorPage;
        private AiMonitoringView _aiMonitorPanel;
        private CortexEngine _cortex;
        private NeuralMode _neural;
        private AIWorkspace _workspace;
        private DocEngine _docEngine;
        private CortexView _cortexPanel;
        private NeuralView _neuralPanel;
        private AIWorkspaceView _workspacePanel;
        private Views.SearchView _searchPanel;
        // _pluginGallery / _analyticsDashboard / _aiSettings : déclarés dans MainPage.Extensions.cs
        // _globalUsage : déclaré dans MainPage.UI.cs
        private DebugPanelView _debugPanel;
        private InfoOverlay? _infoOverlay;

        // ── Home / SettingsMenu : pas de constructeur sans paramètre, donc pas
        // déclarables en XAML — construits ici et ajoutés au visuel à la main. ──
        private Views.HomeView Home;
        private Views.SettingsMenuView SettingsMenu;

        // ── État ──
        private bool _inSandbox;
        private string _realRoot = string.Empty;
        private string _sandboxPath = string.Empty;
        private string _currentRoot = string.Empty;
        private bool _maximized;
        private readonly System.Collections.Generic.Stack<string> _historyBack = new();
        private readonly System.Collections.Generic.Stack<string> _historyForward = new();
        private string _currentPath;

        /// <summary>Résolution DI centralisée (déduplique les GetService verbeux).</summary>
        private T? Resolve<T>() where T : class =>
            Application.Current?.Handler?.MauiContext?.Services?.GetService<T>();

        public MainPage()
        {
            InitializeComponent();
            BindingContext = _viewModel;

            InitializeMainPageExtensions();
            InitializeInfoOverlayAndUpdates();
            InitializeGlobalUsage();

            _chatService = new ChatService(_currentRoot, _aiService.Fallback, _aiService.Kernel);
            _chatService.SelectionProvider = () => EditorPane.GetSelectedText();

            CreateHomeAndSettingsMenu();

            WireEditorPane();
            WireSettings();
            WirePanels();
            WireMenusAndSidebar();
            WireInlayHints();

            // ── Chargement : stats + provider IA + mises à jour ──
            Loaded += OnPageLoaded;
            Loaded += async (s, e) =>
            {
                RefreshHomeStats();
                await CheckAiProviderOnFirstLaunchAsync();
                CheckForUpdatesOnStartup();   // ★ CORRECTION : maintenant câblé
            };
        }

        // ══════════════ Initialisation ══════════════

        private void InitializeInfoOverlayAndUpdates()
        {
            _infoOverlay = Resolve<InfoOverlay>() ?? new InfoOverlay();
            RootGrid.Children.Add(_infoOverlay);
            // ★ CORRECTION (30/08, refonte Zen) : ligne 1 → 2 (nouvelle ligne de nav
            // horizontale insérée), colonnes 4 → 3 (dock IA + centre + arborescence).
            Grid.SetRow(_infoOverlay, 2);
            Grid.SetColumnSpan(_infoOverlay, 3);

            var updateManager = Resolve<Moto.Core.Updates.UpdateManager>();
            if (updateManager != null)
            {
                _infoOverlay.SetUpdateManager(updateManager);
                MenuBar.SetUpdateManager(updateManager);
                updateManager.StartAutoCheck();
            }
            StatusBar.InitializeInfoOverlay(_infoOverlay);
        }

        /// <summary>
        /// Construit Home et SettingsMenu : ni l'une ni l'autre n'ont de constructeur
        /// sans paramètre, donc impossible de les déclarer en XAML. Ajoutées ici à la
        /// cellule Row=1,Col=2 (Home, même emplacement qu'EditorPane) et en overlay
        /// flottant (SettingsMenu) juste après la création de _chatService.
        /// </summary>
        private void CreateHomeAndSettingsMenu()
        {
            Home = new Views.HomeView(_chatService, _cortex, _workspaceState);
            // ★ CORRECTION (30/08, refonte Zen) : colonne centrale 2 → 1 (le dock IA a
            // pris la colonne 0, l'arborescence a pris la 2 — voir MainPage.xaml).
            Grid.SetRow(Home, 2);
            Grid.SetColumn(Home, 1);
            RootGrid.Children.Add(Home);

            SettingsMenu = new Views.SettingsMenuView
            {
                IsVisible = false,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start
            };
            Grid.SetRow(SettingsMenu, 2);
            Grid.SetColumnSpan(SettingsMenu, 3);
            RootGrid.Children.Add(SettingsMenu);
        }

        private void WireInlayHints()
        {
            var inlayHintService = Resolve<Moto.Core.LSP.InlayHints.InlayHintService>();
            if (inlayHintService is null) return;

            EditorPane.InitializeInlayHints(inlayHintService);
            EditorPane.EditorChanged += (s, text) =>
            {
                if (_viewModel.SelectedDocument?.Path != null)
                    EditorPane.NotifyTextChangedForInlayHints(_viewModel.SelectedDocument.Path, text);
            };
        }

        // ══════════════ Câblage ══════════════

        private void WireEditorPane()
        {
            EditorPane.BindTabs(_viewModel.Documents);
            EditorPane.TabSelected += doc => { _viewModel.SelectedDocument = doc; LoadDocumentIntoEditor(doc); };

            // ★ CORRECTION (30/08) : source unique de vérité pour "un document a été
            // sélectionné" — couvre TOUS les chemins (clic sur un onglet déjà visible,
            // ouverture depuis l'explorateur, ouverture automatique d'une réponse IA,
            // etc.), pas seulement le clic sur onglet ci-dessus. Sans ça, un document
            // ouvert par un autre chemin ne charge jamais son contenu dans l'éditeur
            // ("Aucun fichier ouvert" reste affiché) et son onglet n'apparaît jamais
            // visuellement sélectionné (un futur clic dessus ne redéclenche donc rien).
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.SelectedDocument))
                {
                    EditorPane.SelectTab(_viewModel.SelectedDocument);
                    LoadDocumentIntoEditor(_viewModel.SelectedDocument);
                }
            };
            // ★ AJOUT (31/08) : "impossible de revenir au menu principal une fois qu'un
            // fichier est ouvert" (Tom) — aucun moyen de fermer un onglet n'existait.
            EditorPane.TabClosed += doc => _viewModel.RemoveDocument(doc);
            EditorPane.BackRequested += OnNavBack;
            EditorPane.ForwardRequested += OnNavForward;
            EditorPane.MaximizeRequested += OnMaximizeToggled;
            EditorPane.SplitRequested += () => StatusBar.SetStatus("Split : à venir.");
            EditorPane.OpenFileRequested += () => _viewModel.OpenFileCommand.Execute(null);
            EditorPane.AiPromptSubmitted += OnAiBandPrompt;
            EditorPane.ExportRequested += () => ExportMenu.IsVisible = !ExportMenu.IsVisible;
            EditorPane.PreviewRequested += OnPreviewRequested;
            LivePreview.SetPreviewEngine(_previewEngine);
            // Le "Live" de Live Preview : répercute chaque frappe si le panneau est ouvert.
            EditorPane.EditorChanged += (s, text) =>
            {
                if (LivePreview.IsVisible) _ = LivePreview.UpdatePreviewAsync(text);
            };
            EditorPane.EditorChanged += (s, text) =>
            {
                if (_viewModel.SelectedDocument is { } doc)
                {
                    doc.Text = text;
                    if (doc.Path != null) _cortex?.LearnFromCode(doc.Path, text);
                }
            };

            ExplorerPanel.FileOpened += path => _viewModel.OpenFilePath(path);
            AiBar.Submitted += OnAiCommandSubmitted;
        }

        private void WireSettings()
        {
            SettingsMenu.SettingChanged += OnSettingChanged;
            SettingsApplier.ApplyAll(_viewModel, EditorPane.Editor, SettingsEngine.Shared);
            SettingsApplier.Subscribe(_viewModel, EditorPane.Editor, SettingsEngine.Shared);
            ApplyLayoutSettings();

            // ★ AJOUT (31/08) : nouvelle fenêtre de Réglages — thème/police/mini-map
            // sont déjà repris automatiquement par SettingsApplier.Subscribe ci-dessus
            // (tout SettingsEngine.Set(...) déclenche ApplyAll). Seul "terminal_show"
            // n'a aucun abonné ailleurs : câblé ici explicitement.
            SettingsWindow.RealSettingChanged += (key, value) =>
            {
                if (key == "terminal_show" && value is bool visible)
                    _viewModel.IsTerminalVisible = visible;
            };
        }

        private void WirePanels()
        {
            _platformPanel = new PlatformView(_platformEngine);
            _cortexPanel = new CortexView(null);
            _neuralPanel = new NeuralView(null);
            _workspacePanel = new AIWorkspaceView(null);
            _pluginGallery = new PluginGalleryView(null, null, System.IO.Path.Combine(_currentRoot ?? "", "plugins"));
            _analyticsDashboard = new AnalyticsDashboardView();
            _debugPanel = new DebugPanelView();
            // ★ AJOUT (30/08, 2e passe) : onglet "Recherche" — cherche des fichiers
            // par nom dans le projet ouvert (voir SearchView.xaml.cs).
            _searchPanel = new Views.SearchView();
            _searchPanel.FileOpened += path => _viewModel.OpenFilePath(path);

            foreach (var panel in new ContentView[]
            {
                _platformPanel, _cortexPanel, _neuralPanel, _workspacePanel,
                _pluginGallery, _analyticsDashboard, _debugPanel, _searchPanel
            })
                AddFloatingPanel(panel);

            WireAiPanels();
        }

        private void WireMenusAndSidebar()
        {
            MenuBar.MenuCommanded += OnMenuCommanded;
            ActivityBar.ActivitySelected += OnActivitySelected;
            Sidebar.NewChatRequested += () => { };
            Sidebar.ThreadSelected += name => StatusBar.SetStatus($"Ouverture : {name}");
            Sidebar.SessionMoved += (session, section) => StatusBar.SetStatus($"📌 {session} → {section}");
            Sidebar.Refresh(
                pinned: new() { "Snake2000", "Projet logiciel", "Orchestrator Agent", "MOTO Editor" },
                projects: new(),
                recents: new() { "Estimer la valeur du projet" });

            Home.HomePromptSubmitted += text => OnAiCommandSubmitted(text);
            // ★ AJOUT (30/08) : chips "Local"/"Projet logiciel" de l'accueil, jusqu'ici
            // décoratifs (repérés par Tom au premier lancement réel). "Projet logiciel"
            // réutilise le même flux que le bouton Importer existant (OnImportClicked,
            // MainPage.UI.cs). "Local" ouvre un choix façon Claude Code (capture d'écran
            // fournie par Tom : Local/Cloud/Contrôle à distance/WSL/SSH).
            Home.ProjectChipTapped += () => OnImportClicked(this, EventArgs.Empty);
            Home.LocalChipTapped += OnHomeLocalChipRequested;
            Home.SetStats(
                values: new[] { "0", "0", "0", "0" },
                titles: new[] { "Sessions", "Messages", "Tokens", "Patterns appris" });

            _viewModel.Documents.CollectionChanged += (s, e) =>
            {
                bool hasDocs = _viewModel.Documents.Count > 0;
                Home.IsVisible = !hasDocs;
                EditorPane.IsVisible = hasDocs;
            };

            // Panneaux Présentation / Remote / Collab : handlers déjà écrits dans
            // MainPage.UI.cs, jamais branchés faute de MainPage.xaml — câblés ici.
            StatusBar.AiMonitorTapped += () => OnAiMonitorTapped(this, EventArgs.Empty);
            LocationMenu.LocationSelected += OnLocationSelected;
            PresentationPanel.GenerateRequested += OnPresentationGenerate;
            RemotePanel.ConnectRequested += OnRemoteConnect;
            CollabPanel.ChatSubmitted += OnCollabChat;
        }

        /// <summary>
        /// Chip "💻 Local" de l'accueil : choix façon Claude Code entre Local/Cloud/
        /// Contrôle à distance/WSL/SSH (capture d'écran fournie par Tom, 30/08).
        /// ★ CORRECTION (30/08) : DisplayActionSheet (menu natif Windows, pas custom,
        /// mal aligné — repéré par Tom) remplacé par ExecutionLocationMenu (design maison).
        /// Cloud et WSL n'ont pas encore d'implémentation dans ce dépôt.
        /// </summary>
        private void OnHomeLocalChipRequested()
        {
            LocationMenu.IsVisible = true;
        }

        /// <summary>
        /// ★ CORRECTION (30/08) : "local" ne faisait RIEN de visible (le menu se
        /// contentait de se fermer) — Tom a interprété ça comme "impossible de
        /// sélectionner les options". Chaque choix affiche maintenant une confirmation
        /// dans la barre de statut, y compris "Local" (déjà le cas actuel).
        /// </summary>
        private void OnLocationSelected(string id)
        {
            LocationMenu.IsVisible = false;
            switch (id)
            {
                case "remote":
                case "ssh":
                    RemotePanel.IsVisible = true;
                    StatusBar.SetStatus($"🖱️ Contrôle à distance : {id}");
                    break;
                case "cloud":
                case "wsl":
                    StatusBar.SetStatus($"{id} : pas encore disponible.");
                    break;
                case "local":
                    StatusBar.SetStatus("💻 Déjà en local.");
                    break;
            }
        }

        // ══════════════ Cycle de vie ══════════════

        private void OnPageLoaded(object sender, EventArgs e)
        {
#if WINDOWS
            var nativeWindow = Application.Current.Windows[0].Handler.PlatformView
                as Microsoft.UI.Xaml.Window;
            GlobalHotkeyService.Register(nativeWindow, onHotkey: () => AiBar.Toggle(), onWindowActivated: () => AiBar.Show());

            // ★ CORRECTION (30/08) : barre de titre Windows par défaut visible en plus de
            // notre CustomMenuBarView (repéré par Tom au premier lancement réel). Le
            // convertisseur MAUI→WinUI natif manquait ici (SnapLayoutsHelper attend des
            // Microsoft.UI.Xaml.FrameworkElement, MenuBar.BtnMin/BtnMax/BtnClose/
            // TitleBarDragZone sont des Border MAUI) — via leur Handler.PlatformView.
            if (nativeWindow != null
                && MenuBar.BtnMin.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeBtnMin
                && MenuBar.BtnMax.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeBtnMax
                && MenuBar.BtnClose.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeBtnClose
                && MenuBar.TitleBarDragZone.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeDragZone)
            {
                Platforms.Windows.SnapLayoutsHelper.ConfigureSnapLayouts(
                    nativeWindow, nativeBtnMin, nativeBtnMax, nativeBtnClose, nativeDragZone);
                App.Breadcrumb("OnPageLoaded — ConfigureSnapLayouts appelé");
            }
            else
            {
                App.Breadcrumb("OnPageLoaded — ConfigureSnapLayouts IGNORÉ : "
                    + $"nativeWindow={nativeWindow != null}, "
                    + $"BtnMin={MenuBar.BtnMin.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement}, "
                    + $"BtnMax={MenuBar.BtnMax.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement}, "
                    + $"BtnClose={MenuBar.BtnClose.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement}, "
                    + $"DragZone={MenuBar.TitleBarDragZone.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement}");
            }

            // ★ AJOUT (31/08, 3e passe) : point de déclenchement supplémentaire, distinct
            // de Page.Loaded — Window.Activated est un signal natif WinUI (la fenêtre
            // devient la fenêtre active), potentiellement plus fiable que le cycle de vie
            // MAUI si c'est LUI le problème. Ré-appliquer est sans risque (juste redéfinir
            // les mêmes rectangles). Idée reprise d'une piste externe (Qwen), adaptée à
            // notre vraie implémentation (InputNonClientPointerSource, pas de subclass
            // Win32 ici malgré ce que cette piste supposait à tort).
            if (nativeWindow != null)
            {
                nativeWindow.Activated += (s, e) =>
                {
                    App.Breadcrumb($"Window.Activated ({e.WindowActivationState}) — réapplique ConfigureSnapLayouts");
                    if (MenuBar.BtnMin.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement bMin
                        && MenuBar.BtnMax.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement bMax
                        && MenuBar.BtnClose.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement bClose
                        && MenuBar.TitleBarDragZone.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement bDrag)
                    {
                        Platforms.Windows.SnapLayoutsHelper.ConfigureSnapLayouts(nativeWindow, bMin, bMax, bClose, bDrag);
                    }
                };
            }
#endif
        }

        protected override void OnDisappearing()
        {
            _globalUsage?.StopSession();
            _cortex?.Dispose();
            _workspace?.Dispose();
            _docEngine?.Dispose();
            _previewEngine?.Dispose();
            base.OnDisappearing();
        }

        // ══════════════ IA ══════════════

        private async void OnAiBandPrompt(string model, string prompt)
        {
            var doc = _viewModel.SelectedDocument;
            if (doc == null) { EditorPane.SetAiStatus("Ouvre un fichier à modifier."); return; }

            EditorPane.SetAiStatus($"[{model}] Réflexion…");
            try
            {
                var answer = await _chatService.AskWithCodeAsync(model, prompt, doc.Text);
                var code = ExtractCodeBlock(answer);
                if (code != null)
                {
                    EditorPane.EditorText = code;
                    doc.Text = code;
                    EditorPane.SetAiStatus($"[{model}] Code modifié en direct.");
                    _cortex?.LearnFromCode(doc.Path, code);
                }
                else
                {
                    EditorPane.SetAiStatus($"[{model}] " + (answer.Length > 120 ? answer[..120] + "…" : answer));
                }
            }
            catch (Exception ex)
            {
                EditorPane.SetAiStatus("Erreur IA : " + ex.Message);
            }
        }

        /// <summary>
        /// ★ AJOUT (30/08, 3e passe) : bouton 🌐 de EditorPaneView — ouvre LivePreviewView
        /// sur le fichier actuellement sélectionné. "text" est passé comme langage : le
        /// PreviewEngine détecte déjà lui-même html/css/js/java via l'EXTENSION du
        /// fichier (voir PreviewEngine.WrapIfNeeded) — "text" ne correspond à aucun de
        /// ces cas spéciaux, donc les fichiers .cs/.xaml/etc. tombent proprement dans le
        /// repli générique (contenu échappé, affiché dans un &lt;pre&gt;) plutôt que
        /// d'être mal interprétés comme du HTML.
        /// </summary>
        private async void OnPreviewRequested()
        {
            var doc = _viewModel.SelectedDocument;
            if (doc == null)
            {
                StatusBar.SetStatus("Ouvre un fichier à prévisualiser.");
                return;
            }

            await LivePreview.StartPreviewAsync(doc.Path ?? doc.Title, doc.Text ?? string.Empty, "text");
            LivePreview.IsVisible = true;
        }

        private static string? ExtractCodeBlock(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(answer, "```[\\w]*\\r?\\n([\\s\\S]*?)```");
            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        /// <summary>
        /// ★ AJOUT (31/08) : comme ExtractCodeBlock, mais garde aussi l'étiquette de
        /// langage (```csharp, ```python...) pour choisir une extension de fichier
        /// sensée. Utilisé par OnAiCommandSubmitted (MainPage.Routing.cs) — "demander
        /// du code ne fonctionne pas" (Tom) : le fichier s'ouvrait bien, mais avec le
        /// message entier (explications + code mélangés) plutôt que le code seul.
        /// </summary>
        private static (string Code, string Extension)? ExtractCodeBlockWithLanguage(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(answer, "```(\\w*)\\r?\\n([\\s\\S]*?)```");
            if (!match.Success) return null;

            var code = match.Groups[2].Value.TrimEnd();
            if (string.IsNullOrWhiteSpace(code)) return null;

            var extension = match.Groups[1].Value.ToLowerInvariant() switch
            {
                "csharp" or "cs" => ".cs",
                "python" or "py" => ".py",
                "javascript" or "js" => ".js",
                "typescript" or "ts" => ".ts",
                "html" => ".html",
                "css" => ".css",
                "json" => ".json",
                "xml" or "xaml" => ".xml",
                "sql" => ".sql",
                "bash" or "shell" or "sh" or "powershell" or "ps1" => ".sh",
                "java" => ".java",
                "cpp" or "c++" or "c" => ".cpp",
                _ => ".txt"
            };
            return (code, extension);
        }

        private async void OnAiMonitorTapped(object? sender, EventArgs e)
        {
            try
            {
                var monitoringView = Resolve<Views.AiMonitoringView>();
                if (monitoringView != null)
                    // AiMonitoringView est un ContentView, pas une Page : on l'enveloppe.
                    await Navigation.PushAsync(new ContentPage { Title = "Monitoring IA", Content = monitoringView });
                else if (_aiMonitorPage != null)
                {
                    _aiMonitorPage.IsVisible = true;
                    EditorPane.SetAiStatus("Monitoring IA…");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Failed to open AiMonitoringView: {ex.Message}");
            }
        }

        public void UpdateAiStatusIndicator(string state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // ★ CORRECTION (30/08) : AiStatusLabel/AiStatusIcon ont déménagé dans
                // StatusBarPanelView (voir MainPage.xaml).
                StatusBar.SetAiStatus(state);
            });
        }

        // ══════════════ Démarrage : provider IA + mises à jour ══════════════

        private async Task CheckAiProviderOnFirstLaunchAsync()
        {
            if (SettingsEngine.Shared.GetBool("app.firstLaunchCompleted", defaultValue: false)) return;
            try
            {
                if (!await CheckOllamaAvailabilityAsync())
                {
                    // ★ CORRECTION : DisplayDialog n'existe pas dans l'API MAUI (ContentPage
                    // n'expose que DisplayAlert) — probablement une confusion avec une autre techno.
                    bool download = await DisplayAlert("🧠 Moteur IA non détecté",
                        "Ollama n'est pas installé. Souhaitez-vous télécharger un modèle embarqué ?", "Oui", "Non");
                    if (download) await NavigateToModelManagerAsync();
                }
                SettingsEngine.Shared.Set("app.firstLaunchCompleted", true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] First launch check failed: {ex.Message}");
            }
        }

        private static async Task<bool> CheckOllamaAvailabilityAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await http.GetAsync("http://localhost:11434/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private Task NavigateToModelManagerAsync()
        {
            // Gestion du modèle IA embarqué : mise de côté pour cette passe (voir Moto.Core.csproj).
            StatusBar.SetStatus("Le moteur IA embarqué n'est pas encore disponible dans cette version.");
            return Task.CompletedTask;
        }

        private async void CheckForUpdatesOnStartup()
        {
            var updater = Resolve<AutoUpdateService>();
            if (updater is null) return;

            var info = await updater.CheckAsync();
            if (!info.IsAvailable) return;

            bool ok = await DisplayAlert("Mise à jour disponible",
                $"Version {info.Version} disponible. Mettre à jour ?", "Oui", "Plus tard");
            if (ok) await updater.ApplyAsync(info);
        }
    }
}
