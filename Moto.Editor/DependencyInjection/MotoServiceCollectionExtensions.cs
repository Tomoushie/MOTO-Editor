// Moto.Editor/DependencyInjection/MotoServiceCollectionExtensions.cs
using System;
using System.Diagnostics;
using System.IO;

// ── Microsoft Extensions
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ── Core racine
using Moto.Core;

// ── Core AI
using Moto.Core.AI.Actions;
using Moto.Core.AI.Agents;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Commands;
using Moto.Core.AI.Context;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Embedded;
using Moto.Core.AI.Catalog;
using Moto.Core.AI.Coherence;
using Moto.Core.AI.GameDev;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Mcp;
using Moto.Core.AI.Meta;
using Moto.Core.AI.Models;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Orchestration;
using Moto.Core.AI.Profiles;
using Moto.Core.AI.Speculative;
using Moto.Core.AI.Style;
using Moto.Core.AI.Suggestions;
using Moto.Core.AI.Ux;
using Moto.Core.AI.Workspace;

// ── Core Services
using Moto.Core.Chat;
using Moto.Core.Collab;
using Moto.Core.Debug;
using Moto.Core.DevOps;
using Moto.Core.Logging;
using Moto.Core.LSP;
using Moto.Core.LSP.InlayHints;
using Moto.Core.Monitoring;
using Moto.Core.Performance;
using Moto.Core.Plugins;
using Moto.Core.Plugins.Marketplace;
using Moto.Core.Refactor;
using Moto.Core.Security;
using Moto.Core.Services;
using Moto.Core.Settings;
using Moto.Core.Settings.Profiles;
using Moto.Core.Themes;
using Moto.Core.Licensing;

// ── Editor
using Moto.Editor.Services;
using Moto.Editor.Views;
using Moto.Editor.Windows;


// ── XENO
using Snake2000.Engine.AgentIntegrated.Pipeline;

