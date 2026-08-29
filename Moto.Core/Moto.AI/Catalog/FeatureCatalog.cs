using System;
using System.Collections.Generic;
using Moto.Core.Logging;

namespace Moto.Core.AI.Catalog;

public enum FeatureStatus { AlreadyImplemented, Available, Planned, Futuristic }

public sealed class FeatureEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public FeatureStatus Status { get; set; }
    public string Notes { get; set; } = "";
}

/// <summary>
/// Catalogue centralisé des features IA/Perf/Plugins/UX/CRDT/WOW.
/// Permet de tracker ce qui est déjà implémenté vs à venir,
/// sans dupliquer les moteurs existants (Vague 2/3).
/// </summary>
public sealed class FeatureCatalog
{
    private readonly Dictionary<string, FeatureEntry> _entries = new();
    private readonly StructuredLogCollector _log;

    public FeatureCatalog(StructuredLogCollector log)
    {
        _log = log;
        SeedCatalog();
    }

    public IReadOnlyCollection<FeatureEntry> All => _entries.Values;

    public FeatureEntry? Get(string id) =>
        _entries.TryGetValue(id, out var e) ? e : null;

    public IReadOnlyList<FeatureEntry> GetByCategory(string category)
    {
        var result = new List<FeatureEntry>();
        foreach (var e in _entries.Values)
            if (e.Category == category) result.Add(e);
        return result;
    }

    public IReadOnlyList<FeatureEntry> GetPending()
    {
        var result = new List<FeatureEntry>();
        foreach (var e in _entries.Values)
            if (e.Status == FeatureStatus.Available || e.Status == FeatureStatus.Planned)
                result.Add(e);
        return result;
    }

