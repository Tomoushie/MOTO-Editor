namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public static AiAgentsSettings AiAgents { get; } = new();

    public partial class AiAgentsSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<bool> AgentsEnabled { get; } = new("ai.agents.enabled", true, "Active les agents spécialisés");
        public SettingItem<bool> ExplainabilityEnabled { get; } = new("ai.agents.explainability", true, "Journalise les décisions des agents (audit)");
        public SettingItem<bool> LocalRlEnabled { get; } = new("ai.agents.localRl", true, "Boucle de feedback RL locale (sans cloud)");
        public SettingItem<int> SandboxTimeoutSeconds { get; } = new("ai.agents.sandboxTimeoutSec", 30, "Timeout du sandbox LLM local");
    }
}
