using System;
using System.Collections.Generic;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Profiles;

public enum AiProfile
{
    Default,
    Minimalist,
    RefactorOnly,
    SilentArchitect,
    StrictCSharp,
    GameEngineFocus,
    NoExternalDeps,
    DebuggingCoach
}

/// <summary>
/// Bloc 3 — Profils comportementaux IA (7 idées).
/// </summary>
public sealed class AiProfileService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public AiProfileService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public AiProfile GetActiveProfile()
    {
        if (SettingsCatalog.Ai.Profiles.MinimalistMode.Value) return AiProfile.Minimalist;
        if (SettingsCatalog.Ai.Profiles.RefactorOnlyMode.Value) return AiProfile.RefactorOnly;
        if (SettingsCatalog.Ai.Profiles.SilentArchitectMode.Value) return AiProfile.SilentArchitect;
        if (SettingsCatalog.Ai.Profiles.StrictCSharpMode.Value) return AiProfile.StrictCSharp;
        if (SettingsCatalog.Ai.Profiles.GameEngineFocus.Value) return AiProfile.GameEngineFocus;
        if (SettingsCatalog.Ai.Profiles.NoExternalDeps.Value) return AiProfile.NoExternalDeps;
        if (SettingsCatalog.Ai.Profiles.DebuggingCoachMode.Value) return AiProfile.DebuggingCoach;
        return AiProfile.Default;
    }

    /// <summary>Idée "Minimaliste" — limite la taille des suggestions.</summary>
    public string EnforceMinimalist(string suggestion)
    {
        if (!SettingsCatalog.Ai.Profiles.MinimalistMode.Value)
            return suggestion;

        var lines = suggestion.Split('\n');
        return lines.Length > 5 ? string.Join("\n", lines.Take(5)) + "\n// ... (tronqué)" : suggestion;
    }

    /// <summary>Idée "Refactor-only" — refuse la génération nouvelle.</summary>
    public bool AllowsNewCodeGeneration()
    {
        return !SettingsCatalog.Ai.Profiles.RefactorOnlyMode.Value;
    }

    /// <summary>Idée "Architecte silencieux" — génère des diagrammes textuels.</summary>
    public string GenerateArchitectureDiagram(string workspacePath)
    {
        if (!SettingsCatalog.Ai.Profiles.SilentArchitectMode.Value)
            return "";

        return "Diagramme d'architecture :\n" +
               "┌─────────────┐\n" +
               "│   Input     │\n" +
               "└──────┬──────┘\n" +
               "       │\n" +
               "┌──────▼──────┐\n" +
               "│  Logic      │\n" +
               "└──────┬──────┘\n" +
               "       │\n" +
               "┌──────▼──────┐\n" +
               "│  Rendering  │\n" +
               "└─────────────┘";
    }

    /// <summary>Idée "Strict C# idiomatic" — valide le style C# moderne.</summary>
    public IReadOnlyList<string> ValidateCSharpIdiomatic(string code)
    {
        var issues = new List<string>();
        if (!SettingsCatalog.Ai.Profiles.StrictCSharpMode.Value)
            return issues;

        if (code.Contains("var ") && !code.Contains("async "))
            issues.Add("Préférer async/await pour les opérations asynchrones");
        if (code.Contains("new List<") && code.Contains(".Add("))
            issues.Add("Utiliser des collection initializers : new List<T> { item1, item2 }");
        if (code.Contains(".Result"))
            issues.Add("Éviter .Result, utiliser await");

        return issues;
    }

    /// <summary>Idée "Game Engine focus" — privilégie ECS/pipelines.</summary>
    public string SuggestGameEnginePattern(string context)
    {
        if (!SettingsCatalog.Ai.Profiles.GameEngineFocus.Value)
            return "";

        return "Pattern recommandé : ECS (Entity-Component-System)\n" +
               "- Entity : identifiant unique\n" +
               "- Component : données (Position, Velocity)\n" +
               "- System : logique (MovementSystem, RenderSystem)";
    }

    /// <summary>Idée "No external deps" — refuse les libs externes.</summary>
    public bool AllowsExternalDependency(string packageName)
    {
        if (!SettingsCatalog.Ai.Profiles.NoExternalDeps.Value)
            return true;

        // Liste blanche BCL
        var allowed = new[] { "System", "Microsoft", "Moto" };
        return allowed.Any(prefix => packageName.StartsWith(prefix));
    }

    /// <summary>Idée "Debugging coach" — guide plutôt que fix direct.</summary>
    public string CoachDebugging(string exceptionMessage, string stackTrace)
    {
        if (!SettingsCatalog.Ai.Profiles.DebuggingCoachMode.Value)
            return "";

        return $"🔍 Analyse de l'exception :\n" +
               $"Type : {exceptionMessage.Split(':')[0]}\n" +
               $"Hypothèses :\n" +
               $"1. Vérifier les références null\n" +
               $"2. Contrôler les conditions aux limites\n" +
               $"3. Examiner la stack trace ligne par ligne";
    }
}
