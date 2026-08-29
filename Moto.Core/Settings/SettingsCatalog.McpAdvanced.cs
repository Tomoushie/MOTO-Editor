namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public McpAdvancedSettings McpAdvanced { get; } = new();

    public partial class McpAdvancedSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<string> PermissionMode { get; } = new("mcp.adv.permissionMode", "ask", "Mode permission (ask/auto/deny)");
        public SettingItem<string> ManagedPolicyPath { get; } = new("mcp.adv.managedPolicyPath", ".moto/mcp-policy.json", "Chemin managed policy (type CLAUDE.md)");
        public SettingItem<bool> HooksEnabled { get; } = new("mcp.adv.hooksEnabled", true, "Active les hooks pre/post tool");
        public SettingItem<bool> CheckpointingEnabled { get; } = new("mcp.adv.checkpointing", true, "Checkpointing des sessions MCP");
        public SettingItem<int> MaxToolCallsPerTurn { get; } = new("mcp.adv.maxToolCallsPerTurn", 20, "Limite d'appels tools par tour");

        // Ajout d'un nouveau paramètre pour la langue par défaut
        public SettingItem<string> DefaultLanguage { get; } = new("mcp.adv.defaultLanguage", "fr", "Langue par défaut pour l'interface utilisateur");
    }
}

public partial class SettingsCatalog
{
    public McpAdvancedSettings McpAdvanced { get; } = new();

    public partial class McpAdvancedSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<string> PermissionMode { get; } = new("mcp.adv.permissionMode", "ask", "Mode permission (ask/auto/deny)");
        public SettingItem<string> ManagedPolicyPath { get; } = new("mcp.adv.managedPolicyPath", ".moto/mcp-policy.json", "Chemin managed policy (type CLAUDE.md)");
        public SettingItem<bool> HooksEnabled { get; } = new("mcp.adv.hooksEnabled", true, "Active les hooks pre/post tool");
        public SettingItem<bool> CheckpointingEnabled { get; } = new("mcp.adv.checkpointing", true, "Checkpointing des sessions MCP");
        public SettingItem<int> MaxToolCallsPerTurn { get; } = new("mcp.adv.maxToolCallsPerTurn", 20, "Limite d'appels tools par tour");
    }
}
