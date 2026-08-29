// Moto.Core/AI/Context/ContextAnalyzer.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.AutoLink;
using Moto.Core.AI.Internal;

namespace Moto.Core.AI.Context
{
    /// <summary>
    /// Analyse multi-facteurs du contexte : fichier, projet, patterns, habitudes.
    /// Combine AutoLinkEngine + ProjectUnderstandingEngine + PatternDetectorEngine.
    /// </summary>
    public class ContextAnalyzer
    {
        private readonly AutoLinkEngine _autoLink = new();
        private readonly ProjectUnderstandingEngine _understanding = new();
        private readonly PatternDetectorEngine _patterns = new();

        public ContextReport Analyze(string filePath, string workspace)
        {
            var report = new ContextReport { FilePath = filePath };

            if (!File.Exists(filePath)) return report;

            var content = File.ReadAllText(filePath);
            var map = _understanding.BuildMap(workspace);

            // 1. Analyse AutoLink (dépendances manquantes)
            var autoLinkReport = _autoLink.Analyze(filePath);

            foreach (var action in autoLinkReport.Actions)
            {
                var suggestion = ConvertAutoLinkAction(action);
                report.Suggestions.Add(suggestion);
            }

            // 2. Analyse des patterns incomplets
            var patternReport = _patterns.Analyze(map);

            foreach (var suggestion in patternReport.Suggestions.Take(5))
            {
                report.Suggestions.Add(new ContextSuggestion
                {
                    Kind = ContextSuggestionKind.FixPattern,
                    Priority = ContextSuggestionPriority.Medium,
                    Title = suggestion.Title,
                    Description = suggestion.Detail,
                    Confidence = 0.7
                });
            }

            // 3. Analyse du fichier actif : commentaires manquants, variables mal nommées
            AnalyzeFileContent(filePath, content, report);

            // 4. Analyse des systèmes non connectés
            AnalyzeSystems(map, report);

            // Tri par priorité + confiance
            report.Suggestions.Sort((a, b) =>
            {
                var priorityCompare = b.Priority.CompareTo(a.Priority);
                return priorityCompare != 0 ? priorityCompare : b.Confidence.CompareTo(a.Confidence);
            });

            report.TotalIssues = report.Suggestions.Count;

            return report;
        }

        private ContextSuggestion ConvertAutoLinkAction(AutoLinkAction action)
        {
            var kind = action.Kind switch
            {
                AutoLinkIssueKind.MissingClass => ContextSuggestionKind.CreateFile,
                AutoLinkIssueKind.MissingInterface => ContextSuggestionKind.CreateFile,
                AutoLinkIssueKind.MissingSystem => ContextSuggestionKind.ConnectSystem,
                AutoLinkIssueKind.MissingUsing => ContextSuggestionKind.AddUsing,
                AutoLinkIssueKind.IncompleteClass => ContextSuggestionKind.CompleteInterface,
                _ => ContextSuggestionKind.CreateFile
            };

            return new ContextSuggestion
            {
                Kind = kind,
                Priority = kind == ContextSuggestionKind.CreateFile ? ContextSuggestionPriority.High : ContextSuggestionPriority.Medium,
                Title = action.Title,
                Description = action.Description,
                GeneratedContent = action.GeneratedContent,
                TargetPath = action.TargetPath,
                IsInsertion = action.IsInsertion,
                Confidence = 0.85
            };
        }

