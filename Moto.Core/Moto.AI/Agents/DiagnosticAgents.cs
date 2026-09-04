// Moto.Core/AI/Agents/DiagnosticAgents.cs
// ★ AJOUT (04/09) : les 4 agents explicitement demandés par Tom (liste
// relayée d'un autre outil, vérifiée contre le vrai code avant construction
// — voir la mémoire dédiée). Tous des HeuristicAgent (LinesOf/
// StripStringsAndComments hérités de StaticAnalysisAgents.cs) : jamais de
// LLM, jamais mutants, donc jamais de confirmation à demander — ils ne font
// que LIRE et signaler, exactement comme SecurityHintAgent/PrivacyScannerAgent
// déjà existants. "PAS de parsing profond" : heuristiques honnêtes, pas un
// vrai compilateur/analyseur — les Suggestion/Message le rappellent au besoin.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.Settings;

namespace Moto.Core.AI.Agents;

/// <summary>Vérification syntaxique légère : équilibre des accolades/
/// parenthèses/crochets, en ignorant chaînes/commentaires (StripStringsAndComments).
/// Un déséquilibre réel empêche généralement la compilation — signal fiable ;
/// l'absence de déséquilibre ne garantit PAS que le fichier compile.</summary>
public sealed class SyntaxAgent : HeuristicAgent
{
    public SyntaxAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.syntax.balance", Name = "Vérification syntaxique",
        Description = "Détecte les accolades/parenthèses/crochets non équilibrés — signal rapide, pas un vrai compilateur.",
        Priority = AgentPriority.P1, Impact = "high", RequiresLlm = false, EstimatedCpuCost = 0.05
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var stripped = StripStringsAndComments(request.CodeSnippet ?? "");
        var findings = new List<AgentFinding>();
        CheckBalance(stripped, '{', '}', "accolade", findings, request.FilePath);
        CheckBalance(stripped, '(', ')', "parenthèse", findings, request.FilePath);
        CheckBalance(stripped, '[', ']', "crochet", findings, request.FilePath);
        return findings;
    }

    private static void CheckBalance(string content, char open, char close, string label, List<AgentFinding> findings, string? path)
    {
        int depth = 0;
        foreach (var ch in content)
        {
            if (ch == open) depth++;
            else if (ch == close) depth--;
        }
        if (depth == 0) return;

        findings.Add(new AgentFinding
        {
            Severity = "critical",
            FilePath = path ?? "",
            Message = depth > 0
                ? $"{depth} « {open} » ouverte(s) jamais refermée(s) (déséquilibre de {label}s)."
                : $"{-depth} « {close} » en trop, sans ouverture correspondante (déséquilibre de {label}s).",
            Suggestion = "Relire le fichier autour des dernières modifications — ce déséquilibre empêche généralement la compilation."
        });
    }
}

/// <summary>Complexité approximative par comptage de points de décision
/// (if/else if/for/foreach/while/case/catch/&&/||/?:) — même principe que
/// la complexité cyclomatique, sans vrai parseur. Toujours UN constat rendu
/// (même "sain"), pour confirmer que l'agent a bien tourné.</summary>
public sealed class ComplexityAgent : HeuristicAgent
{
    private static readonly Regex DecisionPoints = new(
        @"\b(if|else\s+if|for|foreach|while|case|catch)\b|&&|\|\||\?[^.]", RegexOptions.Compiled);

    public ComplexityAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.complexity", Name = "Complexité approximative",
        Description = "Compte les points de décision (if/boucles/case/&&/||) — une approximation de la complexité cyclomatique, pas une mesure exacte.",
        Priority = AgentPriority.P2, Impact = "medium", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var stripped = StripStringsAndComments(request.CodeSnippet ?? "");
        int score = 1 + DecisionPoints.Matches(stripped).Count;

        var (severity, verdict) = score switch
        {
            > 60 => ("critical", "très élevée — un découpage en fonctions plus petites aiderait beaucoup"),
            > 30 => ("warning", "élevée — envisager de découper en fonctions plus petites"),
            _ => ("info", "raisonnable")
        };

        return new List<AgentFinding>
        {
            new()
            {
                Severity = severity,
                FilePath = request.FilePath ?? "",
                Message = $"Complexité approximative : {score} point(s) de décision — {verdict}.",
                Suggestion = severity == "info" ? null : "Extraire les blocs les plus imbriqués en méthodes nommées."
            }
        };
    }
}

/// <summary>Cohérence de style DANS un même fichier : casse des noms de
/// méthodes (PascalCase vs camelCase). Volontairement limité à UN fichier —
/// SpecializedAgentRequest ne porte le contenu que d'un seul fichier à la
/// fois ; une cohérence VRAIMENT inter-fichiers serait un chantier à part
/// (élargir la requête pour porter plusieurs fichiers).</summary>
public sealed class ConsistencyAgent : HeuristicAgent
{
    private static readonly Regex MethodDecl = new(
        @"\b(?:public|private|protected|internal|static)\s+(?:[\w<>\[\],\?\s]+?)\s+([A-Za-z_]\w*)\s*\(",
        RegexOptions.Compiled);