    private void SeedCatalog()
    {
        // ══ IA & Intelligence autonome ══
        Add("ia.speculative", "Speculative Decoding", "IA", FeatureStatus.AlreadyImplemented, "Vague 2 : SpeculativeDecoder");
        Add("ia.parallel_token", "Parallel Token Generation", "IA", FeatureStatus.AlreadyImplemented, "Vague 2 : parallel decoding");
        Add("ia.kv_compression", "KV-Cache Compression", "IA", FeatureStatus.AlreadyImplemented, "Vague 2 : FP16→INT8");
        Add("ia.layer_skipping", "Layer-Skipping Engine", "IA", FeatureStatus.AlreadyImplemented, "Vague 2 : auto-tier");
        Add("ia.adaptive_quant", "Adaptive Quantization", "IA", FeatureStatus.AlreadyImplemented, "Vague 2 : QuantizationSwitcher");
        Add("ia.context_expansion", "Context Window Expansion", "IA", FeatureStatus.Available, "Sliding window 4096→8192");
        Add("ia.semantic_cache", "Semantic Cache", "IA", FeatureStatus.Available, "Réutilise réponses identiques");
        Add("ia.prompt_rewriting", "Prompt Rewriting", "IA", FeatureStatus.Available, "Optimise prompts");
        Add("ia.code_tokenizer", "Code-Aware Tokenizer", "IA", FeatureStatus.Planned, "Tokenisation C#");
        Add("ia.auto_pruning", "Auto-Pruning", "IA", FeatureStatus.Planned, "Couches inutiles");
        Add("ia.distillation", "Mini-Model Distillation", "IA", FeatureStatus.AlreadyImplemented, "LocalModelDistillationService");
        Add("ia.fine_tuning", "Auto-Fine-Tuning local", "IA", FeatureStatus.Planned, "Sur code utilisateur");
        Add("ia.mmap_loading", "Memory-Mapped Loading", "IA", FeatureStatus.AlreadyImplemented, "Vague 2");
        Add("ia.gpu_warmup", "GPU Warm-Up", "IA", FeatureStatus.Available, "Prépare GPU");
        Add("ia.burst_mode", "Inference Burst Mode", "IA", FeatureStatus.Available, "Accélération temporaire");
        Add("ia.idle_training", "Idle-Time Training", "IA", FeatureStatus.Available, "Micro-apprentissage nocturne");
        Add("ia.context_cleaner", "Auto-Context Cleaner", "IA", FeatureStatus.Available, "Évite prompts longs");
        Add("ia.diff_generation", "Code-Diff-Aware Generation", "IA", FeatureStatus.Available, "Lignes modifiées uniquement");
        Add("ia.auto_retry", "Auto-Retry IA", "IA", FeatureStatus.Available, "Si réponse incohérente");
        Add("ia.self_diagnostics", "IA Self-Diagnostics", "IA", FeatureStatus.Available, "Détecte dégradation");

        // ══ Performance ══
        Add("perf.thread_affinity", "Thread Affinity Optimizer", "Perf", FeatureStatus.Available, "Fixe threads IA");
        Add("perf.numa", "NUMA-Aware Inference", "Perf", FeatureStatus.Planned, "Multi-CPU");
        Add("perf.zero_copy", "Zero-Copy Token Pipeline", "Perf", FeatureStatus.Planned, "");
        Add("perf.async_prefetch", "Async Prefetcher", "Perf", FeatureStatus.AlreadyImplemented, "AdaptivePrefetchService");
        Add("perf.batch_scheduler", "Adaptive Batch Scheduler", "Perf", FeatureStatus.Planned, "");
        Add("perf.heat_scaling", "Heat-Based Scaling", "Perf", FeatureStatus.AlreadyImplemented, "Vague 2 : auto-tier thermique");
        Add("perf.disk_kv", "Disk-Backed KV Cache", "Perf", FeatureStatus.Planned, "");
        Add("perf.gpu_governor", "GPU Memory Governor", "Perf", FeatureStatus.Available, "");
        Add("perf.cpu_monitor", "CPU Frequency Monitor", "Perf", FeatureStatus.Available, "");
        Add("perf.auto_suspend", "Auto-Suspend IA", "Perf", FeatureStatus.Available, "Si utilisateur tape vite");
        Add("perf.predictive_load", "Predictive Loading", "Perf", FeatureStatus.Available, "");
        Add("perf.idle_freeze", "Smart Idle Freeze", "Perf", FeatureStatus.Available, "");
        Add("perf.micro_workers", "Micro-Workers Pool", "Perf", FeatureStatus.AlreadyImplemented, "MicroWorkerPool");
        Add("perf.adaptive_polling", "Adaptive Polling", "Perf", FeatureStatus.AlreadyImplemented, "AdaptivePollingService");
        Add("perf.debouncer", "Inference Debouncer", "Perf", FeatureStatus.Available, "");
        Add("perf.token_profiler", "Token-Level Profiler", "Perf", FeatureStatus.Available, "");
        Add("perf.auto_unload", "Auto-Unload Layers", "Perf", FeatureStatus.Available, "");
        Add("perf.mem_tokenizer", "Memory-Pressure Tokenizer", "Perf", FeatureStatus.Planned, "");
        Add("perf.priority_queue", "Inference Priority Queue", "Perf", FeatureStatus.Available, "");
        Add("perf.hybrid_decoding", "GPU/CPU Hybrid Decoding", "Perf", FeatureStatus.Planned, "");

        // ══ Plugins (représentatifs) ══
        Add("plugin.refactor_live", "AI Refactor Live", "Plugin", FeatureStatus.Available, "");
        Add("plugin.bug_hunter", "AI Bug Hunter", "Plugin", FeatureStatus.Available, "");
        Add("plugin.test_gen_pro", "AI Unit Test Generator Pro", "Plugin", FeatureStatus.Available, "Complète TestSkeletonAgent");
        Add("plugin.doc_writer", "AI Documentation Writer", "Plugin", FeatureStatus.Available, "");
        Add("plugin.arch_advisor", "AI Architecture Advisor", "Plugin", FeatureStatus.Available, "");
        Add("plugin.naming_wizard", "AI Naming Wizard", "Plugin", FeatureStatus.Available, "");
        Add("plugin.pattern_detector", "AI Pattern Detector", "Plugin", FeatureStatus.AlreadyImplemented, "PatternDetectorEngine");
        Add("plugin.style_enforcer", "AI Code Style Enforcer", "Plugin", FeatureStatus.Available, "");
        Add("plugin.security_scanner", "AI Security Scanner", "Plugin", FeatureStatus.AlreadyImplemented, "SecurityHintAgent + PrivacyScannerAgent");
        Add("plugin.perf_analyzer", "AI Performance Analyzer", "Plugin", FeatureStatus.Available, "");
        Add("plugin.mem_leak", "AI Memory Leak Detector", "Plugin", FeatureStatus.Available, "");
        Add("plugin.git_commit", "AI Git Commit Writer", "Plugin", FeatureStatus.AlreadyImplemented, "CommitMessageAgent");
        Add("plugin.git_merge", "AI Git Merge Conflict Solver", "Plugin", FeatureStatus.Available, "");
        Add("plugin.regex_builder", "AI Regex Builder", "Plugin", FeatureStatus.Available, "");
        Add("plugin.sql_optimizer", "AI SQL Query Optimizer", "Plugin", FeatureStatus.Available, "");
        Add("plugin.shader_gen", "AI Shader Generator", "Plugin", FeatureStatus.Futuristic, "");
        Add("plugin.game_logic", "AI Game Logic Generator", "Plugin", FeatureStatus.Available, "");
        Add("plugin.ui_builder", "AI UI Builder", "Plugin", FeatureStatus.Futuristic, "");
        Add("plugin.api_client", "AI API Client Generator", "Plugin", FeatureStatus.Available, "");
        Add("plugin.cloud_advisor", "AI Cloud Deployment Advisor", "Plugin", FeatureStatus.Planned, "");

        // ══ IDE & UX (représentatifs) ══
        Add("ux.minimap_ai", "AI-Powered Minimap", "UX", FeatureStatus.Available, "");
        Add("ux.smart_tabs", "AI-Smart Tabs", "UX", FeatureStatus.Available, "");
        Add("ux.smart_search", "AI-Smart Search", "UX", FeatureStatus.Available, "");
        Add("ux.smart_explorer", "AI-Smart File Explorer", "UX", FeatureStatus.Available, "");
        Add("ux.smart_breakpoints", "AI-Smart Breakpoints", "UX", FeatureStatus.Planned, "");
        Add("ux.error_fixer", "AI-Smart Error Fixer", "UX", FeatureStatus.Available, "");
        Add("ux.smart_rename", "AI-Smart Rename", "UX", FeatureStatus.Available, "");
        Add("ux.code_folding", "AI-Smart Code Folding", "UX", FeatureStatus.Available, "");
        Add("ux.diff_viewer", "AI-Smart Diff Viewer", "UX", FeatureStatus.AlreadyImplemented, "InlineDiffPreviewService");
        Add("ux.merge_tool", "AI-Smart Merge Tool", "UX", FeatureStatus.Planned, "");
        Add("ux.project_overview", "AI-Smart Project Overview", "UX", FeatureStatus.Available, "");
        Add("ux.todo_manager", "AI-Smart TODO Manager", "UX", FeatureStatus.AlreadyImplemented, "SmartTodoAgent");
        Add("ux.snippet_library", "AI-Smart Snippet Library", "UX", FeatureStatus.Available, "");
        Add("ux.theme_generator", "AI-Smart Theme Generator", "UX", FeatureStatus.Futuristic, "");
        Add("ux.keybinding_opt", "AI-Smart Keybinding Optimizer", "UX", FeatureStatus.Planned, "");
        Add("ux.layout_manager", "AI-Smart Layout Manager", "UX", FeatureStatus.Available, "");
        Add("ux.notif_filter", "AI-Smart Notification Filter", "UX", FeatureStatus.Available, "");
        Add("ux.crash_predictor", "AI-Smart Crash Predictor", "UX", FeatureStatus.Planned, "");
        Add("ux.session_recorder", "AI-Smart Session Recorder", "UX", FeatureStatus.AlreadyImplemented, "SessionBookmarkService");
        Add("ux.learning_mode", "AI-Smart Learning Mode", "UX", FeatureStatus.AlreadyImplemented, "PedagogyEngine");

        // ══ Collaboration & CRDT (représentatifs) ══
        Add("crdt.multi_cursor", "AI-Smart Multi-Cursor Sync", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.conflict_resolver", "AI-Smart Conflict Resolver", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.compression", "AI-Smart CRDT Compression", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.prediction", "AI-Smart CRDT Prediction", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.undo_redo", "AI-Smart CRDT Undo/Redo", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.merge_preview", "AI-Smart CRDT Merge Preview", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.latency_opt", "AI-Smart CRDT Latency Optimizer", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.offline", "AI-Smart CRDT Offline Mode", "CRDT", FeatureStatus.Available, "");
        Add("crdt.snapshotting", "AI-Smart CRDT Snapshotting", "CRDT", FeatureStatus.AlreadyImplemented, "SnapshotResumeService");
        Add("crdt.history", "AI-Smart CRDT History Analyzer", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.summaries", "AI-Smart CRDT Session Summaries", "CRDT", FeatureStatus.Available, "");
        Add("crdt.ownership", "AI-Smart CRDT Code Ownership", "CRDT", FeatureStatus.Planned, "");
        Add("crdt.roles", "AI-Smart CRDT Role Manager", "CRDT", FeatureStatus.AlreadyImplemented, "CollabRoleService");
        Add("crdt.permissions", "AI-Smart CRDT Permissions", "CRDT", FeatureStatus.Available, "");
        Add("crdt.chat", "AI-Smart CRDT Chat Assistant", "CRDT", FeatureStatus.Available, "");
        Add("crdt.voice", "AI-Smart CRDT Voice Sync", "CRDT", FeatureStatus.Futuristic, "");
        Add("crdt.presence_map", "AI-Smart CRDT Presence Map", "CRDT", FeatureStatus.AlreadyImplemented, "CollabPresence");
        Add("crdt.heatmap", "AI-Smart CRDT Activity Heatmap", "CRDT", FeatureStatus.Available, "");
        Add("crdt.auto_review", "AI-Smart CRDT Auto-Review", "CRDT", FeatureStatus.AlreadyImplemented, "ReviewLaneService");
        Add("crdt.pair_programming", "AI-Smart CRDT Pair Programming", "CRDT", FeatureStatus.AlreadyImplemented, "PairProgrammingEngine + PairSessionTimerService");

        // ══ WOW (futuristes) ══
        Add("wow.ui_mockups", "AI-Generated UI Mockups", "WOW", FeatureStatus.Futuristic, "");
        Add("wow.game_levels", "AI-Generated Game Levels", "WOW", FeatureStatus.Available, "SnakeAssistantService");
        Add("wow.animations", "AI-Generated Animations", "WOW", FeatureStatus.Futuristic, "");
        Add("wow.sound_fx", "AI-Generated Sound Effects", "WOW", FeatureStatus.Futuristic, "");
        Add("wow.icons", "AI-Generated Icons", "WOW", FeatureStatus.Futuristic, "");
        Add("wow.doc_videos", "AI-Generated Documentation Videos", "WOW", FeatureStatus.Futuristic, "");
        Add("wow.tutorials", "AI-Generated Tutorials", "WOW", FeatureStatus.Available, "PedagogyEngine");
        Add("wow.code_viz", "AI-Generated Code Visualizations", "WOW", FeatureStatus.Futuristic, "");
        Add("wow.uml", "AI-Generated UML Diagrams", "WOW", FeatureStatus.Available, "");
        Add("wow.arch_maps", "AI-Generated Architecture Maps", "WOW", FeatureStatus.Available, "");
        Add("wow.api_blueprints", "AI-Generated API Blueprints", "WOW", FeatureStatus.Available, "");
        Add("wow.release_notes", "AI-Generated Release Notes", "WOW", FeatureStatus.Available, "ChangelogAgent");
        Add("wow.changelogs", "AI-Generated Changelogs", "WOW", FeatureStatus.AlreadyImplemented, "ChangelogAgent");
        Add("wow.benchmarks", "AI-Generated Benchmarks", "WOW", FeatureStatus.AlreadyImplemented, "AiOptimizationsBenchmark");
        Add("wow.perf_reports", "AI-Generated Performance Reports", "WOW", FeatureStatus.AlreadyImplemented, "PerfGateService");
        Add("wow.security_audits", "AI-Generated Security Audits", "WOW", FeatureStatus.AlreadyImplemented, "VulnerabilityScannerService");
        Add("wow.code_contracts", "AI-Generated Code Contracts", "WOW", FeatureStatus.Planned, "");
        Add("wow.test_coverage", "AI-Generated Test Coverage Maps", "WOW", FeatureStatus.Planned, "");
        Add("wow.dep_graphs", "AI-Generated Dependency Graphs", "WOW", FeatureStatus.Available, "");
        Add("wow.roadmaps", "AI-Generated Project Roadmaps", "WOW", FeatureStatus.Available, "");
    }

    private void Add(string id, string name, string category, FeatureStatus status, string notes = "")
    {
        _entries[id] = new FeatureEntry
        {
            Id = id,
            Name = name,
            Category = category,
            Status = status,
            Notes = notes
        };
    }
}
