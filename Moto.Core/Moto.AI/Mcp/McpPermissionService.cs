using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Mcp;

public enum McpPermissionMode { Ask, Auto, Deny }

[Flags]
public enum McpCapability
{
    None        = 0,
    ReadFile    = 1 << 0,
    WriteFile   = 1 << 1,
    Network     = 1 << 2,
    Execute     = 1 << 3,
    UiAccess    = 1 << 4,
    Subagent    = 1 << 5
}

/// <summary>
/// Item 91 — Managed policy MCP (équivalent CLAUDE.md).
/// Fichier JSON déclarant les permissions par tool/serveur.
/// </summary>
public sealed class McpPolicy
{
    public string DefaultMode { get; set; } = "ask";
    public Dictionary<string, string> ToolPermissions { get; set; } = new(); // tool -> allow/deny/ask
    public List<string> AllowedServers { get; set; } = new();
    public List<string> DeniedTools { get; set; } = new();
}

/// <summary>
/// Item 91 — Service de permissions MCP avancé.
/// Évalue les permissions selon mode global + managed policy + capabilities.
/// </summary>
public sealed class McpPermissionService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private McpPolicy _policy = new();

    public event Func<string, McpCapability, bool>? PermissionPrompt; // hook UI pour mode "ask"

    public McpPermissionService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        LoadPolicy();
    }

    private void LoadPolicy()
    {
        string path = SettingsCatalog.McpAdvanced.ManagedPolicyPath.Value;
        if (!File.Exists(path)) return;
        try
        {
            _policy = JsonSerializer.Deserialize<McpPolicy>(File.ReadAllText(path)) ?? new McpPolicy();
            _log.Info("McpPermission", "Managed policy chargée", new { path });
        }
        catch (Exception ex)
        {
            _log.Error("McpPermission", "Échec chargement policy", new { ex.Message });
        }
    }

    public McpPermissionMode GetMode()
    {
        return SettingsCatalog.McpAdvanced.PermissionMode.Value.ToLowerInvariant() switch
        {
            "auto" => McpPermissionMode.Auto,
            "deny" => McpPermissionMode.Deny,
            _ => McpPermissionMode.Ask
        };
    }

    /// <summary>Évalue si un tool peut s'exécuter avec les capabilities demandées.</summary>
    public bool IsAllowed(string toolName, McpCapability requested)
    {
        // 1. Tool explicitement dénié par la policy
        if (_policy.DeniedTools.Contains(toolName))
        {
            _log.Warning("McpPermission", "Tool dénié par policy", new { toolName });
            return false;
        }

        // 2. Mode global
        var mode = GetMode();
        if (mode == McpPermissionMode.Deny) return false;
        if (mode == McpPermissionMode.Auto) return true;

        // 3. Mode Ask : permission par tool dans la policy, sinon prompt
        if (_policy.ToolPermissions.TryGetValue(toolName, out var perm))
        {
            if (perm == "allow") return true;
            if (perm == "deny") return false;
        }

        // 4. Prompt utilisateur (délégué à l'UI)
        return PermissionPrompt?.Invoke(toolName, requested) ?? false;
    }
}