    public ConsistencyAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.consistency.naming", Name = "Cohérence de nommage",
        Description = "Repère les noms de méthodes qui rompent la casse majoritaire du fichier (PascalCase vs camelCase). Limité à un seul fichier à la fois.",
        Priority = AgentPriority.P2, Impact = "low", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var stripped = StripStringsAndComments(request.CodeSnippet ?? "");
        var names = MethodDecl.Matches(stripped).Select(m => m.Groups[1].Value).Distinct().ToList();
        if (names.Count < 4) return Array.Empty<AgentFinding>(); // trop peu pour parler de "majorité"

        int pascal = names.Count(IsPascalCase);
        int camel = names.Count(IsCamelCase);
        bool majorityPascal = pascal >= camel;
        var outliers = names.Where(n => majorityPascal ? !IsPascalCase(n) : !IsCamelCase(n)).ToList();

        if (outliers.Count == 0 || outliers.Count > names.Count / 2) return Array.Empty<AgentFinding>(); // pas assez net pour être utile

        var majorityLabel = majorityPascal ? "PascalCase" : "camelCase";
        return outliers.Select(n => new AgentFinding
        {
            Severity = "info",
            FilePath = request.FilePath ?? "",
            Message = $"« {n} » ne suit pas la casse majoritaire du fichier ({majorityLabel}).",
            Suggestion = $"Renommer en cohérence avec le reste du fichier, si ce n'est pas intentionnel."
        }).ToList();
    }

    private static bool IsPascalCase(string s) => s.Length > 0 && char.IsUpper(s[0]);
    private static bool IsCamelCase(string s) => s.Length > 0 && char.IsLower(s[0]);
}

/// <summary>Suggestions de patterns : imbrication profonde (candidate à un
/// retour anticipé/extraction) et nombres "magiques" répétés (candidats à
/// une constante nommée). Toujours en "info" — ce sont des suggestions, pas
/// des défauts ; beaucoup de nesting/littéraux sont parfaitement légitimes.</summary>
public sealed class PatternAgent : HeuristicAgent
{
    private static readonly Regex NumberLiteral = new(@"(?<![\w.])(?!0x)\d{2,}(?![\w.])", RegexOptions.Compiled);
    private static readonly HashSet<string> CommonNumbers = new() { "10", "100", "200", "404", "500", "1000" };

    public PatternAgent(SettingsEngine s, ExplainabilityLogger e) : base(s, e) { }
    public override AgentDescriptor Descriptor => new()
    {
        Id = "agent.pattern.suggest", Name = "Suggestions de patterns",
        Description = "Signale l'imbrication profonde et les nombres répétés en dur — des pistes, pas des défauts.",
        Priority = AgentPriority.P2, Impact = "low", RequiresLlm = false, EstimatedCpuCost = 0.1
    };

    protected override IReadOnlyList<AgentFinding> Analyze(SpecializedAgentRequest request)
    {
        var stripped = StripStringsAndComments(request.CodeSnippet ?? "");
        var findings = new List<AgentFinding>();

        // Imbrication : profondeur d'indentation max (en unités de 4 espaces
        // ou d'une tabulation) — un proxy grossier mais bon marché du niveau
        // d'imbrication réel, sans vrai parseur.
        int maxDepth = 0;
        foreach (var (line, text) in LinesOf(stripped))
        {
            var trimmed = text.TrimStart(' ', '\t');
            if (trimmed.Length == 0) continue;
            int leading = text.Length - trimmed.Length;
            int depth = text.Contains('\t') && !text.StartsWith("    ")
                ? text.TakeWhile(c => c == '\t').Count()
                : leading / 4;
            if (depth > maxDepth) maxDepth = depth;
            if (depth >= 6)
                findings.Add(new AgentFinding
                {
                    Severity = "info", FilePath = request.FilePath ?? "", Line = line,
                    Message = $"Imbrication profonde (≈{depth} niveaux) à cette ligne.",
                    Suggestion = "Un retour anticipé (early return) ou l'extraction d'une méthode peuvent aplatir ce bloc."
                });
        }

        // Nombres magiques : littéraux numériques (≥2 chiffres) répétés ≥3
        // fois, hors valeurs très courantes (10/100/200/404/500/1000).
        var counts = NumberLiteral.Matches(stripped)
            .Select(m => m.Value)
            .Where(n => !CommonNumbers.Contains(n))
            .GroupBy(n => n)
            .Where(g => g.Count() >= 3);
        foreach (var g in counts)
            findings.Add(new AgentFinding
            {
                Severity = "info", FilePath = request.FilePath ?? "",
                Message = $"Le nombre {g.Key} apparaît {g.Count()} fois en dur dans ce fichier.",
                Suggestion = "Si ce n'est pas une coïncidence, une constante nommée expliquerait son sens."
            });

        return findings;
    }
}
