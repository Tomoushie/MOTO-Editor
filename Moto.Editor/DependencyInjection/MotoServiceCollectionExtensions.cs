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
using Moto.Core.AI.Internal;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Orchestration;
using Moto.Core.AI.Speculative;
using Moto.Core.AI.Suggestions;
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
using Moto.Core.Voice;
using Moto.Core.Licensing;

// ── Editor
using Moto.Editor.Services;
using Moto.Editor.Views;
using Moto.Editor.Windows;

// ── API / Marketplace
using Moto.Marketplace.Api.Services;

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
            services.AddSingleton<Moto.Editor.Platforms.Linux.LinuxShellAdapter>();

            services.AddSingleton<Moto.Core.Platform.IPlatformShell>(sp =>
            {
                switch (Moto.Core.Platform.HostPlatformDetector.Current)
                {
#if WINDOWS
                    case Moto.Core.Platform.MotoHostOs.Windows:
                        return sp.GetRequiredService<Moto.Editor.Platforms.Windows.WindowsShellAdapter>();
#endif
#if MACOS || MACCATALYST
                    case Moto.Core.Platform.MotoHostOs.MacOS:
                        return sp.GetRequiredService<Moto.Editor.Platforms.Mac.MacShellAdapter>();
#endif
                    case Moto.Core.Platform.MotoHostOs.Linux:
                        return sp.GetRequiredService<Moto.Editor.Platforms.Linux.LinuxShellAdapter>();
                    default:
                        return sp.GetRequiredService<Moto.Editor.Platforms.Linux.LinuxShellAdapter>();
                }
            });

            // ══════════════════════════════════════════════════════════════
            // 1. Configuration & Utilitaires de base (Aucune dépendance)
            // ══════════════════════════════════════════════════════════════
            var embeddedConfig = new EmbeddedLlmConfig();
            services.AddSingleton<EmbeddedLlmConfig>(_ => embeddedConfig);
            services.AddSingleton<ModelDownloader>();
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
            services.AddSingleton<ChatService>(sp => new ChatService(workspaceRoot, null, null, sp.GetRequiredService<ILogger<ChatService>>()));
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
            services.AddSingleton<LanguageServerManager>();
            services.AddSingleton<ConfirmationPolicyEngine>(sp => new ConfirmationPolicyEngine(sp.GetRequiredService<SettingsEngine>()));
            services.AddSingleton<DismissPersistenceEngine>(_ => new DismissPersistenceEngine(workspaceRoot));
            services.AddSingleton<AgentScorer>();
            services.AddSingleton<WindowManager>();
            services.AddSingleton<ThemeManager>(sp => new ThemeManager(sp.GetRequiredService<ILogger<ThemeManager>>()));
            services.AddSingleton<PluginMalwareScanner>(sp => new PluginMalwareScanner(sp.GetRequiredService<ILogger<PluginMalwareScanner>>()));
            services.AddSingleton<AnalyticsWebSocketServer>(sp => new AnalyticsWebSocketServer(sp.GetRequiredService<ILogger<AnalyticsWebSocketServer>>()));

            // Services de base avec dépendances internes
            services.AddSingleton<CommandPaletteEngine>(sp => new CommandPaletteEngine(sp.GetRequiredService<ContextualActionsEngine>()));
            services.AddSingleton<ProactiveSuggestionsEngine>(sp => new ProactiveSuggestionsEngine(sp.GetRequiredService<ContextualActionsEngine>(), sp.GetRequiredService<ProactiveAnalyticsEngine>()));
            services.AddSingleton<IInlayHintProvider, RoslynInlayHintProvider>();
            services.AddSingleton<InlayHintService>(sp => new InlayHintService(sp.GetRequiredService<IInlayHintProvider>()));
            services.AddSingleton<AgentOrchestratorV3>(sp => new AgentOrchestratorV3(sp.GetRequiredService<ContextualActionsEngine>(), sp.GetRequiredService<ProactiveAnalyticsEngine>(), sp.GetRequiredService<CortexEngine>()));

            // ══════════════════════════════════════════════════════════════
            // ★ v40 : IA embarquée + optimisations avancées
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<HeavyProcessLauncher>(sp =>
                new HeavyProcessLauncher(Process.GetCurrentProcess().MainModule?.FileName ?? "Moto.Editor.exe"));
            services.AddSingleton<IsolatedInferenceHost>(sp =>
                new IsolatedInferenceHost(sp.GetRequiredService<HeavyProcessLauncher>()));

            services.AddSingleton<SmartModelManager>(sp =>
                new SmartModelManager(
                    sp.GetRequiredService<ModelCompressionService>(),
                    sp.GetRequiredService<IsolatedInferenceHost>(),
                    sp.GetRequiredService<ModelDownloader>()));

            services.AddSingleton<EmbeddedLlmEngine>(sp =>
                new EmbeddedLlmEngine(embeddedConfig, sp.GetRequiredService<SmartModelManager>()));

            services.AddSingleton<ModelSecurityService>(sp =>
                new ModelSecurityService(sp.GetRequiredService<Ed25519Signer>()));

            services.AddSingleton<DualModelRouter>(sp =>
                new DualModelRouter(
                    sp.GetRequiredService<EmbeddedLlmEngine>(),
                    sp.GetRequiredService<EmbeddedLlmEngine>(),
                    new DualModelConfig()));
            services.AddSingleton<DualModelIntegration>(sp =>
                new DualModelIntegration(
                    sp.GetRequiredService<DualModelRouter>(),
                    sp.GetRequiredService<SmartModelManager>()));
            services.AddSingleton<SpeculativeDecoder>(sp =>
                new SpeculativeDecoder(
                    sp.GetRequiredService<EmbeddedLlmEngine>(),
                    sp.GetRequiredService<EmbeddedLlmEngine>(),
                    new SpeculativeConfig()));
            services.AddSingleton<SpeculativeActivationService>(sp =>
                new SpeculativeActivationService(
                    sp.GetRequiredService<SpeculativeDecoder>(),
                    sp.GetRequiredService<ModelDownloader>(),
                    new EmbeddedLlmConfig { ModelFileName = "qwen2.5-0.5b-q4.onnx" }));
            services.AddSingleton<LayeredModelLoader>(sp =>
            {
                var modelPath = ModelPaths.GetModelPath(embeddedConfig.ModelFileName);
                return new LayeredModelLoader(modelPath, new LayeredModelConfig());
            });
            services.AddSingleton<LayeredActivationService>(sp =>
                new LayeredActivationService(
                    sp.GetRequiredService<LayeredModelLoader>(),
                    sp.GetRequiredService<EmbeddedLlmConfig>()));
            services.AddSingleton<ModelBundleManager>(sp =>
                new ModelBundleManager(sp.GetRequiredService<ModelDownloader>()));
            services.AddSingleton<AiOptimizationsBenchmark>(sp =>
                new AiOptimizationsBenchmark(
                    sp.GetRequiredService<DualModelIntegration>(),
                    sp.GetRequiredService<SpeculativeActivationService>(),
                    sp.GetRequiredService<LayeredActivationService>(),
                    sp.GetRequiredService<SmartModelManager>()));

            services.AddSingleton<SystemLoadMonitor>();
            services.AddSingleton<InferenceThrottler>(sp => new InferenceThrottler(ResourceBudget.Minimal));
            services.AddSingleton<AdaptiveResourceGovernor>(sp =>
                new AdaptiveResourceGovernor(
                    sp.GetRequiredService<SystemLoadMonitor>(),
                    sp.GetRequiredService<InferenceThrottler>(),
                    sp.GetRequiredService<IsolatedInferenceHost>()));
            services.AddSingleton<InferenceWatchdog>(sp =>
                new InferenceWatchdog(
                    sp.GetRequiredService<IsolatedInferenceHost>(),
                    sp.GetRequiredService<SystemLoadMonitor>()));
            services.AddSingleton<AiObservabilityService>();

            // ══════════════════════════════════════════════════════════════
            // ★ v37 : Modes IA + Monitoring + Auto-Benchmark
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<AiModeManager>(sp =>
                new AiModeManager(
                    sp.GetRequiredService<AdaptiveResourceGovernor>(),
                    sp.GetRequiredService<SystemLoadMonitor>()));
            services.AddSingleton<AiAutoBenchmark>(sp =>
                new AiAutoBenchmark(
                    sp.GetRequiredService<SmartModelManager>(),
                    sp.GetRequiredService<SystemLoadMonitor>(),
                    sp.GetRequiredService<IsolatedInferenceHost>()));

            // ══════════════════════════════════════════════════════════════
            // ★ v32 / v33 / v34 : Performance, Refactor, UI, Marketplace
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<RefactorEngine>();
            services.AddSingleton<RefactorAnalyzer>();
            services.AddSingleton<RefactorFixer>();
            services.AddSingleton<RefactorLearningStore>();
            services.AddSingleton<ProfilingHeatmapExporter>(sp => new ProfilingHeatmapExporter(sp.GetRequiredService<PerformanceProfiler>()));
            services.AddSingleton<PerFileServiceManager>();
            services.AddSingleton<MemoryPressureMonitor>(sp => new MemoryPressureMonitor(sp.GetRequiredService<UltraLiteMode>()));
            services.AddSingleton<IncrementalIndexer>(_ => new IncrementalIndexer(Path.Combine(cacheDirectory, "index")));
            services.AddSingleton<SymbolCacheManager>(_ => new SymbolCacheManager(Path.Combine(cacheDirectory, "symbols")));
            services.AddSingleton<PluginResourceBudget>();
            services.AddSingleton<SmallFileFastPath>();
            services.AddSingleton<AdaptiveModelSelector>();
            services.AddSingleton<UiCompressor>();
            services.AddSingleton<VoiceEngine>();
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

            services.AddSingleton<MultiAgentSuggestionEngine>(sp =>
                new MultiAgentSuggestionEngine(
                    sp.GetRequiredService<CortexEngine>(),
                    sp.GetRequiredService<NeuralMode>(),
                    sp.GetRequiredService<AIWorkspace>()));

            // ══════════════════════════════════════════════════════════════
            // ★ ContextEngine avec hooks PresenceAware + FeatureFlag
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<PresenceAwareSuggestionGate>();
            services.AddSingleton<FeatureFlagService>();

            services.AddSingleton<ContextEngine>(sp =>
                new ContextEngine(workspaceRoot,
                    sp.GetRequiredService<Moto.Core.Collab.PresenceAwareSuggestionGate>(),
                    sp.GetRequiredService<FeatureFlagService>()));

            // ══════════════════════════════════════════════════════════════
            // ★ v40 : XENO lazy-wrapped
            // ══════════════════════════════════════════════════════════════
            services.AddSingleton<XenoPipelineV5>(sp =>
                sp.GetRequiredService<LazyLoadingManager>().Get(
                    "xeno",
                    () => ActivatorUtilities.CreateInstance<XenoPipelineV5>(sp)));
            services.AddSingleton<XenoPipelineV5_Optimized>(sp =>
                new XenoPipelineV5_Optimized(
                    sp.GetRequiredService<LazyLoadingManager>(),
                    sp.GetRequiredService<AggressiveCacheManager>()));
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
            services.AddScoped<IRefundService, StripeRefundService>();

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

            services.AddTransient<AdminDashboardView>();
            services.AddTransient<AdvancedAiSettingsView>();
            services.AddTransient<ModelManagerView>();
            services.AddTransient<ModelConsentDialog>();
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
