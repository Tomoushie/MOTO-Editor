using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Style;

public sealed class StyleProfile
{
    public Dictionary<string, string> NamingConventions { get; set; } = new();
    public Dictionary<string, int> PatternFrequency { get; set; } = new();
    public string IndentationStyle { get; set; } = "    "; // 4 espaces par défaut
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StyleDiff
{
    public string AiStyle { get; set; } = "";
    public string UserStyle { get; set; } = "";
    public double ConsistencyScore { get; set; }
    public List<string> Differences { get; set; } = new();
}

/// <summary>
/// Bloc 6b — IA pour apprentissage du style personnel (6 idées).
/// </summary>
public sealed class StyleLearningService
{
    private static readonly string StyleProfilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "style-profile.json");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private StyleProfile _profile = new();

    public StyleLearningService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        LoadProfile();
    }

    /// <summary>Idée "Style learning local-only" — apprend sans cloud.</summary>
    public void LearnFromCode(string code, string filePath)
    {
        if (!SettingsCatalog.Ai.Profiles.StyleLearningLocal.Value)
            return;

        // Extrait conventions de nommage
        var methods = Regex.Matches(code, @"\b(public|private)\s+\w+\s+(\w+)\s*\(");
        foreach (Match match in methods)
        {
            string name = match.Groups[2].Value;
            string convention = char.IsUpper(name[0]) ? "PascalCase" : "camelCase";
            _profile.NamingConventions["method"] = convention;
        }

        // Détecte indentation
        var lines = code.Split('\n');
        foreach (var line in lines.Take(20))
        {
            if (line.StartsWith("    ")) _profile.IndentationStyle = "    ";
            else if (line.StartsWith("\t")) _profile.IndentationStyle = "\t";
        }

        _profile.LastUpdatedUtc = DateTime.UtcNow;
        SaveProfile();
    }

    /// <summary>Idée "Imiter ton ancien code" — base sur fichiers anciens.</summary>
    public string ImitateOldStyle(string workspacePath, string intent)
    {
        if (!SettingsCatalog.Ai.Profiles.ImitateOldCode.Value)
            return "";

        var files = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories)
                             .OrderBy(f => File.GetCreationTimeUtc(f))
                             .Take(5);

        // Simplifié : retourne le style du fichier le plus ancien
        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Length > 100)
                    return $"// Style imité de {Path.GetFileName(file)}\n{intent}";
            }
            catch { /* Ignore */ }
        }

        return intent;
    }

    /// <summary>Idée "Score de cohérence stylistique" — calcule 0-100.</summary>
    public double CalculateConsistencyScore(string aiSuggestion, string userCode)
    {
        if (!SettingsCatalog.Ai.Profiles.StyleConsistencyScore.Value)
            return 100;

        double score = 100;

        // Compare indentation
        if (aiSuggestion.Contains("    ") && userCode.Contains("\t"))
            score -= 20;
        else if (aiSuggestion.Contains("\t") && userCode.Contains("    "))
            score -= 20;

        // Compare naming
        var aiMethods = Regex.Matches(aiSuggestion, @"\b(public|private)\s+\w+\s+(\w+)\s*\(");
        var userMethods = Regex.Matches(userCode, @"\b(public|private)\s+\w+\s+(\w+)\s*\(");

        if (aiMethods.Count > 0 && userMethods.Count > 0)
        {
            bool aiPascal = char.IsUpper(aiMethods[0].Groups[2].Value[0]);
            bool userPascal = char.IsUpper(userMethods[0].Groups[2].Value[0]);
            if (aiPascal != userPascal) score -= 30;
        }

        return Math.Max(0, score);
    }

    /// <summary>Idée "Suggestions style diff" — montre différences.</summary>
    public StyleDiff GenerateStyleDiff(string aiSuggestion, string userCode)
    {
        var diff = new StyleDiff
        {
            AiStyle = aiSuggestion,
            UserStyle = userCode,
            ConsistencyScore = CalculateConsistencyScore(aiSuggestion, userCode)
        };

        if (aiSuggestion.Contains("    ") && userCode.Contains("\t"))
            diff.Differences.Add("Indentation : espaces vs tabulations");
        if (aiSuggestion.Contains("var ") && !userCode.Contains("var "))
            diff.Differences.Add("Utilisation de var");

        return diff;
    }

    /// <summary>Idée "Strict no auto-format" — pas de formatage.</summary>
    public bool AllowsAutoFormat()
    {
        return !SettingsCatalog.Ai.Profiles.StrictNoAutoFormat.Value;
    }

    /// <summary>Idée "Style mentor" — explique les choix.</summary>
    public string ExplainStyleChoice(string suggestion, string alternative)
    {
        if (!SettingsCatalog.Ai.Profiles.StyleMentorMode.Value)
            return "";

        return $"Pourquoi ce style ?\n" +
               $"Proposition : {suggestion}\n" +
               $"Alternative : {alternative}\n" +
               $"Raison : meilleure lisibilité et conventions C# modernes";
    }

    private void LoadProfile()
    {
        if (!File.Exists(StyleProfilePath)) return;
        try
        {
            _profile = JsonSerializer.Deserialize<StyleProfile>(File.ReadAllText(StyleProfilePath)) ?? new();
        }
        catch { _profile = new(); }
    }

    private void SaveProfile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StyleProfilePath)!);
            File.WriteAllText(StyleProfilePath, JsonSerializer.Serialize(_profile));
        }
        catch (Exception ex)
        {
            _log.Error("StyleLearning", "Échec sauvegarde profil", new { ex.Message });
        }
    }
}
