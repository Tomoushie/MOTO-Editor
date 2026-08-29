using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Ux;

public sealed class AiTimelineEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public double EstimatedCostMs { get; set; }
}

public sealed class AiCostEstimate
{
    public double RamMb { get; set; }
    public double TimeMs { get; set; }
    public string Model { get; set; } = "";
}

/// <summary>
/// Bloc 4 — UX MOTO Editor + IA (6 idées).
/// </summary>
public sealed class AiUxService
{
    private static readonly string TimelinePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "ai-timeline.json");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<AiTimelineEntry> _timeline = new();

    public event Action<AiTimelineEntry>? TimelineUpdated;

    public AiUxService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        LoadTimeline();
    }

    /// <summary>Idée "Timeline IA" — trace des interventions.</summary>
    public void RecordAction(string action, string details, double costMs = 0)
    {
        if (!_settings.Shared.Ai.Profiles.TimelineEnabled.Value)
            return;

        var entry = new AiTimelineEntry
        {
            Action = action,
            Details = details,
            EstimatedCostMs = costMs
        };

        _timeline.Add(entry);
        if (_timeline.Count > 100) _timeline.RemoveAt(0); // Garde 100 dernières
        SaveTimeline();
        TimelineUpdated?.Invoke(entry);
    }

    public IReadOnlyList<AiTimelineEntry> GetTimeline() => _timeline;

    /// <summary>Idée "Zen IA" — mode épuré.</summary>
    public bool IsZenModeActive() => _settings.Shared.Ai.Profiles.ZenAiMode.Value;

    /// <summary>Idée "No suggestions, only answers" — désactive suggestions proactives.</summary>
    public bool AllowsProactiveSuggestions()
    {
        return !_settings.Shared.Ai.Profiles.NoSuggestionsOnlyAnswers.Value;
    }

    /// <summary>Idée "Affichage coûts estimés" — calcule le coût d'une action.</summary>
    public AiCostEstimate EstimateCost(string actionType, int inputSize)
    {
        if (!_settings.Shared.Ai.Profiles.ShowCostEstimates.Value)
            return new AiCostEstimate();

        // Estimation heuristique
        double ramMb = actionType switch
        {
            "completion" => 50 + inputSize * 0.01,
            "refactor" => 100 + inputSize * 0.02,
            "generate" => 150 + inputSize * 0.03,
            _ => 75
        };

        double timeMs = actionType switch
        {
            "completion" => 200 + inputSize * 0.5,
            "refactor" => 500 + inputSize * 1.0,
            "generate" => 800 + inputSize * 1.5,
            _ => 300
        };

        return new AiCostEstimate
        {
            RamMb = ramMb,
            TimeMs = timeMs,
            Model = _settings.Shared.Ai.Profiles.Use3BByDefault.Value ? "3B" : "7B"
        };
    }

    /// <summary>Idée "Palette IA contextuelle" — commandes dédiées.</summary>
    public IReadOnlyList<string> GetContextualCommands(string context)
    {
        var commands = new List<string>
        {
            "Expliquer ce fichier",
            "Proposer un meilleur nom",
            "Simplifier cette méthode"
        };

        if (context.Contains("error") || context.Contains("exception"))
            commands.Add("Diagnostiquer cette erreur");

        if (context.Contains("test"))
            commands.Add("Générer des tests");

        return commands;
    }

    /// <summary>Idée "Mini-console IA" — commande rapide.</summary>
    public string ExecuteQuickCommand(string command)
    {
        RecordAction("quick_command", command);
        return $"Commande '{command}' exécutée (simulation)";
    }

    private void LoadTimeline()
    {
        if (!File.Exists(TimelinePath)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<AiTimelineEntry>>(File.ReadAllText(TimelinePath));
            if (loaded != null) _timeline.AddRange(loaded);
        }
        catch { /* Timeline corrompue */ }
    }

    private void SaveTimeline()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TimelinePath)!);
            File.WriteAllText(TimelinePath, JsonSerializer.Serialize(_timeline));
        }
        catch (Exception ex)
        {
            _log.Error("AiUx", "Échec sauvegarde timeline", new { ex.Message });
        }
    }
}
