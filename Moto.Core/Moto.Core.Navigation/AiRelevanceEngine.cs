// Moto.Editor/Navigation/AiRelevanceEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Editor.Indexing;

namespace Moto.Editor.Navigation
{
    /// <summary>
    /// Moteur de détection des fichiers importants, cassés, incohérents.
    /// Utilise l'index pour éviter de re-scanner tout le projet.
    ///
    /// Critères de scoring :
    /// - Nombre de références entrantes (hub = important)
    /// - TODO / FIXME / HACK (à améliorer)
    /// - Erreurs de syntaxe basiques (cassé)
    /// - Interfaces sans implémentation (incohérent)
    /// - Taille anormale (candidat refactor)
    /// </summary>
    public class AiRelevanceEngine
    {
        private readonly ProjectIndex _index;

        private static readonly Regex TodoRegex = new Regex(
            @"\b(TODO|FIXME|HACK|BUG|XXX)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex InterfaceRegex = new Regex(
            @"\binterface\s+(\w+)",
            RegexOptions.Compiled
        );

        private static readonly Regex ClassRegex = new Regex(
            @"\bclass\s+(\w+)",
            RegexOptions.Compiled
        );

        public AiRelevanceEngine(ProjectIndex index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        /// <summary>
        /// Analyse tous les fichiers indexés et produit un rapport de pertinence.
        /// </summary>
        public RelevanceReport Analyze(string rootPath)
        {
            var report = new RelevanceReport();

            var files = _index.FindByKind(SymbolKind.Class, 10000)
                              .Concat(_index.FindByKind(SymbolKind.Interface, 10000))
                              .Concat(_index.FindByKind(SymbolKind.System, 10000))
                              .Select(e => e.FilePath)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            report.TotalFilesAnalyzed = files.Count;

            foreach (var filePath in files)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    var content = File.ReadAllText(filePath);
                    var fileInfo = new FileInfo(filePath);

                    double score = 0;
                    var reasons = new List<(RelevanceReason reason, double weight, string explanation)>();

                    // 1. TODO / FIXME / HACK
                    var todoMatches = TodoRegex.Matches(content);
                    if (todoMatches.Count > 0)
                    {
                        double weight = Math.Min(todoMatches.Count * 0.15, 0.6);
                        score += weight;
                        reasons.Add((RelevanceReason.NeedsImprovement, weight,
                            $"{todoMatches.Count} marqueur(s) TODO/FIXME détecté(s)."));
                    }

                    // 2. Erreurs de syntaxe basiques
                    int openBraces = CountChar(content, '{');
                    int closeBraces = CountChar(content, '}');
                    if (Math.Abs(openBraces - closeBraces) > 2)
                    {
                        score += 0.8;
                        reasons.Add((RelevanceReason.Broken, 0.8,
                            $"Accolades déséquilibrées ({openBraces} ouvertes, {closeBraces} fermées)."));
                    }

                    // 3. Interface sans implémentation
                    foreach (Match match in InterfaceRegex.Matches(content))
                    {
                        var interfaceName = match.Groups[1].Value;
                        var implementations = _index.FindByName(interfaceName.TrimStart('I'));

                        if (implementations.Count == 0)
                        {
                            score += 0.5;
                            reasons.Add((RelevanceReason.Inconsistent, 0.5,
                                $"Interface '{interfaceName}' sans implémentation détectée."));
                        }
                    }

                    // 4. Fichier trop gros (candidat refactor)
                    if (fileInfo.Length > 50 * 1024)
                    {
                        double weight = Math.Min(fileInfo.Length / (200.0 * 1024), 0.4);
                        score += weight;
                        reasons.Add((RelevanceReason.NeedsImprovement, weight,
                            $"Fichier volumineux ({fileInfo.Length / 1024} Ko). Candidat au découpage."));
                    }

                    // 5. Nombre de références entrantes (hub)
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var referencingEntries = _index.FindByName(fileName);
                    if (referencingEntries.Count > 5)
                    {
                        double weight = Math.Min(referencingEntries.Count * 0.05, 0.5);
                        score += weight;
                        reasons.Add((RelevanceReason.Important, weight,
                            $"{referencingEntries.Count} référence(s) entrante(s). Fichier central."));
                    }

                    // Classement du fichier
                    if (score > 0)
                    {
                        var primaryReason = reasons.OrderByDescending(r => r.weight).First().reason;
                        var level = score >= 0.8 ? RelevanceLevel.Critical
                                  : score >= 0.5 ? RelevanceLevel.High
                                  : score >= 0.2 ? RelevanceLevel.Medium
                                  : RelevanceLevel.Low;

                        var explanation = string.Join(" ", reasons.Select(r => r.explanation));

                        report.Entries.Add(new RelevanceEntry
                        {
                            FilePath = filePath,
                            Level = level,
                            Reason = primaryReason,
                            Score = score,
                            Explanation = explanation
                        });
                    }
                }
                catch
                {
                    // Fichier illisible : on l'ignore.
                }
            }

            return report;
        }

        private static int CountChar(string text, char c)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == c)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
