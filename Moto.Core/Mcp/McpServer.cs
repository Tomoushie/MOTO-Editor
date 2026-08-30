using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Mcp;

public sealed class McpTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public sealed class McpServerConfig
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public List<string> Args { get; set; } = new();
    public Dictionary<string, string> Env { get; set; } = new();
    public List<McpTool> Tools { get; set; } = new();
}

/// <summary>
/// Item 84 — Serveur MCP (Model Context Protocol) pour tools/hooks/skills.
/// Configuration JSON dans .moto/mcp-servers.json.
/// </summary>
public sealed class McpServerManager
{
    private readonly StructuredLogCollector _log;
    private readonly SettingsEngine _settings;
    private readonly List<McpServerConfig> _servers = new();

    public McpServerManager(StructuredLogCollector log, SettingsEngine settings)
    {
        _log = log;
        _settings = settings;
        LoadServers();
    }

    private void LoadServers()
    {
        if (!SettingsCatalog.Mcp.McpEnabled.Value) return;
        string path = SettingsCatalog.Mcp.McpServersPath.Value;
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var servers = JsonSerializer.Deserialize<List<McpServerConfig>>(json);
            if (servers != null) _servers.AddRange(servers);
            _log.Info("McpServer", "Serveurs MCP chargés", new { count = _servers.Count });
        }
        catch (Exception ex)
        {
            _log.Error("McpServer", "Échec chargement MCP", new { ex.Message });
        }
    }

    public IReadOnlyList<McpServerConfig> GetServers() => _servers;

    public IReadOnlyList<McpTool> GetAllTools()
    {
        var tools = new List<McpTool>();
        foreach (var server in _servers)
            tools.AddRange(server.Tools);
        return tools;
    }
}
