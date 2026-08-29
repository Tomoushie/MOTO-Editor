using Moto.Core.AI.Cortex;
using Moto.Core.AI.Context;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Propose des snippets selon le code courant + habitudes (Cortex).
/// 100% local, complète ContextEngine sans le remplacer.
/// </summary>
public sealed class SmartSnippetEngine
{
    private readonly CortexEngine? _cortex;
    private readonly SettingsEngine _settings;
    private readonly Dictionary<string, List<SnippetTemplate>> _library = new();

    public SmartSnippetEngine(CortexEngine? cortex, SettingsEngine settings)
    {
        _cortex = cortex;
        _settings = settings;
        RegisterBuiltInSnippets();
    }

    /// <summary>
    /// Retourne les snippets pertinents pour le contexte courant, triés par score.
    /// </summary>
    public IReadOnlyList<SnippetSuggestion> Suggest(SnippetRequest request)
    {
        if (!_settings.GetBool("ai.snippets.enabled", true))
            return Array.Empty<SnippetSuggestion>();

        if (!_library.TryGetValue(request.Language, out var templates))
            return Array.Empty<SnippetSuggestion>();

        var suggestions = new List<SnippetSuggestion>();

        foreach (var tpl in templates)
        {
            var score = ScoreSnippet(tpl, request);
            if (score > 0.15)
            {
                suggestions.Add(new SnippetSuggestion
                {
                    Template = tpl,
                    Score = score,
                    Trigger = DetectTrigger(tpl, request)
                });
            }
        }

        // Boost selon le style appris par Cortex (habitudes utilisateur)
        if (_cortex != null)
            ApplyCortexBoost(suggestions, request);

        return suggestions.OrderByDescending(s => s.Score).Take(5).ToList();
    }

    private double ScoreSnippet(SnippetTemplate tpl, SnippetRequest req)
    {
        var score = 0.0;

        // Contexte de fichier (test → snippets test, etc.)
        if (tpl.Context == SnippetContext.Test && req.IsInTestFile) score += 0.5;
        if (tpl.Context == SnippetContext.Class && req.IsTopLevel) score += 0.3;

        // Préfixe tapé
        if (!string.IsNullOrEmpty(req.Prefix) &&
            tpl.Trigger.StartsWith(req.Prefix, StringComparison.OrdinalIgnoreCase))
            score += 0.4;

        // Langage courant
        if (tpl.MinLanguageMatch) score += 0.2;

        return Math.Clamp(score, 0.0, 1.0);
    }

    private void ApplyCortexBoost(List<SnippetSuggestion> suggestions, SnippetRequest req)
    {
        // Boost léger pour les snippets fréquemment acceptés (feedback loop)
        foreach (var s in suggestions)
        {
            var accepted = _settings.GetInt($"ai.snippets.accepted.{s.Template.Id}", 0);
            s.Score += Math.Min(0.2, accepted * 0.02);
        }
    }

    private static string DetectTrigger(SnippetTemplate tpl, SnippetRequest req)
        => string.IsNullOrEmpty(req.Prefix) ? tpl.Trigger : req.Prefix;

    /// <summary>À appeler quand l'utilisateur accepte un snippet (feedback).</summary>
    public void NotifyAccepted(string snippetId)
    {
        var count = _settings.GetInt($"ai.snippets.accepted.{snippetId}", 0);
        _settings.Set($"ai.snippets.accepted.{snippetId}", count + 1);
    }

    private void RegisterBuiltInSnippets()
    {
        _library["csharp"] = new List<SnippetTemplate>
        {
            new() { Id = "ctor", Trigger = "ctor", Language = "csharp",
                    Body = "public {ClassName}()\n{\n    \n}",
                    Context = SnippetContext.Class, MinLanguageMatch = true },
            new() { Id = "prop", Trigger = "prop", Language = "csharp",
                    Body = "public {Type} {Name} {{ get; set; }}",
                    Context = SnippetContext.Class, MinLanguageMatch = true },
            new() { Id = "test", Trigger = "test", Language = "csharp",
                    Body = "[Fact]\npublic void {Name}_Should_{Expectation}()\n{\n    // Arrange\n    // Act\n    // Assert\n}",
                    Context = SnippetContext.Test, MinLanguageMatch = true },
            new() { Id = "trycatch", Trigger = "try", Language = "csharp",
                    Body = "try\n{\n    \n}\ncatch (Exception ex)\n{\n    \n}",
                    Context = SnippetContext.Any, MinLanguageMatch = true },
        };

        _library["javascript"] = new List<SnippetTemplate>
        {
            new() { Id = "arrow", Trigger = "afn", Language = "javascript",
                    Body = "const {name} = ({args}) => {\n    \n};",
                    Context = SnippetContext.Any, MinLanguageMatch = true },
        };
    }
}

public class SnippetRequest
{
    public string Language { get; init; } = "csharp";
    public string Prefix { get; init; } = "";
    public bool IsInTestFile { get; init; }
    public bool IsTopLevel { get; init; }
    public string FilePath { get; init; } = "";
}

public class SnippetSuggestion
{
    public SnippetTemplate Template { get; init; } = null!;
    public double Score { get; set; }
    public string Trigger { get; init; } = "";
}

public class SnippetTemplate
{
    public string Id { get; init; } = "";
    public string Trigger { get; init; } = "";
    public string Language { get; init; } = "";
    public string Body { get; init; } = "";
    public SnippetContext Context { get; init; }
    public bool MinLanguageMatch { get; init; }
}

public enum SnippetContext { Any, Class, Test, Async, Ui }