namespace Moto.Editor.DependencyInjection
{
    /// <summary>
    /// Enregistrement centralisé des services MOTO.
    /// Classe partial : peut être complétée dans d'autres fichiers sans conflit.
    /// </summary>
    public static partial class MotoServiceCollectionExtensions
    {
        /// <summary>
        /// Point d'entrée principal d'enregistrement des services MOTO.
        /// </summary>
        public static IServiceCollection RegisterMotoServices(this IServiceCollection services)
        {
            var workspaceRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MotoProjects");

            var motoAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MotoEditor");

            var pluginsDirectory = Path.Combine(motoAppData, "plugins");

            var cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MotoEditor", "cache");

            // ══════════════════════════════════════════════════════════════
            // ★ Plateforme : IPlatformShell (adapter par OS détecté)
            // ══════════════════════════════════════════════════════════════
#if WINDOWS
            services.AddSingleton<Moto.Editor.Platforms.Windows.SystemMenuAboutService>();
            services.AddSingleton<Moto.Editor.Platforms.Windows.GlobalHotkeyService>();
            services.AddSingleton<Moto.Editor.Platforms.Windows.WindowsShellAdapter>();
#endif
#if MACOS || MACCATALYST
            services.AddSingleton<Moto.Editor.Platforms.Mac.MacShellAdapter>();
#endif
            // Linux : MAUI n'est pas supporté sur Linux (voir Docs/Moto.Editor.build-pipeline.md,
            // "Linux → Core uniquement"), donc Moto.Editor.Platforms.Linux n'est jamais compilé
            // dans cette TFM — pas de branche Linux ici.

            services.AddSingleton<Moto.Core.Platform.IPlatformShell>(sp =>
            {
#if WINDOWS
                return sp.GetRequiredService<Moto.Editor.Platforms.Windows.WindowsShellAdapter>();
#elif MACOS || MACCATALYST
                return sp.GetRequiredService<Moto.Editor.Platforms.Mac.MacShellAdapter>();
#else
                throw new PlatformNotSupportedException("Aucun IPlatformShell pour cette plateforme.");
#endif
            });

            // ══════════════════════════════════════════════════════════════
            // 1. Configuration & Utilitaires de base (Aucune dépendance)
            // ══════════════════════════════════════════════════════════════
            // EmbeddedLlmConfig / ModelDownloader : cluster IA embarquée mis de côté (voir Moto.Core.csproj)
            services.AddSingleton<ModelCompressionService>();
            services.AddSingleton<MemoryMappedModelLoader>();
            services.AddSingleton<KvCacheManager>();
            services.AddSingleton<LazyLoadingManager>();
            services.AddSingleton<AggressiveCacheManager>(sp => new AggressiveCacheManager(cacheDirectory));
            services.AddSingleton<UltraLiteMode>();
            services.AddSingleton<PerformanceProfiler>();

            // ══════════════════════════════════════════════════════════════
            // 2. Services de base (v24-v31) - Fondations
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<Ed25519Signer>(sp => new Ed25519Signer(sp.GetRequiredService<ILogger<Ed25519Signer>>()));
            services.AddSingleton<Moto.Editor.Services.ChatService>(sp => new Moto.Editor.Services.ChatService(workspaceRoot, null, null, sp.GetRequiredService<ILogger<Moto.Editor.Services.ChatService>>()));
            services.AddSingleton<CortexEngine>(_ => new CortexEngine(workspaceRoot));
            services.AddSingleton<WorkspaceStateService>(sp => new WorkspaceStateService(workspaceRoot, sp.GetRequiredService<ILogger<WorkspaceStateService>>()));
            services.AddSingleton<SettingsEngine>(_ => SettingsEngine.Shared);
            services.AddSingleton<ProfileManager>(sp => new ProfileManager(sp.GetRequiredService<SettingsEngine>(), sp.GetRequiredService<ILogger<ProfileManager>>()));
            services.AddSingleton<SettingsImporterExporter>(sp => new SettingsImporterExporter(sp.GetRequiredService<SettingsEngine>(), sp.GetRequiredService<ILogger<SettingsImporterExporter>>()));
            services.AddSingleton<SettingsRollbackEngine>(sp => new SettingsRollbackEngine(sp.GetRequiredService<ILogger<SettingsRollbackEngine>>()));
            services.AddSingleton<MarketplaceClient>();
            services.AddSingleton<MarketplaceClientPro>();
            services.AddSingleton<PluginRegistry>(sp => new PluginRegistry(sp.GetRequiredService<SettingsEngine>(), sp.GetRequiredService<ILogger<PluginRegistry>>()));
            services.AddSingleton<ContextualActionsEngine>();
            services.AddSingleton<AiConfirmationService>();
            services.AddSingleton<ProactiveAnalyticsEngine>(_ => new ProactiveAnalyticsEngine(workspaceRoot));
            // LanguageServerManager : LSP mis de côté pour cette passe (voir Moto.Core.csproj)
            services.AddSingleton<ConfirmationPolicyEngine>(sp => new ConfirmationPolicyEngine(sp.GetRequiredService<SettingsEngine>()));
            services.AddSingleton<DismissPersistenceEngine>(_ => new DismissPersistenceEngine(workspaceRoot));
            services.AddSingleton<AgentScorer>();
            services.AddSingleton<WindowManager>();
            services.AddSingleton<ThemeManager>(sp => new ThemeManager(sp.GetRequiredService<ILogger<ThemeManager>>()));
            services.AddSingleton<PluginMalwareScanner>(sp => new PluginMalwareScanner(sp.GetRequiredService<ILogger<PluginMalwareScanner>>()));
            // AnalyticsWebSocketServer : jamais implémenté

            // Services de base avec dépendances internes
            services.AddSingleton<CommandPaletteEngine>(sp => new CommandPaletteEngine(sp.GetRequiredService<ContextualActionsEngine>()));
            services.AddSingleton<ProactiveSuggestionsEngine>(sp => new ProactiveSuggestionsEngine(sp.GetRequiredService<ContextualActionsEngine>(), sp.GetRequiredService<ProactiveAnalyticsEngine>()));
            // IInlayHintProvider/InlayHintService : LSP mis de côté pour cette passe
            services.AddSingleton<AgentOrchestratorV3>(sp => new AgentOrchestratorV3(sp.GetRequiredService<ContextualActionsEngine>(), sp.GetRequiredService<ProactiveAnalyticsEngine>(), sp.GetRequiredService<CortexEngine>()));

            // ══════════════════════════════════════════════════════════════
            // ★ v40 / v37 : IA embarquée + optimisations avancées + modes IA
            // Cluster entier mis de côté pour cette passe (voir Moto.Core.csproj) :
            // HeavyProcessLauncher/IsolatedInferenceHost/SmartModelManager/EmbeddedLlmEngine/
            // ModelSecurityService/DualModelRouter/DualModelIntegration/SpeculativeDecoder/
            // SpeculativeActivationService/LayeredModelLoader/LayeredActivationService/
            // ModelBundleManager/AiOptimizationsBenchmark/InferenceThrottler/
            // AdaptiveResourceGovernor/InferenceWatchdog/AiModeManager/AiAutoBenchmark.
            // Le chemin Ollama (MotoAiKernel) reste la voie IA active.
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<SystemLoadMonitor>();
            services.AddSingleton<AiObservabilityService>();

            // ══════════════════════════════════════════════════════════════
            // ★ v32 / v33 / v34 : Performance, Refactor, UI, Marketplace
            // ══════════════════════════════════════════════════════════════
            // RefactorEngine/RefactorAnalyzer/PerFileServiceManager : mis de côté (voir Moto.Core.csproj)
            services.AddSingleton<RefactorFixer>();
            services.AddSingleton<RefactorLearningStore>();
            services.AddSingleton<ProfilingHeatmapExporter>(sp => new ProfilingHeatmapExporter(sp.GetRequiredService<PerformanceProfiler>()));
            services.AddSingleton<MemoryPressureMonitor>(sp => new MemoryPressureMonitor(sp.GetRequiredService<UltraLiteMode>()));
            services.AddSingleton<IncrementalIndexer>(_ => new IncrementalIndexer(Path.Combine(cacheDirectory, "index")));
            services.AddSingleton<SymbolCacheManager>(_ => new SymbolCacheManager(Path.Combine(cacheDirectory, "symbols")));
            services.AddSingleton<PluginResourceBudget>();
            services.AddSingleton<SmallFileFastPath>();
            services.AddSingleton<AdaptiveModelSelector>();
            services.AddSingleton<UiCompressor>();
            // VoiceEngine mis de côté pour cette passe (voir Moto.Core.csproj)
            services.AddSingleton<LicenseValidator>();
            services.AddSingleton<PluginRatingService>();
            services.AddSingleton<MarketplaceAccountService>();
            services.AddSingleton<MarketplaceModerationService>();

            // ══════════════════════════════════════════════════════════════
            // ★ Fondations IA pour MultiAgentSuggestionEngine
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<NeuralMode>(sp =>
                new NeuralMode(workspaceRoot, new CortexMemory(workspaceRoot)));
            services.AddSingleton<AIWorkspace>(sp =>
                new AIWorkspace(workspaceRoot));

            // MultiAgentSuggestionEngine : mis de côté (API Cortex/Neural/Workspace jamais implémentée)

            // ══════════════════════════════════════════════════════════════
            // ★ ContextEngine avec hooks PresenceAware + FeatureFlag
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<PresenceAwareSuggestionGate>();
            services.AddSingleton<FeatureFlagService>();

            services.AddSingleton<Moto.Core.AI.Context.ContextEngine>(sp =>
                new Moto.Core.AI.Context.ContextEngine(workspaceRoot,
                    sp.GetRequiredService<PresenceAwareSuggestionGate>(),
                    sp.GetRequiredService<FeatureFlagService>()));

            // ══════════════════════════════════════════════════════════════
            // ★ v40 : XENO lazy-wrapped
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<XenoPipelineV5>(sp =>
                sp.GetRequiredService<LazyLoadingManager>().Get(
                    "xeno",
                    () => ActivatorUtilities.CreateInstance<XenoPipelineV5>(sp)));
            // XenoPipelineV5_Optimized : prototype incomplet mis de côté (voir Snake2000.Engine.csproj)
            services.AddSingleton<NeuralMode_Optimized>(sp =>
                new NeuralMode_Optimized(sp.GetRequiredService<AggressiveCacheManager>()));

            // ══════════════════════════════════════════════════════════════
            // Services Transients & Scoped restants
            // ══════════════════════════════════════════════════════════════
            services.AddTransient<DebugEngine>();
            services.AddTransient<DebugEnginePro>();
            services.AddTransient<CrdtSession>();
            services.AddTransient<ThemePreviewView>();
            services.AddTransient<ThemeSelectorView>();
            // IRefundService/StripeRefundService : jamais implémentés (feature paiement non construite)

            // ══════════════════════════════════════════════════════════════
            // Views (transient)
            // ══════════════════════════════════════════════════════════════
            services.AddTransient<HomeView>();
            services.AddTransient<MigrationOverlay>();
            services.AddTransient<ConfirmationOverlay>();
            services.AddTransient<AnalyticsDashboardView>();
            services.AddTransient<DebugPanelView>();
            services.AddTransient<DebugPanelProView>();
            services.AddTransient<CommandPaletteView>(sp => new CommandPaletteView(sp.GetRequiredService<CommandPaletteEngine>()));
            services.AddTransient<ProactivePanel>(sp => new ProactivePanel(sp.GetRequiredService<ProactiveSuggestionsEngine>()));
            services.AddTransient<PluginGalleryView>(sp => new PluginGalleryView(sp.GetRequiredService<PluginRegistry>(), sp.GetRequiredService<MarketplaceClient>(), pluginsDirectory));

            // AdminDashboardView : jamais implémentée. AdvancedAiSettingsView/ModelManagerView/
            // ModelConsentDialog : mises de côté avec le cluster IA embarquée (voir Moto.Editor.csproj)
            services.AddTransient<PerformanceDashboardView>();
            services.AddTransient<SubscriptionOverlay>();
            services.AddTransient<AiMonitoringView>();
            services.AddTransient<AboutView>();

            // ══════════════════════════════════════════════════════════════
            // ★ Services additionnels (Trial, Collaboration, Agents, etc.)
            // ══════════════════════════════════════════════════════════════
            services.AddMotoTrialAndCollaborationServices();
            services.AddMotoSpecializedAgents();
            services.AddMotoMarketplaceAndSecurityServices();
            services.AddMotoDevOpsAndGitServices();
            services.AddMotoPedagogyAndProfilesServices();
            services.AddMotoAiServices();
            services.AddMotoFinalWaveServices();

            return services;
        }

