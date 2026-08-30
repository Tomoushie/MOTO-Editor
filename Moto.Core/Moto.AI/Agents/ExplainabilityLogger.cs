using System;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Agents;

/// <summary>
/// Idée "Explainability logs" (P1) — journalise POURQUOI un agent a décidé,
/// pour audit et apprentissage. Stocké dans .moto/agents/decisions/.
/// </summary>
public sealed class ExplainabilityLogger
{
    private static readonly string DecisionDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "agents", "decisions");

    private readonly StructuredLogCollector _log;
    private readonly SettingsEngine _settings;

    public ExplainabilityLogger(StructuredLogCollector log, SettingsEngine settings)
    {
        _log = log;
        _settings = settings;
        Directory.CreateDirectory(DecisionDir);
    }

    public void LogDecision(string agentId, SpecializedAgentRequest request,
                            string rationale, string output)
    {
        if (!SettingsCatalog.AiAgents.ExplainabilityEnabled.Value) return;

        var record = new
        {
            timestamp = DateTime.UtcNow,
            agentId,
            rationale,
            input = new { request.FilePath, request.Query, hasDiff = request.Diff is not null },
            outputPreview = output.Length > 500 ? output[..500] : output
        };

        try
        {
            var file = Path.Combine(DecisionDir, $"{DateTime.Now:yyyyMMdd}.jsonl");
            File.AppendAllText(file, JsonSerializer.Serialize(record) + Environment.NewLine);
            _log.Debug("Explainability", $"Décision agent {agentId} journalisée");
        }
        catch (Exception ex)
        {
            _log.Error("Explainability", "Échec journalisation", new { ex.Message });
        }
    }
}
