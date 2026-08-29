using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Settings;

namespace Moto.Core.AI.Agents;

/// <summary>Base des agents à heuristique légère (pattern matching, PAS de parsing profond).</summary>
public abstract class HeuristicAgent : ISpecializedAgent
{
    protected SettingsEngine Settings { get; }
    protected ExplainabilityLogger Explain { get; }
    protected HeuristicAgent(SettingsEngine s, ExplainabilityLogger e) { Settings = s; Explain = e; }

    public abstract AgentDescriptor Descriptor { get; }
    protected abstract IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request);

    public Task<SpecializedAgentResult> ExecuteAsync(SpecializedAgentRequest request, CancellationToken ct = default)
    {
        if (!Settings.Shared.AiAgents.AgentsEnabled.Value)
            return Task.FromResult(SpecializedAgentResult.Fail("Agents désactivés."));

        var findings = Analyze(request);
        Explain.LogDecision(Descriptor.Id, request, "heuristique", $"{findings.Count} constats");
        return Task.FromResult(SpecializedAgentResult.WithFindings(findings));
    }

    protected static IReadOnlyList<(int line, string text)> LinesOf(string? content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<(int, string)>();
        return content.Split('\n')
                      .Select((text, i) => (i + 1, text))
                      .ToList();
    }
}

/// <summary>P1 — Security hint : checks statiques légers de vulnérabilités courantes.</summary>
public sealed class SecurityHintAgent : HeuristicAgent
{
    private static readonly (Regex rx, string msg, string sev)[] Rules =
    {
        (new Regex(@"(SqlCommand|ExecuteQuery)\s*\(\s*[^)]*\+", RegexOptions.IgnoreCase),
         "Concaténation SQL détectée (risque d'injection).", "critical"),
        (new Regex(@"password\s*=\s*""[^""]+""", RegexOptions.IgnoreCase),
         "Mot de passe en dur dans le code.", "critical"),
        (new Regex(@"\beval\s*\(", RegexOptions.IgnoreCase),
         "Usage de eval() : exécution de code arbitraire.", "warning"),
        (new Regex(@"(MD5|SHA1)\.Create", RegexOptions.IgnoreCase),
         "Hash faible (MD5/SHA1) pour un usage de sécurité.", "warning")
    };

    public SecurityHintAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.security.hint", Name = "Security hint",
        Priority = AgentPriority.P1, Impact = "high", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var findings = new List<AgentFinding>();
        foreach (var (line, text) in LinesOf(request.CodeSnippet))
            foreach (var (rx, msg, sev) in Rules)
                if (rx.IsMatch(text))
                    findings.Add(new AgentFinding
                    {
                        Severity = sev, FilePath = request.FilePath ?? "",
                        Line = line, Message = msg,
                        Suggestion = "Préférer des API sûres (requêtes paramétrées, coffre de secrets, SHA256)."
                    });
        return findings;
    }
}

/// <summary>P1 — Privacy scanner : détecte secrets/PII avant publication.</summary>
public sealed class PrivacyScannerAgent : HeuristicAgent
{
    private static readonly (Regex rx, string msg, string sev)[] Rules =
    {
        (new Regex(@"AKIA[0-9A-Z]{16}"), "Clé AWS détectée.", "critical"),
        (new Regex(@"(sk|ghp|gho)_[A-Za-z0-9]{20,}"), "Token API/GitHub détecté.", "critical"),
        (new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----"), "Clé privée détectée.", "critical"),
        (new Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+"), "Adresse e-mail (PII possible).", "info")
    };

    public PrivacyScannerAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.privacy.scanner", Name = "Privacy scanner",
        Priority = AgentPriority.P1, Impact = "high", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var findings = new List<AgentFinding>();
        foreach (var (line, text) in LinesOf(request.CodeSnippet))
            foreach (var (rx, msg, sev) in Rules)
                if (rx.IsMatch(text))
                    findings.Add(new AgentFinding
                    {
                        Severity = sev, FilePath = request.FilePath ?? "", Line = line, Message = msg,
                        Suggestion = "Déplacer vers des variables d'environnement / secrets managés."
                    });
        return findings;
    }
}

/// <summary>P2 — Dependency risk : signale les packages risqués (heuristique sur noms/versions).</summary>
public sealed class DependencyRiskAgent : HeuristicAgent
{
    private static readonly string[] RiskyPrefixes = { "legacy-", "deprecated-", "unofficial-" };

    public DependencyRiskAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.dependency.risk", Name = "Dependency risk",
        Priority = AgentPriority.P2, Impact = "medium", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var findings = new List<AgentFinding>();
        // request.CodeSnippet contient ici la liste des dépendances (1 par ligne : "Name Version").
        foreach (var (line, text) in LinesOf(request.CodeSnippet))
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            string name = parts[0];

            if (RiskyPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                findings.Add(new AgentFinding
                {
                    Severity = "warning", FilePath = request.FilePath ?? "", Line = line,
                    Message = $"Package potentiellement risqué : {name}",
                    Suggestion = "Vérifier la maintenance et les alternatives."
                });
        }
        return findings;
    }
}

/// <summary>P1 — Auto-format policy : règles de style expliquées.</summary>
public sealed class AutoFormatPolicyAgent : HeuristicAgent
{
    public AutoFormatPolicyAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.format.policy", Name = "Auto-format policy",
        Priority = AgentPriority.P1, Impact = "medium", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var findings = new List<AgentFinding>();
        foreach (var (line, text) in LinesOf(request.CodeSnippet))
        {
            if (text.EndsWith(" ") || text.EndsWith("\t"))
                findings.Add(Mk(line, "Espace en fin de ligne.", "Supprimer les espaces de trop."));
            if (text.Length > 140)
                findings.Add(Mk(line, "Ligne > 140 caractères.", "Découper la ligne."));
            if (text.StartsWith("\t") && text.Contains("    "))
                findings.Add(Mk(line, "Indentation mixte tab/espace.", "Uniformiser l'indentation."));
        }
        return findings;

