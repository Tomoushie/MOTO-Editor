using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Internal;   // MotoAiKernel
using Moto.Core.Settings;

namespace Moto.Core.AI.Agents;

/// <summary>
/// Base commune des agents adossés au LLM local. Respecte l'architecture :
/// la génération passe par MotoAiKernel.RouteAsync (Ollama → Embarqué → Fallback).
/// </summary>
public abstract class LlmBackedAgent : ISpecializedAgent
{
    protected MotoAiKernel Kernel { get; }
    protected ExplainabilityLogger Explain { get; }
    protected SettingsEngine Settings { get; }

    protected LlmBackedAgent(MotoAiKernel kernel, ExplainabilityLogger explain, SettingsEngine settings)
    {
        Kernel = kernel;
        Explain = explain;
        Settings = settings;
    }

    public abstract AgentDescriptor Descriptor { get; }
    protected abstract string BuildPrompt(SpecializedAgentRequest request);

    public virtual async Task<SpecializedAgentResult> ExecuteAsync(
        SpecializedAgentRequest request, CancellationToken ct = default)
    {
        if (!Settings.Shared.AiAgents.AgentsEnabled.Value)
            return SpecializedAgentResult.Fail("Agents désactivés par l'utilisateur.");

        try
        {
            string prompt = BuildPrompt(request);
            // NOTE : adaptez si RouteAsync retourne un objet riche (extraire .Text).
            string output = await Kernel.RouteAsync(prompt, ct);
            Explain.LogDecision(Descriptor.Id, request, prompt, output);
            return PostProcess(output, request);
        }
        catch (Exception ex)
        {
            return SpecializedAgentResult.Fail(ex.Message);
        }
    }

    protected virtual SpecializedAgentResult PostProcess(string output, SpecializedAgentRequest request)
        => SpecializedAgentResult.Ok(output);
}

/// <summary>P1 — Micro-agent de tests : squelettes de tests unitaires pour une fonction.</summary>
public sealed class TestSkeletonAgent : LlmBackedAgent
{
    public TestSkeletonAgent(MotoAiKernel k, ExplainabilityLogger e, SettingsEngine s) : base(k, e, s) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.tests.micro", Name = "Micro-agent de tests",
        Priority = AgentPriority.P1, Impact = "high", RequiresLlm = true, EstimatedCpuCost = 0.5
    };
    protected override string BuildPrompt(SpecializedAgentRequest r)
        => $"Génère un squelette de tests unitaires (xUnit) pour cette fonction. Retourne uniquement le code.\n\n```\n{r.CodeSnippet}\n```";
}

/// <summary>P1 — Explain-change : explique en langage clair pourquoi un refactor est suggéré.</summary>
public sealed class ExplainChangeAgent : LlmBackedAgent
{
    public ExplainChangeAgent(MotoAiKernel k, ExplainabilityLogger e, SettingsEngine s) : base(k, e, s) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.explain.change", Name = "Explain-change",
        Priority = AgentPriority.P1, Impact = "high", RequiresLlm = true, EstimatedCpuCost = 0.4
    };
    protected override string BuildPrompt(SpecializedAgentRequest r)
        => $"Explique en français simple et concise pourquoi ce changement est suggéré :\n\n{r.Diff}";
}

/// <summary>P1 — Commit message contextuel à partir d'un diff.</summary>
public sealed class CommitMessageAgent : LlmBackedAgent
{
    public CommitMessageAgent(MotoAiKernel k, ExplainabilityLogger e, SettingsEngine s) : base(k, e, s) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.commit.message", Name = "Aide message de commit",
        Priority = AgentPriority.P1, Impact = "medium", RequiresLlm = true, EstimatedCpuCost = 0.2
    };
    protected override string BuildPrompt(SpecializedAgentRequest r)
        => $"Propose un message de commit concis (format conventionnel) pour ce diff :\n\n{r.Diff}";
}

/// <summary>P1 — Résumeur de recherche : synthèse des résultats à travers le projet.</summary>
public sealed class SearchSummarizerAgent : LlmBackedAgent
{
    public SearchSummarizerAgent(MotoAiKernel k, ExplainabilityLogger e, SettingsEngine s) : base(k, e, s) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.search.summarizer", Name = "Résumeur de recherche",
        Priority = AgentPriority.P1, Impact = "medium", RequiresLlm = true, EstimatedCpuCost = 0.3
    };
    protected override string BuildPrompt(SpecializedAgentRequest r)
        => $"Résume ces résultats de recherche projet pour donner un contexte rapide :\n\n{r.Query}\n\n{r.CodeSnippet}";
}

/// <summary>P2 — Générateur de changelog à partir de diffs/PR.</summary>
public sealed class ChangelogAgent : LlmBackedAgent
{
    public ChangelogAgent(MotoAiKernel k, ExplainabilityLogger e, SettingsEngine s) : base(k, e, s) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.changelog", Name = "Générateur de changelog",
        Priority = AgentPriority.P2, Impact = "medium", RequiresLlm = true, EstimatedCpuCost = 0.3
    };
    protected override string BuildPrompt(SpecializedAgentRequest r)
        => $"Génère des entrées de changelog (Added/Changed/Fixed) à partir de ces diffs :\n\n{r.Diff}";
}

/// <summary>P1 — Générateur de snippets contextuels adaptés au style du projet.</summary>
public sealed class SnippetGeneratorAgent : LlmBackedAgent
{
    public SnippetGeneratorAgent(MotoAiKernel k, ExplainabilityLogger e, SettingsEngine s) : base(k, e, s) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.snippet.generator", Name = "Générateur de snippets",
        Priority = AgentPriority.P1, Impact = "medium", RequiresLlm = true, EstimatedCpuCost = 0.4
    };
    protected override string BuildPrompt(SpecializedAgentRequest r)
        => $"Génère 2 variantes de snippet adaptées au style du projet pour : {r.Query}\n\nContexte :\n{r.CodeSnippet}";
}
