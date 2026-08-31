// Moto.Editor/MainPage.Extensions.cs (v29 — Analytics + WindowManager + fenêtres spécialisées)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.AI;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Commands;
using Moto.Core.AI.Suggestions;
using Moto.Core.I18n;
using Moto.Core.Plugins;
using Moto.Core.Plugins.Marketplace;
using Moto.Core.Settings;
using Moto.Editor.Models;
using Moto.Editor.Settings;
using Moto.Editor.Views;

namespace Moto.Editor
{
    /// <summary>
    /// Partial class qui ajoute :
    /// - Command Palette (Ctrl+Shift+P) + tracking analytics
    /// - Confirmation IA (overlay modal)
    /// - Suggestions proactives (panneau flottant)
    /// - Galerie de plugins (DI réelle)
    /// - Overlay de migration
    /// - Persistance drag & drop (WorkspaceStateService)
    /// - AI Settings avec confirmation
    /// - Actions contextuelles
    /// - Analytics Dashboard
    /// - WindowManager (multi-fenêtres spécialisées)
    /// Aucune méthode existante de MainPage.xaml.cs n'est supprimée.
    /// </summary>
    public partial class MainPage
    {
        // ── Overlays & panneaux v27 ──
        private CommandPaletteView? _commandPalette;
        private ConfirmationOverlay? _confirmationOverlay;
        private ProactivePanel? _proactivePanel;

        // ── Overlays & panneaux v25-v26 (conservés) ──
        private MigrationOverlay? _migrationOverlay;
        private PluginGalleryView? _pluginGallery;
        private ProactiveActionsView? _proactiveActions; // legacy v26

        // ── Services v27 ──
        private AiConfirmationService? _confirmationService;
        private ProactiveSuggestionsEngine? _proactiveEngine;
        private readonly ContextualActionsEngine _actionsEngine = new();

        // ── Services v25-v26 (conservés) ──
        private WorkspaceStateService? _workspaceState;
        private PluginRegistry? _pluginRegistry;
        private MarketplaceClient? _marketplaceClient;
        private AiSettingsService? _aiSettings;

        // ── ★ v28 : Analytics ──
        private Moto.Core.AI.Analytics.ProactiveAnalyticsEngine? _analytics;

        // ── ★ v29 : Analytics Dashboard + WindowManager ──
        private AnalyticsDashboardView? _analyticsDashboard;
        private Moto.Editor.Windows.WindowManager? _windowManager;

        // ── Timers ──
        private System.Timers.Timer? _proactiveTimer;

        /// <summary>
        /// Initialise toutes les extensions.
        /// À appeler dans MainPage.xaml.cs juste après InitializeComponent().
        /// </summary>
        public void InitializeMainPageExtensions()
        {
            SetupOverlays();
            ResolveExtensionServices();
            SetupProactiveTimer();
            AttachExtensionsEventHandlers();
            AttachWindowsHotkey();
            TryShowMigrationOverlay();
        }

        // ------------------------------------------------------------------
        // Setup des overlays (avant résolution DI pour que RootGrid existe)
        // ------------------------------------------------------------------
        private void SetupOverlays()
        {
            _migrationOverlay = new MigrationOverlay();
            AddMotoOverlay(_migrationOverlay);
        }

        private void AddMotoOverlay(ContentView overlay)
        {
            RootGrid.Children.Add(overlay);
            // ★ CORRECTION (30/08, refonte Zen) : ligne 1 → 2, colonnes 4 → 3
            // (nouvelle ligne de nav horizontale + dock IA/centre/arborescence).
            Grid.SetRow(overlay, 2);
            Grid.SetColumnSpan(overlay, 3);
        }

        /// <summary>
        /// Affiche une vue (À propos, etc.) en overlay par-dessus le contenu principal.
        /// Référencée par MainPage.AboutCommand.cs / MainPage.AboutShortcuts.cs comme
        /// "méthode d'overlay existante" — elle n'existait nulle part, ajoutée ici.
        /// </summary>
        private void ShowInOverlay(ContentView view)
        {
            if (!RootGrid.Children.Contains(view))
                AddMotoOverlay(view);
            view.IsVisible = true;
        }