        AgentFinding Mk(int l, string msg, string fix) => new()
        {
            Severity = "info", FilePath = request.FilePath ?? "", Line = l, Message = msg, Suggestion = fix
        };
    }
}

/// <summary>P2 — Smart TODO assistant : convertit les TODO en tâches suivies.</summary>
public sealed class SmartTodoAgent : HeuristicAgent
{
    private static readonly Regex TodoRx = new(@"\b(TODO|FIXME|HACK|XXX)\b\s*:?\s*(.*)", RegexOptions.IgnoreCase);

    public SmartTodoAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.todo.assistant", Name = "Smart TODO assistant",
        Priority = AgentPriority.P2, Impact = "medium", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var findings = new List<AgentFinding>();
        foreach (var (line, text) in LinesOf(request.CodeSnippet))
        {
            var m = TodoRx.Match(text);
            if (m.Success)
                findings.Add(new AgentFinding
                {
                    Severity = "info", FilePath = request.FilePath ?? "", Line = line,
                    Message = $"{m.Groups[1].Value} : {m.Groups[2].Value.Trim()}",
                    Suggestion = "Convertir en tâche suivie avec rappel."
                });
        }
        return findings;
    }
}

/// <summary>P2 — Agent cost estimator : estime le coût CPU/mémoire et propose une alternative.</summary>
public sealed class AgentCostEstimatorAgent : ISpecializedAgent
{
    private readonly SpecializedAgentRegistry _registry;
    public AgentCostEstimatorAgent(SpecializedAgentRegistry registry) => _registry = registry;

    public AgentDescriptor Descriptor => new()
    {
        Id = "agent.cost.estimator", Name = "Cost estimator",
        Priority = AgentPriority.P2, Impact = "medium", RequiresLlm = false, EstimatedCpuCost = 0.05
    };

    public Task<SpecializedAgentResult> ExecuteAsync(SpecializedAgentRequest request, CancellationToken ct = default)
    {
        var target = request.Context.TryGetValue("targetAgent", out var id) ? _registry.Get(id) : null;
        if (target is null)
            return Task.FromResult(SpecializedAgentResult.Fail("Agent cible inconnu."));

        double inputFactor = (request.CodeSnippet?.Length ?? 0) / 10_000.0;
        double cost = Math.Clamp(target.Descriptor.EstimatedCpuCost + inputFactor * 0.1, 0, 1);
        string advice = cost > 0.7
            ? "Coût élevé : envisager une version heuristique ou un modèle plus petit."
            : "Coût acceptable.";

        return Task.FromResult(SpecializedAgentResult.Ok(
            $"Agent '{target.Descriptor.Name}' — coût estimé {cost:P0}. {advice}"));
    }
}

/// <summary>P2 — Test flakiness detector : marque les tests instables à partir de l'historique.</summary>
public sealed class TestFlakinessAgent : ISpecializedAgent
{
    public AgentDescriptor Descriptor => new()
    {
        Id = "agent.test.flakiness", Name = "Flakiness detector",
        Priority = AgentPriority.P2, Impact = "high", RequiresLlm = false, EstimatedCpuCost = 0.2
    };

    public Task<SpecializedAgentResult> ExecuteAsync(SpecializedAgentRequest request, CancellationToken ct = default)
    {
        // request.CodeSnippet : historique "TestName pass|fail" par ligne.
        var stats = new Dictionary<string, (int pass, int fail)>();
        foreach (var line in (request.CodeSnippet ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ');
            if (parts.Length < 2) continue;
            var (p, f) = stats.GetValueOrDefault(parts[0]);
            if (parts[1].Equals("pass", StringComparison.OrdinalIgnoreCase)) p++; else f++;
            stats[parts[0]] = (p, f);
        }

        var findings = stats
            .Where(kv => kv.Value.pass > 0 && kv.Value.fail > 0) // intermittent = potentiellement flaky
            .Select(kv => new AgentFinding
            {
                Severity = "warning", Message = $"Test instable : {kv.Key}",
                Suggestion = "Isoler les dépendances temporelles/aléatoires."
            })
            .ToList();

        return Task.FromResult(SpecializedAgentResult.WithFindings(findings));
    }
}

/// <summary>P2 — Code health dashboard : métriques légères par module. L'analyse profonde → XENO.</summary>
public sealed class CodeHealthAgent : ISpecializedAgent
{
    public AgentDescriptor Descriptor => new()
    {
        Id = "agent.code.health", Name = "Code health dashboard",
        Priority = AgentPriority.P2, Impact = "medium", RequiresLlm = false, EstimatedCpuCost = 0.2
    };

    public Task<SpecializedAgentResult> ExecuteAsync(SpecializedAgentRequest request, CancellationToken ct = default)
    {
        var content = request.CodeSnippet ?? "";
        int lines = content.Split('\n').Length;
        int loops = Regex.Matches(content, @"\b(for|foreach|while)\b").Count;
        int branches = Regex.Matches(content, @"\b(if|switch|case)\b").Count;
        double roughComplexity = 1 + loops + branches * 0.5;

        return Task.FromResult(SpecializedAgentResult.Ok(
            $"Lignes: {lines} | Complexité approx: {roughComplexity:F1}. " +
            "Pour duplication/couverture détaillées, déléguer à XENO-SSS∞."));
    }
}
