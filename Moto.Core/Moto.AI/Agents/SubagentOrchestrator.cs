using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Agents;

/// <summary>
/// Item 84 — Orchestrateur de subagents avec profondeur limitée.
/// </summary>
public sealed class SubagentOrchestrator
{
    private readonly SpecializedAgentRegistry _registry;
    private readonly StructuredLogCollector _log;
    private readonly SettingsEngine _settings;

    public SubagentOrchestrator(SpecializedAgentRegistry registry, StructuredLogCollector log, SettingsEngine settings)
    {
        _registry = registry;
        _log = log;
        _settings = settings;
    }

    public async Task<SpecializedAgentResult> ExecuteWithSubagentsAsync(
        string rootAgentId,
        SpecializedAgentRequest request,
        int currentDepth = 0,
        CancellationToken ct = default)
    {
        if (!_settings.Shared.Mcp.SubagentsEnabled.Value)
            return await _registry.DispatchAsync(rootAgentId, request, ct);

        int maxDepth = _settings.Shared.Mcp.MaxSubagentDepth.Value;
        if (currentDepth > maxDepth)
        {
            _log.Warning("SubagentOrchestrator", "Profondeur max atteinte", new { currentDepth, maxDepth });
            return SpecializedAgentResult.Fail("Profondeur subagent max atteinte");
        }

        var result = await _registry.DispatchAsync(rootAgentId, request, ct);
        // Logique de délégation aux subagents basée sur le résultat
        // (simplifiée ici : en production, parser la sortie pour détecter les appels subagent)
        return result;
    }
}