        // ------------------------------------------------------------------
        // Résolution des services via DI
        // ------------------------------------------------------------------
        private void ResolveExtensionServices()
        {
            try
            {
                var services = Handler?.MauiContext?.Services
                    ?? Application.Current?.Handler?.MauiContext?.Services;

                if (services is null)
                {
                    System.Diagnostics.Debug.WriteLine("[Extensions] Services DI non disponibles.");
                    return;
                }

                // ── Services v25-v26 ──
                _workspaceState = services.GetService<WorkspaceStateService>();
                _pluginRegistry = services.GetService<PluginRegistry>();
                _marketplaceClient = services.GetService<MarketplaceClient>();

                // ★ CORRECTION : cette méthode construisait ICI une première
                // PluginGalleryView (DI-résolue ou neuve) et l'ajoutait en overlay
                // plein-écran via AddMotoOverlay — mais WirePanels() (MainPage.xaml.cs,
                // appelée juste après dans le constructeur) écrase TOUJOURS le champ
                // _pluginGallery avec une toute nouvelle instance, enveloppée et
                // ancrée dans PanelHost via AddFloatingPanel. La première instance ne
                // devenait donc jamais visible ni pilotable (aucun code ne la
                // référence plus une fois écrasée) : un objet fantôme, doublon mort.
                // _pluginRegistry/_marketplaceClient restent résolus ci-dessus (utiles
                // ailleurs, ex. SettingsMenuView) ; seule la construction en double de
                // la galerie est retirée.

                // AI Settings
                _aiSettings = new AiSettingsService(SettingsEngine.Shared, GetWorkspaceRoot());

                // ── Services v27 ──
                _commandPalette = services.GetService<CommandPaletteView>();
                _confirmationOverlay = services.GetService<ConfirmationOverlay>();
                _proactivePanel = services.GetService<ProactivePanel>();
                _confirmationService = services.GetService<AiConfirmationService>();
                _proactiveEngine = services.GetService<ProactiveSuggestionsEngine>();

                // ── ★ v28 : Analytics ──
                _analytics = services.GetService<Moto.Core.AI.Analytics.ProactiveAnalyticsEngine>();

                // ── ★ v29 : Analytics Dashboard + WindowManager ──
                _analyticsDashboard = services.GetService<AnalyticsDashboardView>();
                _windowManager = services.GetService<Moto.Editor.Windows.WindowManager>();

                // Ajoute les overlays au RootGrid
                if (_commandPalette != null)
                {
                    AddMotoOverlay(_commandPalette);
                    _commandPalette.CommandInvoked += OnPaletteCommandInvoked;
                }

                if (_confirmationOverlay != null)
                {
                    AddMotoOverlay(_confirmationOverlay);
                }

                if (_proactivePanel != null)
                {
                    RootGrid.Children.Add(_proactivePanel);
                    Grid.SetRow(_proactivePanel, 2);
                    Grid.SetColumnSpan(_proactivePanel, 3);
                    _proactivePanel.SuggestionInvoked += command => OnAiCommandSubmitted(command);
                }

                if (_analyticsDashboard != null)
                {
                    AddMotoOverlay(_analyticsDashboard);
                }

                // Branche le handler de confirmation UI
                if (_confirmationService != null && _confirmationOverlay != null)
                {
                    _confirmationService.ConfirmationHandler = async request =>
                        await _confirmationOverlay.ShowAsync(request);
                }

                // Legacy proactive view v26 (conservé pour compatibilité)
                _proactiveActions = new ProactiveActionsView
                {
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.End
                };
                AddMotoOverlay(_proactiveActions);
                _proactiveActions.ActionSelected += command => OnAiCommandSubmitted(command);

                // ── ★ i18n + TAB variants ──
                _liveLanguageSwitcher = services.GetService<LiveLanguageSwitcher>();
                _translationAdvisor = services.GetService<DocumentTranslationAdvisor>();
                _tabVariantsEngine = services.GetService<TabVariantsEngine>();

                // Initialiser TAB variants dans EditorPane
                if (_tabVariantsEngine != null)
                {
                    EditorPane.InitializeTabVariants(_tabVariantsEngine);
                }

                // ★ Déclenchement au clavier (Tab → variantes) mis de côté pour cette passe :
                // CodeEditorView est un WebView (éditeur HTML/JS maison) sans évènement
                // KeyPressed exposé côté MAUI, et Keyboard.Tab n'existe pas dans l'API MAUI
                // (Keyboard y désigne le type de clavier virtuel, pas une touche physique).
                // TriggerTabVariantsAsync() reste appelable manuellement (ex: bouton futur).
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Extensions] Erreur init : {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Overlay de migration (affiché au démarrage si migration effectuée)
        // ------------------------------------------------------------------
        private void TryShowMigrationOverlay()
        {
            if (_migrationOverlay is null) return;

            // ★ CORRECTION : Application.Properties (Xamarin.Forms) n'existe plus en MAUI.
            // MauiProgram.cs enregistre déjà le résultat de migration en DI quand il y en a un.
            var migrationResult = Resolve<MigrationResult>();
            if (migrationResult is { Success: true, MigratedKeys: > 0 })
            {
                _ = _migrationOverlay.ShowAsync(migrationResult.MigratedKeys, migrationResult.BackupPath);
            }
        }

        // ------------------------------------------------------------------
        // Event handlers : menu + activity bar
        // ------------------------------------------------------------------
        private void AttachExtensionsEventHandlers()
        {
            Sidebar.SessionMoved += OnSidebarSessionMovedPersist;
            MenuBar.MenuCommanded += OnExtensionsMenuCommanded;
            // ★ RETRAIT (31/08) : ActivityBar a quitté MainPage.xaml (voir
            // WireMenusAndSidebar, MainPage.xaml.cs) — OnExtensionsActivitySelected
            // n'a plus de source ; ses cases ("palette"/"gallery"/"proactive")
            // n'étaient de toute façon déjà atteintes par aucun bouton visible
            // (ActivityBarView ne les a jamais émises).
        }

        private void OnExtensionsMenuCommanded(string id)
        {
            switch (id)
            {
                case "view.commandpalette": ToggleCommandPalette(); break;
                case "view.proactive": ToggleProactiveActions(); break;
                case "ai.gallery": TogglePluginGallery(); break;
            }
        }

        // ★ Plus abonnée à rien (voir AttachExtensionsEventHandlers ci-dessus) — gardée
        // telle quelle, inoffensive, au cas où une vraie palette/proactive/galerie
        // aurait un jour un bouton dédié qui voudrait réutiliser ces id.
        private void OnExtensionsActivitySelected(string id)
        {
            switch (id)
            {
                case "palette": ToggleCommandPalette(); break;
                case "gallery": TogglePluginGallery(); break;
                case "proactive": ToggleProactiveActions(); break;
            }
        }

        // ------------------------------------------------------------------
        // Command Palette (Ctrl+Shift+P)
        // ------------------------------------------------------------------
        public void ToggleCommandPalette()
        {
            if (_commandPalette == null) return;

            if (_commandPalette.IsVisible)
            {
                _commandPalette.Close();
            }
            else
            {
                _commandPalette.Open(BuildActionContext());
            }
        }

        /// <summary>
        /// ★ v28 : Tracking analytics des commandes palette + routage.
        /// </summary>
        private void OnPaletteCommandInvoked(string command)
        {
            _commandPalette?.Close();
            if (string.IsNullOrWhiteSpace(command)) return;

            // ★ Analytics : track l'exécution palette
            _analytics?.Record(Moto.Core.AI.Analytics.AnalyticsEventKind.PaletteCommandExecuted, command);

            if (command.StartsWith("menu:", StringComparison.OrdinalIgnoreCase))
                OnMenuCommanded(command.Substring(5));
            else
                OnAiCommandSubmitted(command);
        }

        // ------------------------------------------------------------------
        // Hotkey Windows : Ctrl+Shift+P
        // ------------------------------------------------------------------
        private void AttachWindowsHotkey()
        {
#if WINDOWS
            try
            {
                var nativeWindow = Application.Current.Windows[0].Handler.PlatformView
                    as Microsoft.UI.Xaml.Window;

                if (nativeWindow?.Content is Microsoft.UI.Xaml.UIElement root)
                {
                    root.PreviewKeyDown += OnWindowsPreviewKeyDown;
                }
            }
            catch
            {
                // Le hotkey est optionnel.
            }
#endif
        }

#if WINDOWS
        private void OnWindowsPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // ★ global:: nécessaire : "Windows" est aussi un namespace de ce projet
            // (Moto.Editor.Windows), qui masquerait sinon la racine WinRT "Windows.*".
            if (e.Key != global::Windows.System.VirtualKey.P) return;

            var ctrl = Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Control)
                .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);

