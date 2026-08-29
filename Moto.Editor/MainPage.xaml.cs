// Moto.Editor/MainPage.xaml.cs (v31 — orchestrateur compressé, résolution DI centralisée)
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Workspace;
using Moto.Core.Collab;
using Moto.Core.Doc;
using Moto.Core.Services;
using Moto.Core.Settings;
using Moto.Editor.Services;
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
        private RemoteClient _remoteClient;

        // ── Panneaux & moteurs IA ──
        private PlatformView _platformPanel;
        private AiMonitorPage _aiMonitorPage;
        private AiMonitorView _aiMonitorPanel;
        private CortexEngine _cortex;
        private NeuralMode _neural;
        private AIWorkspace _workspace;
        private DocEngine _docEngine;
        private CortexView _cortexPanel;
        private NeuralView _neuralPanel;
        private AIWorkspaceView _workspacePanel;
        private PluginGalleryView _pluginGallery;
        private AnalyticsDashboardView _analyticsDashboard;
        private DebugPanelView _debugPanel;
        private Moto.Core.Settings.AiSettingsService _aiSettings;
        private Moto.Core.Monitoring.GlobalUsageService _globalUsage;

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
            var infoOverlay = Resolve<InfoOverlay>();
            if (infoOverlay is null) return;

            var updateManager = Resolve<Moto.Core.Updates.UpdateManager>();
            if (updateManager != null)
            {
                infoOverlay.SetUpdateManager(updateManager);
                MenuBar.SetUpdateManager(updateManager);
                updateManager.StartAutoCheck();
            }
            StatusBar.InitializeInfoOverlay(infoOverlay);
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
            EditorPane.BackRequested += OnNavBack;
            EditorPane.ForwardRequested += OnNavForward;
            EditorPane.MaximizeRequested += OnMaximizeToggled;
            EditorPane.SplitRequested += () => StatusBar.SetStatus("Split : à venir.");
            EditorPane.OpenFileRequested += () => _viewModel.OpenFileCommand.Execute(null);
            EditorPane.AiPromptSubmitted += OnAiBandPrompt;
            EditorPane.ExportRequested += () => ExportMenu.IsVisible = !ExportMenu.IsVisible;
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

            foreach (var panel in new ContentView[]
            {
                _platformPanel, _cortexPanel, _neuralPanel, _workspacePanel,
                _pluginGallery, _analyticsDashboard, _debugPanel
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
            Home.SetStats(
                values: new[] { "0", "0", "0", "0" },
                titles: new[] { "Sessions", "Messages", "Tokens", "Patterns appris" });

            _viewModel.Documents.CollectionChanged += (s, e) =>
            {
                bool hasDocs = _viewModel.Documents.Count > 0;
                Home.IsVisible = !hasDocs;
                EditorPane.IsVisible = hasDocs;
            };
        }

        // ══════════════ Cycle de vie ══════════════

        private void OnPageLoaded(object sender, EventArgs e)
        {
#if WINDOWS
            var nativeWindow = Application.Current.Windows[0].Handler.PlatformView
                as Microsoft.UI.Xaml.Window;
            GlobalHotkeyService.Register(nativeWindow, onHotkey: () => AiBar.Toggle(), onWindowActivated: () => AiBar.Show());

            if (this.Window is Window window)
                SnapLayoutsHelper.ConfigureSnapLayouts(window, MenuBar.BtnMin, MenuBar.BtnMax, MenuBar.BtnClose, TitleBarDragZone);
#endif
        }

        protected override void OnDisappearing()
        {
            _globalUsage?.StopSession();
            _cortex?.Dispose();
            _workspace?.Dispose();
            _docEngine?.Dispose();
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

        private static string? ExtractCodeBlock(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(answer, "```[\\w]*\\r?\\n([\\s\\S]*?)```");
            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        private async void OnAiMonitorTapped(object? sender, EventArgs e)
        {
            try
            {
                var monitoringView = Resolve<Views.AiMonitoringView>();
                if (monitoringView != null)
                    await Navigation.PushAsync(monitoringView);
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
                AiStatusLabel.Text = state;
                AiStatusIcon.Text = state switch
                {
                    "Idle" => "🧠", "Inferring" => "⚡", "Throttled" => "🐢", "Error" => "❌", _ => "🧠"
                };
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
                    bool download = await DisplayDialog("🧠 Moteur IA non détecté",
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

        private async Task NavigateToModelManagerAsync()
        {
            try
            {
                var view = Resolve<Views.ModelManagerView>();
                if (view != null) await Navigation.PushAsync(view);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Failed to open ModelManagerView: {ex.Message}");
            }
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
