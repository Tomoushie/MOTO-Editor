// Moto.Core/AI/Internal/HealthMetricsEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// 28. Métriques étendues de santé : complexité, cohérence, architecture.
    /// Complète le HealthMonitorEngine existant (duplication, erreurs...).
    /// </summary>
    public class HealthMetrics
    {
        public int ComplexityScore { get; set; } = 100;
        public int ConsistencyScore { get; set; } = 100;
        public int ArchitectureScore { get; set; } = 100;
        public int GlobalScore { get; set; } = 100;
        public double AvgCyclomatic { get; set; }
        public List<string> Details { get; } = new();
    }

    public class HealthMetricsEngine
    {
        private static readonly string[] BranchTokens =
        {
            "if ", "for ", "foreach", "while ", "switch", "case ", "&&", "||", "catch"
        };

        public HealthMetrics Analyze(ProjectMap map)
        {
            var metrics = new HealthMetrics();

            AnalyzeComplexity(map, metrics);
            AnalyzeConsistency(map, metrics);
            AnalyzeArchitecture(map, metrics);

            metrics.GlobalScore = (metrics.ComplexityScore + metrics.ConsistencyScore + metrics.ArchitectureScore) / 3;

            return metrics;
        }

        /// <summary>Complexité cyclomatique moyenne (branches par fichier).</summary>
        private void AnalyzeComplexity(ProjectMap map, HealthMetrics metrics)
        {
            var complexities = new List<int>();

            foreach (var file in map.Files.Where(f => f.EndsWith(".cs")).Take(300))
            {
                string text;

                try { text = File.ReadAllText(file); }
                catch { continue; }

                int cc = 1;

                foreach (var token in BranchTokens)
                {
                    int i = 0;

                    while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0)
                    {
                        cc++;
                        i += token.Length;
                    }
                }

                complexities.Add(cc);
            }

            if (complexities.Count > 0)
            {
                metrics.AvgCyclomatic = complexities.Average();

                // Au-delà de 4 branches/méthode en moyenne, ça se dégrade.
                metrics.ComplexityScore = Math.Clamp(100 - (int)Math.Max(0, metrics.AvgCyclomatic - 4) * 8, 0, 100);
            }
        }

        /// <summary>Cohérence : chaque système a-t-il interface + composant ?</summary>
        private void AnalyzeConsistency(ProjectMap map, HealthMetrics metrics)
        {
            var systems = map.Symbols.Where(s => s.Kind == SymbolKind.System).ToList();

            if (systems.Count == 0) return;

            var interfaces = map.Symbols.Where(s => s.Kind == SymbolKind.Interface).Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var components = map.Symbols.Where(s => s.Kind == SymbolKind.Component).Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            int complete = 0;

            foreach (var system in systems)
            {
                var baseName = system.Name.Replace("System", "");

                bool hasInterface = interfaces.Any(i => i.Contains(baseName));
                bool hasComponent = components.Any(c => c.Contains(baseName));

                if (hasInterface && hasComponent) complete++;
            }

            metrics.ConsistencyScore = complete * 100 / systems.Count;

            if (metrics.ConsistencyScore < 100)
            {
                metrics.Details.Add($"{systems.Count - complete} système(s) sans triplet ECS complet.");
            }
        }

        /// <summary>Architecture : les symboles sont-ils dans les dossiers conventionnels ?</summary>
        private void AnalyzeArchitecture(ProjectMap map, HealthMetrics metrics)
        {
            var typed = map.Symbols.Where(s =>
                s.Kind == SymbolKind.System ||
                s.Kind == SymbolKind.Component ||
                s.Kind == SymbolKind.Interface).ToList();

            if (typed.Count == 0) return;

            int inPlace = typed.Count(s =>
                s.FilePath.Contains("Systems") || s.FilePath.Contains("Components") ||
                s.FilePath.Contains("Interfaces") || s.FilePath.Contains("Modules") ||
                s.FilePath.Contains("Behaviors"));

            metrics.ArchitectureScore = inPlace * 100 / typed.Count;

            if (metrics.ArchitectureScore < 100)
            {
                metrics.Details.Add($"{typed.Count - inPlace} symbole(s) hors dossiers conventionnels.");
            }
        }
    }
}
