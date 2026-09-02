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
            // ★ RETRAIT (02/09, état des lieux) : ResolveExtensionServices() appelée
            // d'ici échouait en silence — CAUSE RÉELLE confirmée par un breadcrumb
            // ("services DI non disponibles, sortie anticipée") : ni Handler (celui de
            // MainPage) ni Application.Current.Handler n'existent encore à ce stade du
            // constructeur. Résultat : _commandPalette (Ctrl+Maj+P, palette de
            // commandes — signalé par Tom), _confirmationOverlay, _proactivePanel,
            // _analytics, _windowManager restaient TOUS null pour toujours, la méthode
            // sortant avant de les assigner — aucune exception visible (le catch de
            // cette méthode ne partait que vers Debug.WriteLine, invisible sans
            // débogueur attaché, corrigé au passage). Même famille de bug que
            // AttachWindowsHotkey ce même jour. Déplacée dans OnPageLoaded
            // (MainPage.xaml.cs), où Handler est déjà garanti prêt (Loaded ne se
            // déclenche qu'une fois la page montée avec son handler natif).
            SetupProactiveTimer();
            AttachExtensionsEventHandlers();
            // ★ RETRAIT (02/09, état des lieux) : AttachWindowsHotkey() appelée d'ici
            // — donc dans le constructeur de MainPage, avant que la fenêtre native
            // existe — causait un ArgumentOutOfRangeException avalé en silence,
            // empêchant Ctrl+Shift+P de fonctionner. Déplacée dans OnPageLoaded
            // (MainPage.xaml.cs), où nativeWindow est déjà résolu en sécurité.
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
                    App.Breadcrumb("ResolveExtensionServices — services DI non disponibles, sortie anticipée");
                    return;
                }

                // ── Services v25-v26 ──
                _workspaceState = services.GetService<WorkspaceStateService>();
                _pluginRegistry = services.GetService<PluginRegistry>();
                _marketplaceClient = services.GetService<MarketplaceClient>();

                // ★ CORRECTION (02/09, réparation Galerie de plugins) : _pluginGallery
                // (construit dans WirePanels(), MainPage.xaml.cs, AVANT que cette
                // méthode tourne) recevait `new PluginGalleryView(null, null, ...)` —
                // _pluginRegistry/_marketplaceClient n'existaient pas encore à ce
                // stade. SetServices() existe déjà pour exactement ce cas (résoudre
                // plus tard, brancher après coup) mais n'était appelée nulle part :
                // le panneau que Tom ouvre (🧱 "Galerie de plugins") affichait donc
                // TOUJOURS "❌ Services plugins non initialisés.", quoi qu'il fasse.
                // Même patron que _analyticsDashboard.SetAnalytics(_analytics) un peu
                // plus bas dans ce fichier. Ne rend pas le système "réel" (toujours 0
                // plugin jamais installé en pratique, marketplace distant jamais
                // vérifié — voir CLAUDE.md, section "Modularité façon Zed/VS Code")
                // mais arrête d'afficher une erreur permanente sur un bouton visible.
                if (_pluginGallery != null && _pluginRegistry != null && _marketplaceClient != null)
                    _pluginGallery.SetServices(_pluginRegistry, _marketplaceClient, GetPluginsDirectory());

                // ★ AJOUT (02/09, "vrai système de plugins") : jusqu'ici
                // _pluginRegistry existait mais Register()/RegisterAsync() n'était
                // JAMAIS appelée par du code de production — 0 plugin réellement
                // "installé", quoi que fasse Tom. Enregistre ici le plugin
                // d'exemple bundlé (Moto.Plugin.SampleFormat), le seul à ce jour
                // écrit contre un contrat qui compile réellement (voir CLAUDE.md :
                // 4 autres plugins — MotoDarkPro/CortexBooster/AutoRefactorPro/
                // Template — référencent une interface IMotoPlugin qui n'existe
                // nulle part, restent hors scope). Fire-and-forget : l'échec d'un
                // plugin ne doit jamais empêcher MainPage de finir de se charger.
                if (_pluginRegistry != null)
                    _ = RegisterBundledPluginsAsync();

                // ★ AJOUT (02/09, "vrai système de plugins") : câble le pont commande
                // sur ChatService (voir HandlePluginCommandAsync plus bas) — atteint
                // ainsi TOUTES les surfaces qui envoient via _chatService.SendAsync
                // (AiChatView "MOTO AI", bandeau IA/Accueil), pas seulement une seule.
                _chatService.PluginCommandHandler = HandlePluginCommandAsync;

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

                // ★ RETRAIT (02/09, état des lieux) : _analyticsDashboard = services.
                // GetService<AnalyticsDashboardView>() retiré d'ici — EXACTEMENT le
                // même doublon fantôme déjà repéré et retiré pour _pluginGallery (voir
                // commentaire un peu plus bas dans ce fichier) : WirePanels()
                // (MainPage.xaml.cs, appelée dans le constructeur, donc AVANT que
                // OnPageLoaded lance cette méthode) construit TOUJOURS la vraie
                // instance (new AnalyticsDashboardView(), enveloppée par
                // AddFloatingPanel qui la masque et lui donne son en-tête). Cette
                // 2e instance résolue par DI ici écrasait le champ avec une instance
                // JAMAIS masquée (AddMotoOverlay ne met pas IsVisible=false, seul
                // AddFloatingPanel le fait) — resté invisible tant que
                // ResolveExtensionServices() échouait en silence (voir plus haut),
                // mais dès que ce bug a été corrigé, ce doublon s'est mis à couvrir
                // tout l'écran au démarrage (repéré par Tom, capture d'écran).
                // ── ★ v29 : WindowManager ──
                _windowManager = services.GetService<Moto.Editor.Windows.WindowManager>();

                // ★ CORRECTIF (03/09, réveil de GlobalDashboardView) : InitializeGlobalUsage()
                // (MainPage.UI.cs, appelée dans le CONSTRUCTEUR) résolvait _globalUsage via
                // Handler?.MauiContext?.Services — mais Handler est encore null à ce stade du
                // cycle de vie MAUI (avant Loaded), donc _globalUsage restait null pour toute
                // la durée de vie de l'app, exactement le même patron que _pluginGallery plus
                // haut. On retente ici, où `services` est déjà confirmé disponible.
                if (_globalUsage == null)
                {
                    _globalUsage = services.GetService<Moto.Core.Analytics.GlobalUsageEngine>();
                    _globalUsage?.StartSession();
                }

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
                // ★ DIAGNOSTIC TEMPORAIRE (02/09) : cette exception ne partait QUE vers
                // Debug.WriteLine (invisible sans débogueur attaché — on lance l'exe
                // seul). _commandPalette (et tout ce qui est résolu après lui dans ce
                // même bloc try) reste alors null en silence — c'est ce qui empêchait
                // Ctrl+Maj+P de faire quoi que ce soit (confirmé : ToggleCommandPalette
                // s'exécutait bien, mais _commandPalette était null). Journalisé dans le
                // vrai fichier de crash le temps de voir la cause exacte.
                System.Diagnostics.Debug.WriteLine($"[Extensions] Erreur init : {ex.Message}");
                App.Breadcrumb($"ResolveExtensionServices — EXCEPTION : {ex}");
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
#if WINDOWS
        /// <summary>
        /// ★ CORRECTION (02/09, état des lieux) : CAUSE RÉELLE de "Ctrl+Shift+P ne
        /// fait rien du tout", signalé par Tom. Cette méthode était appelée depuis
        /// InitializeMainPageExtensions() — exécutée DANS le constructeur de
        /// MainPage, donc bien avant que la fenêtre native existe encore.
        /// Application.Current.Windows[0] levait un ArgumentOutOfRangeException
        /// (collection vide) — confirmé par un breadcrumb temporaire, pas deviné.
        /// Le catch générique ("le hotkey est optionnel") avalait cette exception
        /// en silence depuis le début : aucune trace, échec invisible. Même famille
        /// de bug que SnapLayoutsHelper/ConfigureSnapLayouts, déjà corrigée en 08 en
        /// déplaçant l'appel dans OnPageLoaded — même remède ici : on reçoit
        /// nativeWindow déjà résolu par OnPageLoaded au lieu de le redemander trop
        /// tôt.
        /// </summary>
        private void AttachWindowsHotkey(Microsoft.UI.Xaml.Window? nativeWindow)
        {
            if (nativeWindow?.Content is Microsoft.UI.Xaml.UIElement root)
            {
                root.PreviewKeyDown += OnWindowsPreviewKeyDown;
                App.Breadcrumb("AttachWindowsHotkey — PreviewKeyDown abonné");
            }
            else
            {
                App.Breadcrumb($"AttachWindowsHotkey — IGNORÉ : nativeWindow={nativeWindow != null}");
            }
        }
