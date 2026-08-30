namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public static McpSettings Mcp { get; } = new();

    public partial class McpSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<bool> McpEnabled { get; } = new("mcp.enabled", true, "Active le Model Context Protocol");
        public SettingItem<string> McpServersPath { get; } = new("mcp.serversPath", ".moto/mcp-servers.json", "Chemin config serveurs MCP");
        public SettingItem<bool> SubagentsEnabled { get; } = new("mcp.subagents", true, "Active les subagents");
        public SettingItem<int> MaxSubagentDepth { get; } = new("mcp.maxSubagentDepth", 3, "Profondeur max subagents");
        public SettingItem<bool> PromptInjectionProtection { get; } = new("mcp.promptInjectionProtection", true, "Protection injection prompt");
        public SettingItem<string> PermissionMode { get; } = new("mcp.permissionMode", "ask", "Mode permission (ask/auto/deny)");
    }
}
