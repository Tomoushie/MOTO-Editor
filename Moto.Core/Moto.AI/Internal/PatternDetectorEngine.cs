// Moto.Core/AI/Internal/PatternDetectorEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    public enum PatternKind { ECS, Singleton, Factory, Observer, DependencyInjection, Pipeline }

    public class DetectedPattern
    {
        public PatternKind Kind { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Files { get; } = new List<string>();
        public double Completeness { get; set; } = 1.0;
        public string Issue { get; set; } = string.Empty;
    }

    public class PatternReport
    {
        public List<DetectedPattern> Detected { get; } = new List<DetectedPattern>();
        public List<AiSuggestion> Suggestions { get; } = new List<AiSuggestion>();
    }

    /// <summary>
    /// AI Pattern Detector : détecte les patterns utilisés, manquants,
    /// incomplets ou incohérents, et propose des créations ciblées.
    /// </summary>
    public class PatternDetectorEngine
    {
        private static readonly Regex SingletonRegex = new Regex(
            @"\b(static\s+\w+\s+Instance|private\s+static\s+\w+\s+_instance)\b",
            RegexOptions.Compiled);

        private static readonly Regex FactoryRegex = new Regex(
            @"\bpublic\s+\w+\s+(Create|Build)\w*\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex ObserverRegex = new Regex(
            @"\bevent\s+(Action|EventHandler|Func)",
            RegexOptions.Compiled);

        private static readonly Regex NewInstanceRegex = new Regex(
            @"\bnew\s+([A-Z]\w+)\s*\(",
            RegexOptions.Compiled);

        public PatternReport Analyze(ProjectMap map)
        {
            var report = new PatternReport();

            DetectEcs(map, report);
            DetectCodePatterns(map, report);
            DetectMissingFactory(map, report);

            return report;
        }

        private void DetectEcs(ProjectMap map, PatternReport report)
        {
            var systems = map.Symbols.Where(s => s.Kind == SymbolKind.System).ToList();
            var components = map.Symbols.Where(s => s.Kind == SymbolKind.Component).ToList();
            var interfaces = map.Symbols.Where(s => s.Kind == SymbolKind.Interface).ToList();

            foreach (var system in systems)
            {
                var baseName = system.Name.Replace("System", "");

                var hasComponent = components.Any(c => c.Name.Contains(baseName));
                var hasInterface = interfaces.Any(i => i.Name.Contains(baseName));

                var pattern = new DetectedPattern
                {
                    Kind = PatternKind.ECS,
                    Name = baseName,
                    Completeness = (hasComponent ? 0.5 : 0) + (hasInterface ? 0.5 : 0),
                    Files = { system.FilePath }
                };

                if (!hasComponent)
                {
                    pattern.Issue = $"Composant manquant pour {system.Name}.";
                    report.Suggestions.Add(new AiSuggestion
                    {
                        Title = $"Créer {baseName}Component",
                        Detail = $"Le système {system.Name} n'a pas de composant de données.",
                        ActionId = "pattern.component"
                    });
                }

                if (!hasInterface)
                {
                    pattern.Issue = $"Interface manquante pour {system.Name}.";
                    report.Suggestions.Add(new AiSuggestion
                    {
                        Title = $"Créer I{system.Name}",
                        Detail = $"Le système {system.Name} n'a pas de contrat d'interface.",
                        ActionId = "pattern.interface"
                    });
                }

                report.Detected.Add(pattern);
            }
        }

        private void DetectCodePatterns(ProjectMap map, PatternReport report)
        {
            foreach (var filePath in map.Files.Where(f => f.EndsWith(".cs")).Take(300))
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

                if (SingletonRegex.IsMatch(text))
                {
                    report.Detected.Add(new DetectedPattern
                    {
                        Kind = PatternKind.Singleton,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Files = { filePath }
                    });
                }

                if (FactoryRegex.IsMatch(text))
                {
                    report.Detected.Add(new DetectedPattern
                    {
                        Kind = PatternKind.Factory,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Files = { filePath }
                    });
                }

                if (ObserverRegex.IsMatch(text))
                {
                    report.Detected.Add(new DetectedPattern
                    {
                        Kind = PatternKind.Observer,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Files = { filePath }
                    });
                }

                if (text.Contains("Pipeline"))
                {
                    report.Detected.Add(new DetectedPattern
                    {
                        Kind = PatternKind.Pipeline,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Files = { filePath }
                    });
                }
            }
        }

        /// <summary>
        /// Une classe instanciée à 3+ endroits différents sans Factory
        /// = candidat à une Factory.
        /// </summary>
        private void DetectMissingFactory(ProjectMap map, PatternReport report)
        {
            var instantiations = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in map.Files.Where(f => f.EndsWith(".cs")).Take(300))
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

                foreach (Match m in NewInstanceRegex.Matches(text))
                {
                    var className = m.Groups[1].Value;

                    if (!instantiations.TryGetValue(className, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        instantiations[className] = set;
                    }

                    set.Add(filePath);
                }
            }

            foreach (var kv in instantiations.Where(kv => kv.Value.Count >= 3))
            {
                bool hasFactory = map.Symbols.Any(s =>
                    s.Kind == SymbolKind.Class &&
                    s.Name.Contains("Factory", StringComparison.OrdinalIgnoreCase) &&
                    s.Name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));

                if (!hasFactory)
                {
                    report.Suggestions.Add(new AiSuggestion
                    {
                        Title = $"Tu devrais créer une Factory pour {kv.Key}",
                        Detail = $"Instantié dans {kv.Value.Count} fichiers différents.",
                        ActionId = "pattern.factory"
                    });
                }
            }
        }
    }
}
