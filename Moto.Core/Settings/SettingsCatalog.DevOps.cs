namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public DevOpsSettings DevOps { get; } = new();

    public partial class DevOpsSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<bool> PerfGateEnabled { get; } = new("devops.perfgate.enabled", true, "Active la CI perf gate");
        public SettingItem<double> StartupTimeThresholdMs { get; } = new("devops.perfgate.startupMs", 2000, "Seuil temps de démarrage (ms)");
        public SettingItem<double> MemoryThresholdMb { get; } = new("devops.perfgate.memoryMb", 512, "Seuil mémoire (Mo)");
        public SettingItem<bool> SyntheticJourneysEnabled { get; } = new("devops.journeys.enabled", true, "Active les user journeys synthétiques");
        public SettingItem<bool> PluginFuzzingEnabled { get; } = new("devops.fuzzing.enabled", true, "Active le fuzzing plugins");
        public SettingItem<bool> CrashTriageEnabled { get; } = new("devops.crashtriage.enabled", true, "Active le triage automatique de crashes");
        public SettingItem<bool> FeatureFlagsEnabled { get; } = new("devops.featureflags.enabled", true, "Active les feature flags");
        public SettingItem<bool> TelemetryPrivacySandbox { get; } = new("devops.telemetry.privacySandbox", true, "Sandbox télémétrie privacy");
        public SettingItem<double> PerfRegressionThresholdPercent { get; } = new("devops.perfalert.thresholdPercent", 15, "Seuil alerte régression perf (%)");
    }
}
