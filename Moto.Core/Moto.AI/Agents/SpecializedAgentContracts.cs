using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Agents;

public enum AgentPriority { P1 = 1, P2 = 2, P3 = 3 }

/// <summary>Métadonnées d'un agent (utilisées par le cost estimator et le marketplace).</summary>
public sealed class AgentDescriptor
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public AgentPriority Priority { get; init; } = AgentPriority.P2;
    public string Impact { get; init; } = "medium";      // low/medium/high
    public double EstimatedCpuCost { get; init; } = 0.3; // 0..1, pour le cost estimator
    public bool RequiresLlm { get; init; }
}

/// <summary>Requête générique adressée à un agent.</summary>
public sealed class SpecializedAgentRequest
{
    public string? FilePath { get; init; }
    public string? CodeSnippet { get; init; }
    public string? Diff { get; init; }
    public string? Query { get; init; }
    public IReadOnlyDictionary<string, string> Context { get; init; }
        = new Dictionary<string, string>();
}

/// <summary>Résultat générique d'un agent (texte + findings structurés).</summary>
public sealed class SpecializedAgentResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = "";
    public string? Error { get; init; }
    public IReadOnlyList<AgentFinding> Findings { get; init; } = Array.Empty<AgentFinding>();

    public static SpecializedAgentResult Ok(string output)
        => new() { Success = true, Output = output };
    public static SpecializedAgentResult Fail(string error)
        => new() { Success = false, Error = error };
    public static SpecializedAgentResult WithFindings(IReadOnlyList<AgentFinding> findings)
        => new() { Success = true, Findings = findings };
}

/// <summary>Constat structuré (sécurité, privacy, dépendance, format…).</summary>
public sealed class AgentFinding
{
    public string Severity { get; init; } = "info"; // info / warning / critical
    public string FilePath { get; init; } = "";
    public int Line { get; init; }
    public string Message { get; init; } = "";
    public string? Suggestion { get; init; }
}

/// <summary>Contrat commun à tous les agents spécialisés (assistants, jamais structurants).</summary>
public interface ISpecializedAgent
{
    AgentDescriptor Descriptor { get; }
    Task<SpecializedAgentResult> ExecuteAsync(SpecializedAgentRequest request, CancellationToken ct = default);
}