#endif

#if WINDOWS
        private void OnWindowsPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // ★ AJOUT (02/09) : Échap ferme la palette de commandes si elle est
            // ouverte — jusqu'ici, refaire Ctrl+Maj+P était le seul moyen (voir
            // aussi le bouton ✕ ajouté dans CommandPaletteView.xaml). Même
            // mécanisme que Ctrl+Maj+P ci-dessous.
            if (e.Key == global::Windows.System.VirtualKey.Escape)
            {
                if (_commandPalette?.IsVisible == true)
                {
                    _commandPalette.Close();
                    e.Handled = true;
                }
                return;
            }

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

                // ★ AJOUT (03/09, "détacher un panneau" — sonde de modularité, Gap B) :
                // 5 cas manquants, désormais atteignables par le bouton ⧉ de chaque
                // panneau (voir AddFloatingPanel/KindFor, MainPage.Panels.cs) — avant
                // ceci, seule la commande cachée "/window <kind>" pouvait déjà ouvrir
                // "editor"/"debug"/"analytics"/"plugin" ci-dessus, jamais ces 5-là.
                case "cortex":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Cortex, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("Cortex", new Views.CortexView(_cortex))));
                    break;

                case "neural":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Neural, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("Neural", new Views.NeuralView(_neural))));
                    break;

                case "workspace":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Workspace, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("Workspace", new Views.AIWorkspaceView(_workspace))));
                    break;

                case "aichat":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.AiChat, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("MOTO AI", new Views.AiChatView(_chatService))));
                    break;

                case "platform":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.Platform, () =>
                        new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("Plateforme", new Views.PlatformView(_platformEngine))));
                    break;

                // ★ AJOUT (03/09, réveil de GlobalDashboardView) : jamais navigable
                // auparavant (fichier exclu du build — voir CLAUDE.md, son .xaml
                // survivait sous un nom de fichier corrompu, renommé pour ce
                // correctif). _globalUsage (GlobalUsageEngine) alimente déjà de
                // vraies statistiques en continu depuis longtemps (RecordBuild,
                // RecordDebugSession, StartSession...) sans qu'aucun écran ne les
                // affiche jusqu'ici.
                case "globaldashboard":
                    _windowManager.OpenOrFocus(Moto.Editor.Windows.WindowKind.GlobalDashboard, () =>
                    {
                        var view = new Views.GlobalDashboardView { IsVisible = true };
                        if (_globalUsage != null) view.SetEngine(_globalUsage);
                        return new Microsoft.Maui.Controls.Window(
                            new Moto.Editor.Windows.SpecializedWindowPage("Tableau de bord global", view));
                    });
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

        /// <summary>
        /// ★ AJOUT (02/09, "vrai système de plugins") : instancie et enregistre
        /// le(s) plugin(s) bundlé(s) avec MOTO Editor. Pour cette première passe,
        /// un seul plugin réel (Moto.Plugin.SampleFormat, référencé en dur via
        /// ProjectReference — pas de chargement dynamique d'un dossier externe,
        /// ça reste un chantier séparé, plus gros, voir CLAUDE.md). L'adaptateur
        /// SdkPluginAdapter (Moto.Core/Plugins/SdkAdapter.cs) fait le pont entre
        /// le contrat public du SDK (Moto.Plugin.SDK.IPlugin) et le contrat
        /// interne attendu par PluginRegistry (Moto.Core.Plugins.IPlugin).
        /// </summary>
        private async Task RegisterBundledPluginsAsync()
        {
            try
            {
                var sdkPlugin = new Moto.Plugin.SampleFormat.SampleFormatPlugin();
                var adapter = new SdkPluginAdapter(sdkPlugin);
                await _pluginRegistry!.RegisterAsync(adapter, GetWorkspaceRoot());
            }
            catch (Exception ex)
            {
                App.Breadcrumb($"RegisterBundledPluginsAsync — EXCEPTION : {ex}");
            }
        }

        /// <summary>
        /// ★ AJOUT (02/09, "vrai système de plugins") : point d'entrée partagé,
        /// câblé sur ChatService.PluginCommandHandler — atteint depuis n'importe
        /// quelle surface qui envoie un message via _chatService.SendAsync.
        /// Propose le texte à chaque plugin enregistré, dans l'ordre ; le premier
        /// qui répond (non-null) gagne. Retourne null si aucun plugin ne gère
        /// cette commande (ChatService route alors normalement vers le modèle IA).
        /// </summary>
        private async Task<string?> HandlePluginCommandAsync(string text)
        {
            if (_pluginRegistry == null) return null;

            foreach (var plugin in _pluginRegistry.GetActivePlugins())
            {
                var reply = await plugin.ExecuteCommandAsync(text, _currentRoot);
                if (reply != null) return reply;
            }
            return null;
        }
    }
}
