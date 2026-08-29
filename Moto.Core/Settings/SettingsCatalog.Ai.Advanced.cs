namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class AiSettings
    {
        public partial class AdvancedSettings
        {
            // 1 ligne = 1 paramètre (Règle d'or SettingsCatalog)
            public SettingItem<bool> SpeculativeActivationEnabled { get; } = new("ai.speculative.enabled", true, "Décodage spéculatif (logits)");
            public SettingItem<int> CircuitBreakerThreshold { get; } = new("ai.circuit.threshold", 3, "Erreurs avant ouverture circuit");
            public SettingItem<int> MaxConcurrentPrefetch { get; } = new("ai.prefetch.maxConcurrent", 3, "Slots de prefetch simultanés (Backpressure)");
            public SettingItem<bool> PerformanceMaxMode { get; } = new("ai.perf.maxMode", false, "Mode performance maximale (désactive éco)");
        }
    }
}