        /// <summary>
        /// Item 63 — Enregistrement additif des services Trial + Collaboration.
        /// </summary>
        public static IServiceCollection AddMotoTrialAndCollaborationServices(this IServiceCollection services)
        {
            // ══════════════ PHASE 1 : CONFIG ══════════════
            services.AddSingleton<StructuredLogCollector>();

            // ══════════════ PHASE 2 : ISOLATION ══════════════
            services.AddSingleton<LocalModelResourceGovernor>();

            // ══════════════ PHASE 3 : MANAGER ══════════════
            services.AddSingleton<SessionBookmarkService>();
            services.AddSingleton<SharedRunConfigService>();
            services.AddSingleton<SharedScratchpadService>();

            // ══════════════ PHASE 4 : ENGINE ══════════════
            services.AddSingleton<UxModeService>();
            services.AddSingleton<InlineDiffPreviewService>();
            services.AddSingleton<CollabRoleService>();
            services.AddSingleton<ReviewLaneService>();
            services.AddSingleton<AnnotationLayerService>();
            services.AddSingleton<PairSessionTimerService>();
            services.AddSingleton<WhiteboardService>();
            services.AddSingleton<TerminalService>();
            services.AddSingleton<LightweightPrService>();

            // ══════════════ PHASE 5 : OPTIMISATIONS ══════════════
            services.AddSingleton<AdaptivePrefetchService>();
            services.AddSingleton<SpeculativeLogitsVerifier>();
            services.AddSingleton<PresenceAwareSuggestionGate>();

            // ══════════════ PHASE 6 : MONITORING ══════════════
            services.AddSingleton<CircuitBreakerStateService>();
            services.AddSingleton<SecureLogUploader>();

            return services;
        }

