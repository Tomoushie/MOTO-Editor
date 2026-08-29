// Moto.Core/AI/Internal/SearchEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur de recherche IA interne.
    /// Permet de naviguer intelligemment dans le projet.
    /// "Où est utilisé ce système ?", "Où est définie cette classe ?", etc.
    /// </summary>
    public class SearchEngine
    {
        /// <summary>
        /// Résultat de recherche.
        /// </summary>
        public class SearchResult
        {
            public string FilePath { get; set; } = string.Empty;
            public int Line { get; set; }
            public string MatchedText { get; set; } = string.Empty;
            public string SymbolName { get; set; } = string.Empty;
            public SymbolKind Kind { get; set; }
            public double Relevance { get; set; }
        }

        /// <summary>
        /// Recherche où un symbole est défini.
        /// </summary>
        public List<SearchResult> FindDefinition(ProjectMap map, string symbolName)
        {
            return map.Symbols
                .Where(s => s.Name.Contains(symbolName, StringComparison.OrdinalIgnoreCase))
                .Select(s => new SearchResult
                {
                    FilePath = s.FilePath,
                    Line = s.Line,
                    SymbolName = s.Name,
                    Kind = s.Kind,
                    MatchedText = $"Définition de {s.Kind} {s.Name}",
                    Relevance = 1.0
                })
                .OrderByDescending(r => r.Relevance)
                .Take(20)
                .ToList();
        }

        /// <summary>
        /// Recherche où un symbole est utilisé.
        /// </summary>
        public List<SearchResult> FindUsages(ProjectMap map, string symbolName)
        {
            var results = new List<SearchResult>();

            foreach (var relation in map.Relations)
            {
                var matchingRelations = relation.Value
                    .Where(r => r.Contains(symbolName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var match in matchingRelations)
                {
                    results.Add(new SearchResult
                    {
                        FilePath = relation.Key,
                        SymbolName = symbolName,
                        MatchedText = match,
                        Relevance = 0.8
                    });
                }
            }

            return results
                .OrderByDescending(r => r.Relevance)
                .Take(30)
                .ToList();
        }

        /// <summary>
        /// Recherche tous les fichiers liés à un module.
        /// </summary>
        public List<SearchResult> FindModuleFiles(ProjectMap map, string moduleName)
        {
            return map.Files
                .Where(f => f.Contains(moduleName, StringComparison.OrdinalIgnoreCase))
                .Select(f => new SearchResult
                {
                    FilePath = f,
                    SymbolName = moduleName,
                    MatchedText = $"Fichier du module {moduleName}",
                    Relevance = 0.9
                })
                .Take(50)
                .ToList();
        }

        /// <summary>
        /// Recherche les fichiers cassés ou à problème.
        /// </summary>
        public List<SearchResult> FindBrokenFiles(ProjectMap map)
        {
            return map.Issues
                .Where(i => i.Severity == IssueSeverity.Error || i.Severity == IssueSeverity.Warning)
                .Select(i => new SearchResult
                {
                    FilePath = i.FilePath,
                    SymbolName = i.SymbolName,
                    MatchedText = i.Message,
                    Relevance = i.Severity == IssueSeverity.Error ? 1.0 : 0.7
                })
                .OrderByDescending(r => r.Relevance)
                .Take(30)
                .ToList();
        }

        /// <summary>
        /// Recherche les fichiers importants (hubs de dépendances).
        /// </summary>
        public List<SearchResult> FindImportantFiles(ProjectMap map)
        {
            var fileScores = new Dictionary<string, double>();

            foreach (var relation in map.Relations)
            {
                if (!fileScores.ContainsKey(relation.Key))
                {
                    fileScores[relation.Key] = 0;
                }

                fileScores[relation.Key] += relation.Value.Count;
            }

            return fileScores
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new SearchResult
                {
                    FilePath = kv.Key,
                    MatchedText = $"Fichier référencé {kv.Value} fois.",
                    Relevance = kv.Value / 10.0
                })
                .ToList();
        }
    }
}
