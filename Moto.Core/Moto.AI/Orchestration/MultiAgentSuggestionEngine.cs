using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;

namespace Moto.Core.AI.Orchestration;

/// <summary>
/// Orchestrateur multi-agents pour suggestions IA :
/// combine Cortex (mémoire+style), Neural (embeddings), Workspace (proactif).
/// </summary>
public sealed class MultiAgentSuggestionEngine
{
    private readonly CortexEngine _cortex;
    private readonly NeuralMode _neural;
    private readonly AIWorkspace _workspace;

    public MultiAgentSuggestionEngine(CortexEngine cortex, NeuralMode neural, AIWorkspace workspace)
    {
        _cortex = cortex;
        _neural = neural;
        _workspace = workspace;
    }

    /// <summary>
    /// Génère une suggestion en combinant les 3 agents.
    /// </summary>
    public async Task<UnifiedSuggestion> GenerateAsync(SuggestionContext context)
    {
        // 1. Cortex : récupère le style + mémoire utilisateur
        var cortexMemory = await _cortex.RecallAsync(context.ProjectPath, context.Intent);

        // 2. Neural : cherche les patterns similaires dans l'historique
        var similarCases = await _neural.SearchAsync(context.CodeSnippet, context.ProjectFiles, topK: 3);

        // 3. Workspace : détermine l'action proactive la plus pertinente
        var proactiveAction = await _workspace.SuggestProactiveActionAsync(context);

        // 4. Fusion des 3 signaux
        return new UnifiedSuggestion
        {
            Id = Guid.NewGuid().ToString("N"),
            Description = BuildDescription(cortexMemory, similarCases, proactiveAction),
            ConfidenceScore = ComputeConfidence(cortexMemory, similarCases, proactiveAction),
            Source = SuggestionSource.MultiAgent,
            CortexInsight = cortexMemory,
            NeuralMatches = similarCases,
            ProactiveAction = proactiveAction,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string BuildDescription(CortexMemory memory, List<(string doc, float score)> neural, ProactiveAction action)
    {
        var parts = new List<string>();

        if (memory.LastStyle != null)
            parts.Add($"Style détecté : {memory.LastStyle}");

        if (neural.Any())
            parts.Add($"{neural.Count} pattern{((neural.Count > 1) ? "s" : "")} similaire{((neural.Count > 1) ? "s" : "")} trouvé{((neural.Count > 1) ? "s" : "")}");

        if (action != null)
            parts.Add($"Action proactive : {action.Name}");

        return string.Join(" · ", parts);
    }

    private static double ComputeConfidence(CortexMemory memory, List<(string doc, float score)> neural, ProactiveAction action)
    {
        double score = 0.3; // base
        if (memory.LastStyle != null) score += 0.2;
        if (neural.Any()) score += neural.Average(n => n.score) * 0.3;
        if (action != null) score += 0.2;
        return Math.Clamp(score, 0, 1);
    }
}

public class SuggestionContext
{
    public string ProjectPath { get; set; } = "";
    public string Intent { get; set; } = "";
    public string CodeSnippet { get; set; } = "";
    public List<string> ProjectFiles { get; set; } = new();
}

public class UnifiedSuggestion
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public double ConfidenceScore { get; set; }
    public SuggestionSource Source { get; set; }
    public CortexMemory? CortexInsight { get; set; }
    public List<(string doc, float score)> NeuralMatches { get; set; } = new();
    public ProactiveAction? ProactiveAction { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum SuggestionSource
{
    Cortex,
    Neural,
    Workspace,
    MultiAgent
}

public class ProactiveAction
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double Priority { get; set; }
}