        /// <summary>
        /// Item 72 — Enregistrement additif des 20 agents spécialisés IA.
        /// </summary>
        public static IServiceCollection AddMotoSpecializedAgents(this IServiceCollection services)
        {
            // Fondations
            services.AddSingleton<ExplainabilityLogger>();

            // Agents LLM
            services.AddSingleton<ISpecializedAgent, TestSkeletonAgent>();
            services.AddSingleton<ISpecializedAgent, ExplainChangeAgent>();
            services.AddSingleton<ISpecializedAgent, CommitMessageAgent>();
            services.AddSingleton<ISpecializedAgent, SearchSummarizerAgent>();
            services.AddSingleton<ISpecializedAgent, SnippetGeneratorAgent>();
            services.AddSingleton<ISpecializedAgent, ChangelogAgent>();

            // Agents heuristiques
            services.AddSingleton<ISpecializedAgent, SecurityHintAgent>();
            services.AddSingleton<ISpecializedAgent, PrivacyScannerAgent>();
            services.AddSingleton<ISpecializedAgent, DependencyRiskAgent>();
            services.AddSingleton<ISpecializedAgent, AutoFormatPolicyAgent>();
            services.AddSingleton<ISpecializedAgent, SmartTodoAgent>();
            services.AddSingleton<ISpecializedAgent, TestFlakinessAgent>();
            services.AddSingleton<ISpecializedAgent, CodeHealthAgent>();

            // Registre
            services.AddSingleton<SpecializedAgentRegistry>();
            services.AddSingleton<ISpecializedAgent, AgentCostEstimatorAgent>();

            // Services avancés
            services.AddSingleton<LocalModelDistillationService>();
            services.AddSingleton<LocalLlmSandbox>();
            services.AddSingleton<AgentMarketplaceService>();
            services.AddSingleton<LocalRlFeedbackLoop>();
            services.AddSingleton<RefactorWalkthroughService>();

            // Vues associées
            services.AddTransient<ReviewLaneView>();
            services.AddTransient<WhiteboardView>();

            return services;
        }

