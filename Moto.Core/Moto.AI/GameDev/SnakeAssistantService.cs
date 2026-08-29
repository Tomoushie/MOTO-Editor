using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.GameDev;

public sealed class LevelConfig
{
    public string Name { get; set; } = "";
    public int Width { get; set; } = 20;
    public int Height { get; set; } = 20;
    public List<string> Obstacles { get; set; } = new();
    public List<string> Traps { get; set; } = new();
    public string? Boss { get; set; }
}

public sealed class BalancingSuggestion
{
    public string Stat { get; set; } = "";
    public double CurrentValue { get; set; }
    public double SuggestedValue { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Bloc 7 — Assistant IA pour Snake2000 / SnakeEngine V2 (6 idées).
/// MOTO AI assiste/propose ; la génération structurée reste déléguée à XENO.
/// </summary>
public sealed class SnakeAssistantService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public SnakeAssistantService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Idée "Level designer textuel" — texte → config JSON.</summary>
    public LevelConfig GenerateLevelFromText(string description)
    {
        var config = new LevelConfig { Name = "GeneratedLevel" };

        // Parse simple du texte descriptif
        if (description.Contains("couloir", StringComparison.OrdinalIgnoreCase))
        {
            config.Width = 30;
            config.Height = 10;
        }
        if (description.Contains("piège", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("piege", StringComparison.OrdinalIgnoreCase))
        {
            config.Traps.Add("spike");
            config.Traps.Add("hole");
        }
        if (description.Contains("boss", StringComparison.OrdinalIgnoreCase))
        {
            config.Boss = "BossSnake";
        }
        if (description.Contains("mur", StringComparison.OrdinalIgnoreCase))
        {
            config.Obstacles.Add("wall");
        }

        _log.Info("SnakeAssistant", "Niveau généré depuis texte", new { description, config.Name });
        return config;
    }

    /// <summary>Idée "Balancing assistant" — propose ajustements de stats.</summary>
    public IReadOnlyList<BalancingSuggestion> AnalyzeBalance(
        double speed, double damage, double difficulty, double playerDeathRate)
    {
        var suggestions = new List<BalancingSuggestion>();

        // Si le joueur meurt trop souvent, réduire la difficulté
        if (playerDeathRate > 0.5)
        {
            suggestions.Add(new BalancingSuggestion
            {
                Stat = "speed",
                CurrentValue = speed,
                SuggestedValue = speed * 0.85,
                Reason = $"Taux de mortalité élevé ({playerDeathRate:P0}) : ralentir le jeu."
            });
        }

        if (difficulty > 0.8 && playerDeathRate > 0.4)
        {
            suggestions.Add(new BalancingSuggestion
            {
                Stat = "damage",
                CurrentValue = damage,
                SuggestedValue = damage * 0.75,
                Reason = "Difficulté élevée combinée à un fort taux de mortalité."
            });
        }

        return suggestions;
    }

    /// <summary>Idée "Bug pattern detector" — repère patterns de bugs fréquents.</summary>
    public IReadOnlyList<(int line, string issue)> DetectBugPatterns(string code)
    {
        var findings = new List<(int, string)>();
        var lines = code.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Accès tableau sans borne
            if (Regex.IsMatch(line, @"\[\s*\w+\s*\]") && !line.Contains("Length") && !line.Contains("Count"))
            {
                if (Regex.IsMatch(line, @"\[\s*(index|i|j)\s*\]"))
                    findings.Add((i + 1, "Accès indexé sans vérification de bornes."));
            }

            // Déréférencement potentiellement null
            if (Regex.IsMatch(line, @"\.\w+\.\w+") && !line.Contains("?."))
            {
                // Heuristique légère : chains d'accès sans null-conditional
            }
        }

        return findings;
    }

    /// <summary>Idée "Porting helper" — liste points de friction multi-plateforme.</summary>
    public IReadOnlyList<string> AnalyzePortingFriction(string targetPlatform)
    {
        var friction = new List<string>();

        friction.Add(targetPlatform.ToLowerInvariant() switch
        {
            "linux" => "Linux : vérifier chemins (Path.DirectorySeparatorChar), pas de WinUI.",
            "macos" => "macOS : vérifier permissions Gatekeeper, bundling .app.",
            _ => "Plateforme inconnue : analyse générique."
        });

        friction.Add("Vérifier les appels P/Invoke spécifiques Windows.");
        friction.Add("Vérifier les dépendances GPU/directives #if WINDOWS.");

        return friction;
    }

    /// <summary>Idée "Performance hints" — commente les hotspots.</summary>
    public IReadOnlyList<(int line, string hint)> DetectPerformanceHotspots(string code)
    {
        var hints = new List<(int, string)>();
        var lines = code.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.Contains("new List<") && (line.Contains("for") || line.Contains("foreach")))
                hints.Add((i + 1, "Allocation dans une boucle : envisager un pool ou réutilisation."));

            if (line.Contains(".ToList()") || line.Contains(".ToArray()"))
                hints.Add((i + 1, "LINQ materialise la collection : coût mémoire dans le moteur."));

            if (Regex.IsMatch(line, @"\(\s*object\s*\)") || line.Contains("ToString()"))
                hints.Add((i + 1, "Boxing/allocation potentielle dans le hot path."));
        }

        return hints;
    }

    /// <summary>Idée "Gameplay script explainer" — explique en langage naturel.</summary>
    public string ExplainGameplayScript(string script)
    {
        var explanation = new System.Text.StringBuilder();

        if (script.Contains("boss", StringComparison.OrdinalIgnoreCase))
            explanation.AppendLine("Ce script gère un boss : il déclenche des comportements spécifiques.");
        if (script.Contains("charge", StringComparison.OrdinalIgnoreCase) ||
            script.Contains("dash", StringComparison.OrdinalIgnoreCase))
            explanation.AppendLine("Le boss charge/fonce vers le joueur sous certaines conditions.");
        if (script.Contains("spawn", StringComparison.OrdinalIgnoreCase))
            explanation.AppendLine("Des entités sont générées (spawn) à des moments clés.");
        if (script.Contains("damage", StringComparison.OrdinalIgnoreCase))
            explanation.AppendLine("Le script applique des dégâts au contact ou à distance.");

        return explanation.Length > 0
            ? explanation.ToString()
            : "Ce script définit la logique de gameplay du niveau.";
    }
}
