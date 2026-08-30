using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Pedagogy;

public sealed class LearningProgress
{
    public Dictionary<string, bool> Concepts { get; set; } = new();
    public List<string> FrequentErrors { get; set; } = new();
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public int EstimatedLevel { get; set; } = 1; // 1-10
}

public sealed class TutorialMission
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string VerificationCode { get; set; } = ""; // Code à vérifier
    public bool Completed { get; set; }
}

/// <summary>
/// Bloc 2 — Pédagogie & onboarding IA (7 idées).
/// </summary>
public sealed class PedagogyEngine
{
    private static readonly string LearningLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", "learning.log");

    private static readonly string ProgressPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "learning-progress.json");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private LearningProgress _progress = new();
    private readonly List<TutorialMission> _missions = new();

    public PedagogyEngine(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        LoadProgress();
        InitializeMissions();
    }

    /// <summary>Idée "Assistant Premier projet" — guide de création.</summary>
    public IReadOnlyList<string> GetFirstProjectQuestions()
    {
        if (!SettingsCatalog.Ai.Profiles.PedagogyMode.Value)
            return Array.Empty<string>();

        return new[]
        {
            "Quel type de projet ? (console/MAUI/jeu)",
            "Quel langage principal ? (C#/Python/autre)",
            "Quel niveau ? (débutant/intermédiaire/avancé)",
            "Quel thème ? (productivité/jeu/utilitaire)",
            "Combien de fichiers de départ ? (1-5)"
        };
    }

    /// <summary>Idée "Explain-only" — explique sans générer.</summary>
    public string ExplainCode(string code, string context)
    {
        if (!SettingsCatalog.Ai.Profiles.ExplainOnlyMode.Value)
            return "";

        // Analyse simple : compte classes, méthodes, complexité
        int classes = System.Text.RegularExpressions.Regex.Matches(code, @"\bclass\b").Count;
        int methods = System.Text.RegularExpressions.Regex.Matches(code, @"\b(public|private|protected)\s+\w+\s+\w+\s*\(").Count;

        return $"Ce code contient {classes} classe(s) et {methods} méthode(s). " +
               $"Contexte : {context}. Voulez-vous que j'explique une partie spécifique ?";
    }

    /// <summary>Idée "Tutoriel interactif" — missions guidées.</summary>
    public IReadOnlyList<TutorialMission> GetMissions()
    {
        if (!SettingsCatalog.Ai.Profiles.InteractiveTutorial.Value)
            return Array.Empty<TutorialMission>();
        return _missions;
    }

    public bool VerifyMission(string missionId, string userCode)
    {
        var mission = _missions.FirstOrDefault(m => m.Id == missionId);
        if (mission == null) return false;

        // Vérification simple : le code contient-il les éléments requis ?
        bool verified = userCode.Contains(mission.VerificationCode);
        if (verified)
        {
            mission.Completed = true;
            LogLearning($"Mission complétée : {mission.Title}");
        }
        return verified;
    }

    /// <summary>Idée "Glossaire dynamique" — concepts du projet.</summary>
    public IReadOnlyList<GlossaryEntry> GenerateGlossary(string workspacePath)
    {
        var entries = new List<GlossaryEntry>();
        if (!SettingsCatalog.Ai.Profiles.PedagogyMode.Value)
            return entries;

        // Scan simplifié des fichiers pour extraire les concepts
        var files = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files.Take(20)) // Limite pour performance
        {
            try
            {
                var content = File.ReadAllText(file);
                var classes = System.Text.RegularExpressions.Regex.Matches(content, @"\bclass\s+(\w+)");
                foreach (System.Text.RegularExpressions.Match match in classes)
                {
                    string className = match.Groups[1].Value;
                    entries.Add(new GlossaryEntry
                    {
                        Term = className,
                        Definition = $"Classe définie dans {Path.GetFileName(file)}",
                        FilePath = file
                    });
                }
            }
            catch { /* Ignore fichiers illisibles */ }
        }

        return entries.DistinctBy(e => e.Term).ToList();
    }

    /// <summary>Idée "Anti-magie noire" — explications obligatoires.</summary>
    public string AddExplanation(string generatedCode, string explanation)
    {
        if (!SettingsCatalog.Ai.Profiles.AntiMagicMode.Value)
            return generatedCode;

        return $"// 💡 {explanation}\n{generatedCode}";
    }

    /// <summary>Idée "Checklist de progression" — suivi des concepts.</summary>
    public LearningProgress GetProgress() => _progress;

    public void MarkConceptLearned(string concept)
    {
        _progress.Concepts[concept] = true;
        _progress.LastActivityUtc = DateTime.UtcNow;
        SaveProgress();
        LogLearning($"Concept appris : {concept}");
    }

    public void RecordError(string errorMessage)
    {
        if (_progress.FrequentErrors.Count > 10)
            _progress.FrequentErrors.RemoveAt(0);
        _progress.FrequentErrors.Add(errorMessage);
        SaveProgress();
    }

    /// <summary>Idée "Journal d'apprentissage" — learning.log.</summary>
    private void LogLearning(string entry)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {entry}\n";
            File.AppendAllText(LearningLogPath, line);
        }
        catch (Exception ex)
        {
            _log.Error("Pedagogy", "Échec log apprentissage", new { ex.Message });
        }
    }

    private void InitializeMissions()
    {
        _missions.AddRange(new[]
        {
            new TutorialMission { Id = "m1", Title = "Créer une classe", Description = "Créez une classe Player avec une propriété Name", VerificationCode = "class Player" },
            new TutorialMission { Id = "m2", Title = "Ajouter une méthode", Description = "Ajoutez une méthode Move() à Player", VerificationCode = "void Move()" },
            new TutorialMission { Id = "m3", Title = "Utiliser un event", Description = "Créez un event OnPlayerMoved", VerificationCode = "event Action" }
        });
    }

    private void LoadProgress()
    {
        if (!File.Exists(ProgressPath)) return;
        try
        {
            _progress = JsonSerializer.Deserialize<LearningProgress>(File.ReadAllText(ProgressPath)) ?? new();
        }
        catch { _progress = new(); }
    }

    private void SaveProgress()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProgressPath)!);
            File.WriteAllText(ProgressPath, JsonSerializer.Serialize(_progress));
        }
        catch (Exception ex)
        {
            _log.Error("Pedagogy", "Échec sauvegarde progrès", new { ex.Message });
        }
    }
}

public sealed class GlossaryEntry
{
    public string Term { get; set; } = "";
    public string Definition { get; set; } = "";
    public string? FilePath { get; set; }
}
