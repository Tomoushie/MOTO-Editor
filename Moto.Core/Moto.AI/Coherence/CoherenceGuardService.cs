using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Coherence;

public sealed class CoherenceViolation
{
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public string Issue { get; set; } = "";
    public string Suggestion { get; set; } = "";
}

public sealed class ResponsibilityMapEntry
{
    public string Module { get; set; } = "";
    public string Responsibility { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

/// <summary>
/// Bloc 6a — IA comme gardien de cohérence (7 idées).
/// </summary>
public sealed class CoherenceGuardService
{
    private static readonly string ArchitectureJournalPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "architecture-decisions.md");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public CoherenceGuardService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Idée "Contrat de cohérence" — valide naming/patterns.</summary>
    public IReadOnlyList<CoherenceViolation> ValidateCoherence(string code, string filePath)
    {
        var violations = new List<CoherenceViolation>();
        if (!_settings.Shared.Ai.Profiles.CoherenceContract.Value)
            return violations;

        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Vérifie naming convention (PascalCase pour classes)
            var classMatch = Regex.Match(line, @"\bclass\s+(\w+)");
            if (classMatch.Success && !char.IsUpper(classMatch.Groups[1].Value[0]))
            {
                violations.Add(new CoherenceViolation
                {
                    FilePath = filePath,
                    Line = i + 1,
                    Issue = "Nom de classe non-PascalCase",
                    Suggestion = $"Renommer en {char.ToUpper(classMatch.Groups[1].Value[0]) + classMatch.Groups[1].Value.Substring(1)}"
                });
            }

            // Vérifie complexité (nesting profond)
            int nesting = line.TakeWhile(c => c == ' ' || c == '\t').Count() / 4;
            if (nesting > 4)
            {
                violations.Add(new CoherenceViolation
                {
                    FilePath = filePath,
                    Line = i + 1,
                    Issue = "Nesting trop profond",
                    Suggestion = "Extraire dans une méthode séparée"
                });
            }
        }

        return violations;
    }

    /// <summary>Idée "Audit de dépendances" — détecte couplages forts.</summary>
    public IReadOnlyList<string> AuditDependencies(string workspacePath)
    {
        var issues = new List<string>();
        if (!_settings.Shared.Ai.Profiles.DependencyAudit.Value)
            return issues;

        // Scan simplifié : compte les using directives
        var files = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files.Take(20))
        {
            try
            {
                var content = File.ReadAllText(file);
                var usings = Regex.Matches(content, @"using\s+([\w.]+);");
                if (usings.Count > 15)
                    issues.Add($"{Path.GetFileName(file)} : {usings.Count} dépendances (élevé)");
            }
            catch { /* Ignore */ }
        }

        return issues;
    }

    /// <summary>Idée "Carte des responsabilités" — qui fait quoi.</summary>
    public IReadOnlyList<ResponsibilityMapEntry> GenerateResponsibilityMap(string workspacePath)
    {
        var map = new List<ResponsibilityMapEntry>();
        if (!_settings.Shared.Ai.Profiles.ResponsibilityMap.Value)
            return map;

        var files = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories);
        var modules = new Dictionary<string, List<string>>
        {
            ["Input"] = new(),
            ["Logic"] = new(),
            ["Rendering"] = new(),
            ["UI"] = new()
        };

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            if (name.Contains("input") || name.Contains("controller")) modules["Input"].Add(file);
            else if (name.Contains("render") || name.Contains("draw")) modules["Rendering"].Add(file);
            else if (name.Contains("ui") || name.Contains("view")) modules["UI"].Add(file);
            else modules["Logic"].Add(file);
        }

        foreach (var kv in modules)
        {
            map.Add(new ResponsibilityMapEntry
            {
                Module = kv.Key,
                Responsibility = $"Gère {kv.Key.ToLowerInvariant()}",
                Files = kv.Value
            });
        }

        return map;
    }

    /// <summary>Idée "Détection code magique" — marque zones complexes.</summary>
    public IReadOnlyList<CoherenceViolation> DetectMagicCode(string code, string filePath)
    {
        var violations = new List<CoherenceViolation>();
        if (!_settings.Shared.Ai.Profiles.MagicCodeDetection.Value)
            return violations;

        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Détecte conditions complexes
            if (Regex.Matches(line, @"\b(if|else|for|while)\b").Count > 3)
            {
                violations.Add(new CoherenceViolation
                {
                    FilePath = filePath,
                    Line = i + 1,
                    Issue = "Condition trop complexe",
                    Suggestion = "Découper en sous-conditions ou extraire méthode"
                });
            }
        }

        return violations;
    }

    /// <summary>Idée "Contrôle duplication logique" — repère patterns dupliqués.</summary>
    public IReadOnlyList<string> DetectLogicalDuplication(string workspacePath)
    {
        var duplicates = new List<string>();
        if (!_settings.Shared.Ai.Profiles.LogicalDuplicationControl.Value)
            return duplicates;

        // Simplifié : détecte méthodes avec signatures similaires
        var files = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories);
        var methodSignatures = new Dictionary<string, List<string>>();

        foreach (var file in files.Take(20))
        {
            try
            {
                var content = File.ReadAllText(file);
                var methods = Regex.Matches(content, @"\b(public|private)\s+\w+\s+(\w+)\s*\(");
                foreach (Match match in methods)
                {
                    string sig = match.Groups[2].Value;
                    if (!methodSignatures.ContainsKey(sig))
                        methodSignatures[sig] = new List<string>();
                    methodSignatures[sig].Add(file);
                }
            }
            catch { /* Ignore */ }
        }

        foreach (var kv in methodSignatures.Where(x => x.Value.Count > 2))
        {
            duplicates.Add($"Méthode '{kv.Key}' dupliquée dans {kv.Value.Count} fichiers");
        }

        return duplicates;
    }

    /// <summary>Idée "API contract first" — aide à définir interfaces.</summary>
    public string SuggestApiContract(string intent)
    {
        if (!_settings.Shared.Ai.Profiles.ApiContractFirst.Value)
            return "";

        return $"Interface suggérée pour '{intent}' :\n" +
               $"public interface I{intent}\n" +
               $"{{\n" +
               $"    void Execute();\n" +
               $"    Task ExecuteAsync();\n" +
               $"}}";
    }

    /// <summary>Idée "Journal de décisions architecturales".</summary>
    public void LogArchitectureDecision(string decision, string rationale)
    {
        if (!_settings.Shared.Ai.Profiles.ArchitectureJournal.Value)
            return;

        try
        {
            var entry = $"## {DateTime.Now:yyyy-MM-dd HH:mm}\n\n" +
                       $"**Décision** : {decision}\n\n" +
                       $"**Raison** : {rationale}\n\n---\n\n";
            File.AppendAllText(ArchitectureJournalPath, entry);
            _log.Info("CoherenceGuard", "Décision architecturale journalisée");
        }
        catch (Exception ex)
        {
            _log.Error("CoherenceGuard", "Échec journal", new { ex.Message });
        }
    }
}
