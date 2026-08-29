using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Mcp;

public enum McpHookPhase { PreTool, PostTool, OnError }

/// <summary>
/// Item 91 — Hooks MCP pre/post tool execution.
/// </summary>
public sealed class McpHookService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Dictionary<McpHookPhase, List<Func<string, Task>>> _hooks = new();

    public McpHookService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        foreach (McpHookPhase phase in Enum.GetValues<McpHookPhase>())
            _hooks[phase] = new List<Func<string, Task>>();
    }

    public void Register(McpHookPhase phase, Func<string, Task> hook)
    {
        _hooks[phase].Add(hook);
    }

    public async Task RunHooksAsync(McpHookPhase phase, string toolName)
    {
        if (!_settings.Shared.McpAdvanced.HooksEnabled.Value) return;
        foreach (var hook in _hooks[phase])
        {
            try { await hook(toolName); }
            catch (Exception ex)
            {
                _log.Error("McpHook", $"Hook {phase} échoué", new { toolName, ex.Message });
            }
        }
    }
}