        private void AnalyzeFileContent(string filePath, string content, ContextReport report)
        {
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                // 1. Méthodes sans commentaires XML
                if (Regex.IsMatch(line, @"\b(public|private|protected|internal)\s+\w+\s+\w+\s*\(") &&
                    !line.TrimStart().StartsWith("///"))
                {
                    // Vérifie si la ligne précédente est un commentaire
                    if (i > 0 && !lines[i - 1].TrimStart().StartsWith("///"))
                    {
                        report.Suggestions.Add(new ContextSuggestion
                        {
                            Kind = ContextSuggestionKind.AddComment,
                            Priority = ContextSuggestionPriority.Low,
                            Title = "Ajouter un commentaire XML",
                            Description = $"La méthode ligne {lineNumber} n'a pas de documentation.",
                            FilePath = filePath,
                            Line = lineNumber,
                            GeneratedContent = "/// <summary>\n/// TODO : documenter cette méthode.\n/// </summary>\n",
                            IsInsertion = true,
                            Confidence = 0.6
                        });
                    }
                }

                // 2. Variables mal nommées (single letter sauf i, j, k dans les boucles)
                var varMatches = Regex.Matches(line, @"\b(var|[A-Z]\w*)\s+([a-z])\s*=");

                foreach (Match match in varMatches)
                {
                    var varName = match.Groups[2].Value;

                    if (varName.Length == 1 && !"ijk".Contains(varName))
                    {
                        report.Suggestions.Add(new ContextSuggestion
                        {
                            Kind = ContextSuggestionKind.RenameVariable,
                            Priority = ContextSuggestionPriority.Low,
                            Title = $"Renommer la variable '{varName}'",
                            Description = "Les variables d'une seule lettre sont difficiles à lire.",
                            FilePath = filePath,
                            Line = lineNumber,
                            Confidence = 0.5
                        });
                    }
                }

                // 3. Méthodes trop longues (> 50 lignes)
                if (Regex.IsMatch(line, @"\b(public|private|protected|internal)\s+\w+\s+\w+\s*\("))
                {
                    var methodStart = i;
                    var braceCount = 0;
                    var methodEnd = i;

                    for (int j = i; j < lines.Length; j++)
                    {
                        braceCount += lines[j].Count(c => c == '{');
                        braceCount -= lines[j].Count(c => c == '}');

                        if (braceCount == 0 && j > i)
                        {
                            methodEnd = j;
                            break;
                        }
                    }

                    var methodLength = methodEnd - methodStart;

                    if (methodLength > 50)
                    {
                        report.Suggestions.Add(new ContextSuggestion
                        {
                            Kind = ContextSuggestionKind.OptimizeCode,
                            Priority = ContextSuggestionPriority.Medium,
                            Title = "Méthode trop longue",
                            Description = $"Cette méthode fait {methodLength} lignes. Découpe-la en sous-méthodes.",
                            FilePath = filePath,
                            Line = lineNumber,
                            Confidence = 0.75
                        });
                    }
                }
            }
        }

        private void AnalyzeSystems(ProjectMap map, ContextReport report)
        {
            var systems = map.Symbols.Where(s => s.Kind == SymbolKind.System).ToList();
            var components = map.Symbols.Where(s => s.Kind == SymbolKind.Component).ToList();

            // Systèmes sans composants correspondants
            foreach (var system in systems)
            {
                var baseName = system.Name.Replace("System", "");

                if (!components.Any(c => c.Name.Contains(baseName)))
                {
                    report.Suggestions.Add(new ContextSuggestion
                    {
                        Kind = ContextSuggestionKind.GenerateModule,
                        Priority = ContextSuggestionPriority.Medium,
                        Title = $"Créer {baseName}Component",
                        Description = $"Le système {system.Name} n'a pas de composant de données.",
                        Confidence = 0.7
                    });
                }
            }

            // Composants sans systèmes correspondants
            foreach (var component in components)
            {
                var baseName = component.Name.Replace("Component", "");

                if (!systems.Any(s => s.Name.Contains(baseName)))
                {
                    report.Suggestions.Add(new ContextSuggestion
                    {
                        Kind = ContextSuggestionKind.GenerateModule,
                        Priority = ContextSuggestionPriority.Medium,
                        Title = $"Créer {baseName}System",
                        Description = $"Le composant {component.Name} n'a pas de système logique.",
                        Confidence = 0.7
                    });
                }
            }
        }
    }
}
