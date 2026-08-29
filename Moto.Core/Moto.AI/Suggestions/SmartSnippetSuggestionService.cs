// Moto.Core/AI/Suggestions/SmartSnippetSuggestionService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Agents;
using Moto.Core.AI.Cortex;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Suggestions;

public sealed class SmartSnippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Code { get; set; } = "";
    public double Score { get; set; }
    public string SourceAgentId { get; set; } = "";
}

/// <summary>
/// Item 104 — Smart snippets suggestions.
/// Combine CortexEngine (style appris) + LocalRlFeedbackLoop (tri adaptatif).
/// MOTO AI propose ; l'utilisateur applique (règle d'architecture).
/// </summary>
public sealed class SmartSnippetSuggestionService
{
    private readonly CortexEngine _cortex;
    private readonly LocalRlFeedbackLoop _rlLoop;
    private readonly StructuredLogCollector _log;
    private readonly SettingsEngine _settings;

    public event Action<IReadOnlyList<SmartSnippet>>? SnippetsReady;

    public SmartSnippetSuggestionService(CortexEngine cortex,
                                         LocalRlFeedbackLoop rlLoop,
                                         StructuredLogCollector log,
                                         SettingsEngine settings)
    {
        _cortex = cortex;
        _rlLoop = rlLoop;
        _log = log;
        _settings = settings;
    }

    /// <summary>Génère des snippets adaptés au contexte courant.</summary>
    public IReadOnlyList<SmartSnippet> Suggest(string filePath, string currentLine)
    {
        var snippets = new List<SmartSnippet>();
        try
        {
            // 1. Récupère les suggestions du style appris (Cortex)
            var cortexSuggestions = _cortex.GetSuggestions(filePath, currentLine);

            foreach (var s in cortexSuggestions.Take(5))
            {
                // 2. Boost adaptatif via RL local (feedback utilisateur)
                double boost = _rlLoop.GetRankingBoost(s.Kind.ToString());
                snippets.Add(new SmartSnippet
                {
                    Title = s.Title,
                    Code = s.GeneratedContent ?? "",
                    Score = s.Confidence + boost,
                    SourceAgentId = "cortex"
                });
            }

            // 3. Tri par score décroissant
            snippets = snippets.OrderByDescending(x => x.Score).ToList();
            SnippetsReady?.Invoke(snippets);
            _log.Debug("SmartSnippet", "Suggestions générées", new { count = snippets.Count });
        }
        catch (Exception ex)
        {
            _log.Error("SmartSnippet", "Échec génération", new { ex.Message });
        }
        return snippets;
    }

    /// <summary>Feedback utilisateur : alimente la boucle RL locale.</summary>
    public void RecordAcceptance(SmartSnippet snippet, bool accepted)
    {
        _rlLoop.RecordFeedback(snippet.SourceAgentId, accepted);
    }
}
