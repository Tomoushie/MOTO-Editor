// Moto.Core/AI/Internal/HealthMonitorEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    public enum HealthCategory
    {
        Structure, Duplication, Dependencies, Patterns, SilentErrors, FutureRisk
    }

    public class HealthIssue
    {
        public HealthCategory Category { get; set; }
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }

    public class HealthReport
    {
        public int GlobalScore { get; set; } = 100;
        public Dictionary<HealthCategory, int> CategoryScores { get; } = new Dictionary<HealthCategory, int>();
        public List<HealthIssue> Issues { get; } = new List<HealthIssue>();
        public List<AiSuggestion> Proposals { get; } = new List<AiSuggestion>();
    }

    /// <summary>
    /// AI Project Health Monitor : score de santé global et par catégorie,
    /// détection de duplications, erreurs silencieuses, modules inutilisés, risques futurs.
    /// </summary>
    public class HealthMonitorEngine
    {
        private static readonly Regex EmptyCatchRegex = new Regex(
            @"catch\s*(\([^)]*\))?\s*\{\s*\}",
            RegexOptions.Compiled);

        /// <summary>Analyse complète du projet.</summary>
        public HealthReport Analyze(ProjectMap map)
        {
            var report = new HealthReport();

            AnalyzeStructure(map, report);
            AnalyzeFiles(map, report);
            AnalyzeUnusedModules(map, report);
            AnalyzeFutureRisks(map, report);

            ComputeScores(report);
            BuildProposals(report);

            return report;
        }

        private void AnalyzeStructure(ProjectMap map, HealthReport report)
        {
            foreach (var issue in map.Issues)
            {
                report.Issues.Add(new HealthIssue
                {
                    Category = issue.Kind == IssueKind.MissingImplementation ||
                               issue.Kind == IssueKind.MissingInterfaceForSystem
                        ? HealthCategory.Patterns
                        : HealthCategory.Structure,
                    Severity = issue.Severity.ToString().ToLowerInvariant(),
                    Message = issue.Message,
                    FilePath = issue.FilePath,
                    Suggestion = "Corriger via AutoFix."
                });
            }
        }

        private void AnalyzeFiles(ProjectMap map, HealthReport report)
        {
            var blockMap = new Dictionary<string, List<(string File, int Line)>>();

            foreach (var filePath in map.Files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Take(300))
            {
                string text;

                try
                {
                    text = File.ReadAllText(filePath);
                }
                catch
                {
                    continue;
                }

                // Erreurs silencieuses : catch vides, NotImplementedException.
                foreach (Match m in EmptyCatchRegex.Matches(text))
                {
                    report.Issues.Add(new HealthIssue
                    {
                        Category = HealthCategory.SilentErrors,
                        Severity = "warning",
                        Message = "Catch vide : erreur avalée silencieusement.",
                        FilePath = filePath,
                        Suggestion = "Logger ou propager l'erreur."
                    });
                }

                if (text.Contains("NotImplementedException"))
                {
                    report.Issues.Add(new HealthIssue
                    {
                        Category = HealthCategory.SilentErrors,
                        Severity = "warning",
                        Message = "Méthode non implémentée (NotImplementedException).",
                        FilePath = filePath,
                        Suggestion = "Compléter l'implémentation."
                    });
                }

                // Duplication : blocs de 6 lignes identiques.
                var lines = text.Split('\n');

                for (int i = 0; i + 6 < lines.Length; i++)
                {
                    var block = string.Join("|", lines
                        .Skip(i).Take(6)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 10));

                    if (block.Length < 40)
                    {
                        continue;
                    }

                    if (!blockMap.TryGetValue(block, out var list))
                    {
                        list = new List<(string, int)>();
                        blockMap[block] = list;
                    }

                    list.Add((filePath, i + 1));
                }
            }

            // Blocs présents à plusieurs endroits = duplication.
            foreach (var kv in blockMap.Where(kv => kv.Value.Count > 1).Take(20))
            {
                var first = kv.Value[0];

                report.Issues.Add(new HealthIssue
                {
                    Category = HealthCategory.Duplication,
                    Severity = "info",
                    Message = $"Code dupliqué ({kv.Value.Count} occurrences).",
                    FilePath = first.File,
                    Suggestion = "Extraire une méthode ou un module partagé."
                });
            }
        }

        private void AnalyzeUnusedModules(ProjectMap map, HealthReport report)
        {
            foreach (var module in map.Modules)
            {
                var moduleSymbols = map.Symbols
                    .Where(s => s.FilePath.Contains(module, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (moduleSymbols.Count == 0)
                {
                    continue;
                }

                bool referenced = map.Relations.Any(r =>
                    !r.Key.Contains(module, StringComparison.OrdinalIgnoreCase) &&
                    r.Value.Any(v => moduleSymbols.Any(sym => v.Contains(sym, StringComparison.OrdinalIgnoreCase))));

                if (!referenced)
                {
                    report.Issues.Add(new HealthIssue
                    {
                        Category = HealthCategory.Dependencies,
                        Severity = "info",
                        Message = $"Module '{module}' jamais référencé ailleurs.",
                        FilePath = module,
                        Suggestion = "Connecter le module ou le supprimer."
                    });
                }
            }
        }

        private void AnalyzeFutureRisks(ProjectMap map, HealthReport report)
        {
            foreach (var kv in map.FileLineCounts.Where(kv => kv.Value > 500))
            {
                report.Issues.Add(new HealthIssue
                {
                    Category = HealthCategory.FutureRisk,
                    Severity = "warning",
                    Message = $"Fichier volumineux ({kv.Value} lignes) : risque de maintenance.",
                    FilePath = kv.Key,
                    Suggestion = "Découper en plusieurs fichiers."
                });
            }
        }

        private void ComputeScores(HealthReport report)
        {
            var categories = Enum.GetValues(typeof(HealthCategory)).Cast<HealthCategory>().ToList();

            foreach (var category in categories)
            {
                var issues = report.Issues.Where(i => i.Category == category).ToList();

                int penalty = issues.Sum(i =>
                    i.Severity == "error" ? 15 : i.Severity == "warning" ? 7 : 3);

                report.CategoryScores[category] = Math.Max(0, 100 - penalty);
            }

            report.GlobalScore = (int)categories.Average(c => report.CategoryScores[c]);
        }

        private void BuildProposals(HealthReport report)
        {
            foreach (var issue in report.Issues.Take(15))
            {
                report.Proposals.Add(new AiSuggestion
                {
                    Title = $"{issue.Category} : {issue.Suggestion}",
                    Detail = issue.Message,
                    ActionId = "health.fix"
                });
            }
        }
    }
}