            var shift = Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Shift)
                .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (ctrl && shift)
            {
                ToggleCommandPalette();
                e.Handled = true;
            }
        }
#endif

        // ------------------------------------------------------------------
        // Actions contextuelles
        // ------------------------------------------------------------------
        private ActionContext BuildActionContext()
        {
            return new ActionContext
            {
                HasOpenDocument = _viewModel.SelectedDocument != null,
                IsTerminalVisible = _viewModel.IsTerminalVisible,
                IsMaximized = _maximized,
                CurrentFilePath = _viewModel.SelectedDocument?.Path,
                HasErrors = (_viewModel.SelectedDocument?.ErrorCount ?? 0) > 0,
                OpenTabsCount = _viewModel.Documents.Count
            };
        }

        private void ShowContextualActions()
        {
            var actions = _actionsEngine.GetActions(BuildActionContext());
            if (actions.Count == 0)
            {
                StatusBar.SetStatus("Aucune action contextuelle disponible.");
                return;
            }
            StatusBar.SetStatus("💡 Actions : " + string.Join(" | ", actions.Select(a => a.Title)));
        }

        private void HandleContextualAction(string actionId)
        {
            switch (actionId.ToLowerInvariant())
            {
                case "layout-optimize":
                    ApplyLayoutSettings();
                    StatusBar.SetStatus("✅ Layout optimisé.");
                    break;
                case "maximize":
                    if (!_maximized) OnMaximizeToggled();
                    break;
                case "layout-restore":
                    if (_maximized) OnMaximizeToggled();
                    break;
                case "terminal-open":
                    _viewModel.IsTerminalVisible = true;
                    StatusBar.SetStatus("Terminal ouvert.");
                    break;
                case "terminal-test":
                    _viewModel.IsTerminalVisible = true;
                    _viewModel.TerminalLines.Add(new TerminalLine { Text = "$ echo 'Test terminal OK'" });
                    StatusBar.SetStatus("✅ Test terminal réussi.");
                    break;
                case "format":
                    OnAiCommandSubmitted("/sample-format format");
                    break;
                case "explain":
                    OnAiBandPrompt("cortex", "Explique ce code");
                    break;
                case "build":
                    OnBuildClicked(null, EventArgs.Empty);
                    break;
                default:
                    StatusBar.SetStatus($"Action inconnue : {actionId}");
                    break;
            }
            RefreshHomeStats();
        }

        // ------------------------------------------------------------------
        // Suggestions proactives (timer toutes les 30s)
        // ------------------------------------------------------------------
        private void SetupProactiveTimer()
        {
            _proactiveTimer?.Stop();

            _proactiveTimer = new System.Timers.Timer(30000) { AutoReset = true };
            _proactiveTimer.Elapsed += (s, e) => RefreshProactiveSuggestions();
            _proactiveTimer.Start();
        }

        /// <summary>
        /// ★ v28 : Refresh avec branchement ProactivePanel v27 + analytics tracking.
        /// </summary>
        private void RefreshProactiveSuggestions()
        {
            // v27 : ProactivePanel (avec analytics injecté via DI)
            if (_proactiveEngine != null && _proactivePanel != null)
            {
                var context = BuildActionContext();
                var suggestions = _proactiveEngine.GetSuggestions(context);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _proactivePanel.UpdateSuggestions(suggestions);
                });
            }

            // v26 legacy : ProactiveActionsView (conservé pour compatibilité)
            if (_proactiveActions != null)
            {
                var actions = _actionsEngine.GetActions(BuildActionContext());
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _proactiveActions.UpdateActions(actions);
                });
            }
        }

        private void ToggleProactiveActions()
        {
            if (_proactivePanel != null)
            {
                _proactivePanel.IsVisible = !_proactivePanel.IsVisible;
                if (_proactivePanel.IsVisible) RefreshProactiveSuggestions();
            }
            else if (_proactiveActions != null)
            {
                _proactiveActions.IsVisible = !_proactiveActions.IsVisible;
                if (_proactiveActions.IsVisible) RefreshProactiveSuggestions();
            }
        }

        // ------------------------------------------------------------------
        // Galerie de plugins
        // ------------------------------------------------------------------
        private void TogglePluginGallery()
        {
            if (_pluginGallery is null) return;

            _pluginGallery.IsVisible = !_pluginGallery.IsVisible;
            if (_pluginGallery.IsVisible)
                _pluginGallery.LoadGallery();
        }

        // ------------------------------------------------------------------
        // Drag & drop persistant (WorkspaceStateService)
        // ------------------------------------------------------------------
        private void OnSidebarSessionMovedPersist(string sessionId, string sectionName)
        {
            StatusBar.SetStatus($"📌 {sessionId} → {sectionName}");

            if (_workspaceState is null) return;

            var section = MapSessionSection(sectionName);
            _ = _workspaceState.SetSessionSectionAsync(sessionId, section);

            RefreshHomeStats();
        }

        private static SessionSection MapSessionSection(string sectionName)
        {
            var normalized = sectionName?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "épinglés" or "pinned" => SessionSection.Pinned,
                "projets" or "projects" => SessionSection.Projects,
                _ => SessionSection.Recent
            };
        }

        // Moto.Editor/MainPage.Extensions.cs — AJOUTS

        private LiveLanguageSwitcher? _liveLanguageSwitcher;
        private DocumentTranslationAdvisor? _translationAdvisor;
        private TabVariantsEngine? _tabVariantsEngine;

        // ------------------------------------------------------------------
        // AI settings avec confirmation (via AiConfirmationService v27)
        // ------------------------------------------------------------------
        private async Task HandleAiSettingsCommandAsync(string args)
        {
            if (_aiSettings is null)
            {
                StatusBar.SetStatus("Ouvre d'abord un workspace.");
                return;
            }

            var parts = args.Split(' ', 3);
            var sub = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;

            switch (sub)
            {
                case "list":
                    var keys = _aiSettings.GetModifiableKeys();
                    StatusBar.SetStatus($"🔓 {keys.Count} clés modifiables par l'IA.");
                    break;

                case "set":
                    if (parts.Length < 3)
                    {
                        StatusBar.SetStatus("Usage : /ai-settings set <key> <value>");
                        return;
                    }

                    var preview = _aiSettings.PrepareSetting(parts[1], ParseAiSettingValue(parts[2]));

                    if (!preview.IsValid)
                    {
                        StatusBar.SetStatus($"❌ {preview.ErrorMessage}");
                        return;
                    }

                    // v27 : utilise AiConfirmationService si disponible
                    bool confirmed;
                    if (_confirmationService != null)
                    {
                        var result = await _confirmationService.ConfirmSettingChangeAsync(
                            preview.Key, preview.OldValue, preview.NewValue!);
                        confirmed = result.Confirmed;
                    }
                    else
                    {
                        // Fallback : DisplayAlert MAUI
                        confirmed = await DisplayAlert(
                            "Confirmation IA",
                            $"MOTO AI veut modifier un paramètre.{Environment.NewLine}{Environment.NewLine}" +
                            $"{preview.Key}{Environment.NewLine}" +
                            $"Ancienne valeur : {preview.OldValue?.ToString() ?? "(null)"}{Environment.NewLine}" +
                            $"Nouvelle valeur : {preview.NewValue?.ToString() ?? "(null)"}",
                            "Appliquer",
                            "Annuler");
                    }

                    if (!confirmed)
                    {
                        StatusBar.SetStatus("Modification IA annulée.");
                        return;
                    }

                    var applyResult = _aiSettings.ApplySetting(preview);
                    StatusBar.SetStatus(applyResult.Message);

                    if (applyResult.Success)
                    {
                        SettingsApplier.ApplyAll(_viewModel, EditorPane.Editor, SettingsEngine.Shared);
                    }
                    break;

                case "help":
                default:
                    StatusBar.SetStatus("Usage : /ai-settings list | set <key> <value>");
                    break;
            }

            RefreshHomeStats();
        }

        // Version synchrone legacy (v25) conservée pour compatibilité
        private void HandleAiSettingsCommand(string args)
        {
            _ = HandleAiSettingsCommandAsync(args);
        }

        private static object ParseAiSettingValue(string raw)
        {
            if (bool.TryParse(raw, out var b)) return b;
            if (int.TryParse(raw, out var i)) return i;
            return raw;
        }

        // ------------------------------------------------------------------
        // ★ v29 : Ouverture de fenêtres spécialisées (WindowManager)
        // ------------------------------------------------------------------
        private void OpenSpecializedWindow(string kind)
        {
            if (_windowManager == null)
            {
                StatusBar.SetStatus("WindowManager non disponible.");
                return;
            }

            var normalized = kind.ToLowerInvariant();

            switch (normalized)
            {
                case "editor":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Editor, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage(
                                "Éditeur", new Controls.EditorPaneView())));
                    break;

                case "debug":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Debug, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage(
                                "Debug", new Views.DebugPanelProView())));
                    break;

                case "analytics":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Analytics, () =>
                    {
                        var view = new Views.AnalyticsDashboardView();
                        if (_analytics != null) view.SetAnalytics(_analytics);
                        return new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("Analytics", view));
                    });
                    break;

                case "plugin":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Plugin, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage(
                                "Plugins", new Views.PluginGalleryView(_pluginRegistry, _marketplaceClient, GetPluginsDirectory()))));
                    break;

                default:
                    StatusBar.SetStatus($"Fenêtre inconnue : {kind}");
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private string GetWorkspaceRoot()
        {
            if (!string.IsNullOrWhiteSpace(_currentRoot))
                return _currentRoot;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MotoProjects");
        }

        private string GetPluginsDirectory()
            => Path.Combine(GetWorkspaceRoot(), "plugins");
    }
}
