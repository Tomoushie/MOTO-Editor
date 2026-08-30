// Moto.Core/AI/Cortex/CortexEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Core.AI.Internal;
using Moto.Core.Settings;

namespace Moto.Core.AI.Cortex
{
    /// <summary>
    /// Stats agrégées du moteur cognitif. Utilisé par RefreshHomeStats() pour affichage.
    /// Record immuable pour garantir la thread-safety lors de la lecture des stats.
    /// </summary>
    public sealed record CortexStats(
        int TotalHabits,       // patterns comportementaux appris
        int TotalPatterns,     // patterns de code/style détectés
        int TotalCorrections,  // corrections appliquées via StyleLearner
        double ConfidenceAvg   // confiance moyenne [0..1]
    )
    {
        /// <summary>Horodatage de la dernière activité Cortex (utilisé par AIWorkspace.WorkspaceStats).</summary>
        public DateTime LastActivity { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// MOTO AI v3 — Cortex Engine : cerveau logique évolutif.
    /// Mémoire cognitive + apprentissage de style + comportement adaptatif.
    /// </summary>
    public partial class CortexEngine : IDisposable
    {
        private readonly CortexMemory _memory;
        private readonly StyleLearner _styleLearner;
        private readonly ProjectUnderstandingEngine _understanding = new();
        private CortexBehaviorConfig _behaviorConfig;

        public event Action<CortexBehaviorConfig>? BehaviorChanged;
        public event Action<string>? MemoryUpdated;

        public CortexEngine(string workspace)
        {
            _memory = new CortexMemory(workspace);
            _styleLearner = new StyleLearner(_memory);
            _behaviorConfig = CortexBehaviorConfig.ForMode(CortexBehaviorMode.Balanced);

            var modeStr = SettingsEngine.Shared.GetString("cortex_mode");
            var mode = modeStr switch
            {
                "Beginner" => CortexBehaviorMode.Beginner,
                "Expert" => CortexBehaviorMode.Expert,
                "Turbo" => CortexBehaviorMode.Turbo,
                "Ultra" => CortexBehaviorMode.Ultra,
                _ => CortexBehaviorMode.Balanced
            };
            SetBehaviorMode(mode);
        }

        public void SetBehaviorMode(CortexBehaviorMode mode)
        {
            _behaviorConfig = CortexBehaviorConfig.ForMode(mode);
            BehaviorChanged?.Invoke(_behaviorConfig);
        }

        public void LearnFromCode(string filePath, string content)
        {
            _styleLearner.LearnFromFile(filePath, content);
            MemoryUpdated?.Invoke($"Appris de {Path.GetFileName(filePath)}");
        }

        public void RecordCorrection(string before, string after, string reason = "")
        {
            _memory.RecordCorrection(before, after, reason);
            MemoryUpdated?.Invoke("Correction enregistrée");
        }

        public void RecordPattern(string signature, string example, string context = "")
        {
            _memory.RecordPattern(signature, example, context);
            MemoryUpdated?.Invoke("Pattern enregistré");
        }

        public string GenerateCode(string intent, string context = "")
        {
            var conventions = _memory.GetNamingConventions();
            var habits = _memory.GetHabits("type_usage");
            var patterns = _memory.GetPatterns(5);

            var code = GenerateBase(intent, context);
            code = ApplyNamingConventions(code, conventions);
            code = ApplyTypePreferences(code, habits);
            code = ApplyPatternPreferences(code, patterns);
            return code;
        }

        public List<CortexSuggestion> GetSuggestions(string filePath, string content)
        {
            var suggestions = new List<CortexSuggestion>();
            var conventions = _memory.GetNamingConventions();
            var corrections = _memory.GetCorrections(5);

            if (conventions.TryGetValue("variable", out var varConvention) && varConvention == "camelCase")
            {
                foreach (var v in FindNonCamelCaseVariables(content))
                {
                    suggestions.Add(new CortexSuggestion
                    {
                        Kind = SuggestionKind.Rename,
                        Title = $"Renommer '{v}' en camelCase",
                        Description = "Tu utilises habituellement camelCase pour les variables.",
                        Confidence = 0.8
                    });
                }
            }

            foreach (var correction in corrections)
            {
                if (content.Contains(correction.Before))
                {
                    suggestions.Add(new CortexSuggestion
                    {
                        Kind = SuggestionKind.Fix,
                        Title = $"Corriger : {correction.Before}",
                        Description = $"Tu as déjà corrigé cela : {correction.Reason}",
                        Confidence = 0.9
                    });
                }
            }

            return suggestions
                .Where(s => s.Confidence >= _behaviorConfig.ConfidenceThreshold)
                .OrderByDescending(s => s.Confidence)
                .Take(10)
                .ToList();
        }

        /// <summary>
        /// Retourne un snapshot immuable des stats du moteur cognitif.
        /// </summary>
        public CortexStats GetStats()
        {
            return new CortexStats(
                TotalHabits: _memory.Habits.Count,
                TotalPatterns: _memory.Patterns.Count,
                TotalCorrections: _styleLearner.CorrectionsApplied,
                ConfidenceAvg: _styleLearner.AverageConfidence
            );
        }

        public CortexBehaviorConfig CurrentBehavior => _behaviorConfig;

        // --- Méthodes internes de génération (simplifiées pour l'exemple) ---
        private string GenerateBase(string intent, string context) => $"// Génération pour : {intent}";
        private string ApplyNamingConventions(string code, Dictionary<string, string> conventions) => code;
        private string ApplyTypePreferences(string code, Dictionary<string, double> habits) => code;
        private string ApplyPatternPreferences(string code, List<PatternRecordLegacy> patterns) => code;

        private List<string> FindNonCamelCaseVariables(string content)
        {
            var badVars = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"\b(var|[A-Z]\w*)\s+([A-Z_][a-zA-Z0-9_]*)\s*=");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var varName = m.Groups[2].Value;
                if (char.IsUpper(varName[0]) || varName.Contains("_"))
                    badVars.Add(varName);
            }
            return badVars.Distinct().ToList();
        }

        public void Dispose() { /* Cleanup si nécessaire */ }
    }

    public enum SuggestionKind { Rename, Fix, Refactor, Generate, Document }

    public class CortexSuggestion
    {
        public SuggestionKind Kind { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }

        /// <summary>Extrait de code généré associé à la suggestion (snippet), si applicable.</summary>
        public string? GeneratedContent { get; set; }
    }
}
