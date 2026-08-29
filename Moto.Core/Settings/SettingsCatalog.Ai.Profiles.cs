namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class AiSettings
    {
        public AiProfilesSettings Profiles { get; } = new();

        public partial class AiProfilesSettings
        {
            // ══════════════ PÉDAGOGIE ══════════════
            public SettingItem<bool> PedagogyMode { get; } = new("ai.profiles.pedagogy", false, "Mode pédagogique actif");
            public SettingItem<bool> ExplainOnlyMode { get; } = new("ai.profiles.explainOnly", false, "Explique sans générer");
            public SettingItem<bool> AntiMagicMode { get; } = new("ai.profiles.antiMagic", true, "Explications obligatoires");
            public SettingItem<bool> InteractiveTutorial { get; } = new("ai.profiles.tutorial", false, "Tutoriel interactif");
            public SettingItem<bool> ProgressChecklist { get; } = new("ai.profiles.checklist", true, "Checklist de progression");

            // ══════════════ COMPORTEMENT ══════════════
            public SettingItem<bool> MinimalistMode { get; } = new("ai.profiles.minimalist", false, "Suggestions ultra-courtes");
            public SettingItem<bool> RefactorOnlyMode { get; } = new("ai.profiles.refactorOnly", false, "Refactor uniquement");
            public SettingItem<bool> SilentArchitectMode { get; } = new("ai.profiles.silentArchitect", false, "Diagrammes uniquement");
            public SettingItem<bool> StrictCSharpMode { get; } = new("ai.profiles.strictCSharp", false, "C# idiomatic strict");
            public SettingItem<bool> GameEngineFocus { get; } = new("ai.profiles.gameEngine", false, "Patterns moteur de jeu");
            public SettingItem<bool> NoExternalDeps { get; } = new("ai.profiles.noExternalDeps", true, "Pas de libs externes");
            public SettingItem<bool> DebuggingCoachMode { get; } = new("ai.profiles.debugCoach", false, "Coach debugging");

            // ══════════════ UX ══════════════
            public SettingItem<bool> ZenAiMode { get; } = new("ai.profiles.zenAi", false, "Interface épurée IA");
            public SettingItem<bool> NoSuggestionsOnlyAnswers { get; } = new("ai.profiles.noSuggestions", false, "Réponses uniquement");
            public SettingItem<bool> ShowCostEstimates { get; } = new("ai.profiles.showCost", true, "Afficher coûts estimés");
            public SettingItem<bool> TimelineEnabled { get; } = new("ai.profiles.timeline", true, "Timeline des décisions IA");

            // ══════════════ MODÈLES ══════════════
            public SettingItem<bool> Use3BByDefault { get; } = new("ai.profiles.use3B", true, "3B par défaut, 7B sur demande");
            public SettingItem<bool> LowRamMode { get; } = new("ai.profiles.lowRam", false, "Mode basse RAM");
            public SettingItem<int> RamThresholdGb { get; } = new("ai.profiles.ramThresholdGb", 16, "Seuil RAM pour 7B (Go)");
            public SettingItem<bool> SharedModelMode { get; } = new("ai.profiles.sharedModel", false, "Partage modèle multi-instance");
            public SettingItem<bool> NoGpuMode { get; } = new("ai.profiles.noGpu", false, "CPU uniquement");
            public SettingItem<bool> NightlyHeavyMode { get; } = new("ai.profiles.nightlyHeavy", true, "Tâches lourdes nocturnes");

            // ══════════════ COHÉRENCE ══════════════
            public SettingItem<bool> CoherenceContract { get; } = new("ai.profiles.coherenceContract", true, "Contrat de cohérence");
            public SettingItem<bool> DependencyAudit { get; } = new("ai.profiles.dependencyAudit", true, "Audit dépendances");
            public SettingItem<bool> ResponsibilityMap { get; } = new("ai.profiles.responsibilityMap", true, "Carte responsabilités");
            public SettingItem<bool> MagicCodeDetection { get; } = new("ai.profiles.magicCodeDetection", true, "Détection code magique");
            public SettingItem<bool> LogicalDuplicationControl { get; } = new("ai.profiles.logicalDuplication", true, "Contrôle duplication");
            public SettingItem<bool> ApiContractFirst { get; } = new("ai.profiles.apiContractFirst", false, "API contract first");
            public SettingItem<bool> ArchitectureJournal { get; } = new("ai.profiles.architectureJournal", true, "Journal décisions");

            // ══════════════ STYLE ══════════════
            public SettingItem<bool> StyleLearningLocal { get; } = new("ai.profiles.styleLearningLocal", true, "Apprentissage style local");
            public SettingItem<bool> ImitateOldCode { get; } = new("ai.profiles.imitateOldCode", false, "Imiter ancien code");
            public SettingItem<bool> StyleConsistencyScore { get; } = new("ai.profiles.styleConsistencyScore", true, "Score cohérence style");
            public SettingItem<bool> StyleDiffView { get; } = new("ai.profiles.styleDiff", true, "Vue diff style");
            public SettingItem<bool> StrictNoAutoFormat { get; } = new("ai.profiles.noAutoFormat", false, "Pas d'auto-format");
            public SettingItem<bool> StyleMentorMode { get; } = new("ai.profiles.styleMentor", true, "Mentor style");
        }
    }
}