        /// <summary>
        /// Services Marketplace, Sécurité, Git et MCP.
        /// </summary>
        public static IServiceCollection AddMotoMarketplaceAndSecurityServices(this IServiceCollection services)
        {
            // Git
            services.AddSingleton<GitService>();

            // MCP & Subagents
            services.AddSingleton<McpServerManager>();
            services.AddSingleton<SubagentOrchestrator>();
            services.AddSingleton<PromptInjectionProtector>();

            // Licensing
            services.AddSingleton<TrialLicenseManager>();
            services.AddSingleton<LicenseTransferService>();

            // Security
            services.AddSingleton<PluginSandboxService>();
            services.AddSingleton<VulnerabilityScannerService>();
            services.AddSingleton<VerifiedPublisherService>();

            // Marketplace
            services.AddSingleton<SubscriptionBundleService>();
            services.AddSingleton<MicroDonationService>();

            // Vues
            services.AddTransient<AgentMarketplaceView>();

            return services;
        }

        /// <summary>
        /// Services DevOps, Git UI, MCP avancé.
        /// </summary>
        public static IServiceCollection AddMotoDevOpsAndGitServices(this IServiceCollection services)
        {
            // Git
            services.AddTransient<GitPanelView>();

            // MCP avancé
            services.AddSingleton<McpPermissionService>();
            services.AddSingleton<McpHookService>();

            // DevOps / Testing / Observability
            services.AddSingleton<PerfGateService>();
            services.AddSingleton<SyntheticJourneyService>();
            services.AddSingleton<PluginFuzzingService>();
            services.AddSingleton<CrashTriageService>();
            services.AddSingleton<PerfBundleService>();
            services.AddSingleton<FeatureFlagService>();
            services.AddSingleton<TelemetryPrivacyService>();
            services.AddSingleton<DependencyUpdateBotService>();

            // Vues
            services.AddTransient<DevOpsDashboardView>();
            services.AddTransient<PerformanceStatusBarView>();

            return services;
        }

        /// <summary>
        /// Services Pédagogie, Profils IA, UX IA, Cohérence, Style.
        /// </summary>
        public static IServiceCollection AddMotoPedagogyAndProfilesServices(this IServiceCollection services)
        {
            services.AddSingleton<PedagogyEngine>();
            services.AddSingleton<AiProfileService>();
            services.AddSingleton<AiUxService>();
            services.AddSingleton<CoherenceGuardService>();
            services.AddSingleton<StyleLearningService>();

            return services;
        }

        /// <summary>
        /// Services AI, modèles, ressources.
        /// </summary>
        public static IServiceCollection AddMotoAiServices(this IServiceCollection services)
        {
            services.AddSingleton<ModelProfileService>();

            return services;
        }

        /// <summary>
        /// Services finale (Vague B/C UX, Snake2000, MOTO meta, FeatureCatalog).
        /// </summary>
        public static IServiceCollection AddMotoFinalWaveServices(this IServiceCollection services)
        {
            // BONUS UX (Vague B/C)
            services.AddSingleton<UxEnhancementService>();

            // Snake2000
            services.AddSingleton<SnakeAssistantService>();

            // MOTO meta
            services.AddSingleton<MotoSelfCareService>();

            // Catalogue de features
            services.AddSingleton<FeatureCatalog>();

            return services;
        }
    }
}
