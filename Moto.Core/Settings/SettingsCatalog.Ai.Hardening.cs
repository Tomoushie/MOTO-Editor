namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class AiSettings
    {
        public partial class AdvancedSettings
        {
            // 1 ligne = 1 paramètre
            public SettingItem<bool> TelemetryEnabled { get; } = new("ai.telemetry.enabled", false, "Télémétrie privacy-safe (opt-in)");
            public SettingItem<bool> AdaptivePrefetchEnabled { get; } = new("ai.prefetch.adaptive", true, "Préfetch adaptatif avec backpressure");
            public SettingItem<int> SpeculativeLogitsTopK { get; } = new("ai.speculative.logitsTopK", 5, "Top-K pour vérification logits réels");
            public SettingItem<double> SpeculativeAcceptThreshold { get; } = new("ai.speculative.acceptThreshold", 0.6, "Seuil d'acceptation spéculative");
        }
    }
}
