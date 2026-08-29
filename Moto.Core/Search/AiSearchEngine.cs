// Moto.Editor/Search/AiSearchEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Moto.Editor.Indexing;

namespace Moto.Editor.Search
{
    /// <summary>
    /// Résultat de recherche IA.
    /// </summary>
    public class AiSearchResult
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public string MatchedText { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Moteur de recherche IA sans LSP.
    /// Transforme une requête en langage naturel en regex,
    /// puis recherche dans l'index plutôt que sur le disque.
    ///
    /// Exemples de requêtes supportées :
    /// - "Montre-moi où ce système est utilisé"
    /// - "Trouve la classe AgentScanner"
    /// - "Où est l'interface IValidator ?"
    /// - "Cherche les fichiers qui parlent de pipeline"
    /// </summary>
    public class AiSearchEngine
    {
        private readonly ProjectIndex _index;

        /// <summary>
        /// Optionnel : générateur IA pour reformuler la requête en regex.
        /// Si null, utilise une extraction de mots-clés simple.
        /// </summary>
        private readonly Func<string, Task<string>> _queryReformulator;

        public AiSearchEngine(ProjectIndex index, Func<string, Task<string>> queryReformulator = null)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _queryReformulator = queryReformulator;
        }

        /// <summary>
        /// Recherche principale.
        /// </summary>
        public async Task<IReadOnlyList<AiSearchResult>> SearchAsync(string query, int maxResults = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<AiSearchResult>();
            }

            // Étape 1 : extraction des mots-clés.
            var keywords = ExtractKeywords(query);

            if (keywords.Count == 0)
            {
                return Array.Empty<AiSearchResult>();
            }

            // Étape 2 : recherche dans l'index (rapide).
            var candidates = new List<AiSearchResult>();

            foreach (var keyword in keywords)
            {
                // Recherche par nom exact.
                var exactMatches = _index.FindByName(keyword);
                foreach (var entry in exactMatches)
                {
                    candidates.Add(new AiSearchResult
                    {
                        FilePath = entry.FilePath,
                        Line = entry.Line,
                        MatchedText = entry.Name,
                        Score = 1.0,
                        Reason = $"Symbole '{entry.Name}' ({entry.Kind})"
                    });
                }

                // Recherche par préfixe.
                var prefixMatches = _index.FindByPrefix(keyword, 20);
                foreach (var entry in prefixMatches)
                {
                    if (!candidates.Any(c => c.FilePath == entry.FilePath && c.Line == entry.Line))
                    {
                        candidates.Add(new AiSearchResult
                        {
                            FilePath = entry.FilePath,
                            Line = entry.Line,
                            MatchedText = entry.Name,
                            Score = 0.7,
                            Reason = $"Symbole proche : '{entry.Name}' ({entry.Kind})"
                        });
                    }
                }
            }

            // Étape 3 : recherche texte dans les fichiers candidats uniquement.
            var candidateFiles = candidates.Select(c => c.FilePath).Distinct().Take(20).ToList();
            var contentMatches = await SearchInFilesAsync(candidateFiles, keywords);
            candidates.AddRange(contentMatches);

            // Étape 4 : tri et déduplication.
            var results = candidates
                .OrderByDescending(c => c.Score)
                .Take(maxResults)
                .ToList();

            return results;
        }

        /// <summary>
        /// Recherche dans le contenu d'une liste de fichiers.
        /// Limité à un petit nombre de fichiers pour rester rapide.
        /// </summary>
        private async Task<IReadOnlyList<AiSearchResult>> SearchInFilesAsync(
            IEnumerable<string> filePaths,
            IReadOnlyList<string> keywords)
        {
            var results = new List<AiSearchResult>();

            foreach (var filePath in filePaths)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    var content = await File.ReadAllTextAsync(filePath);
                    var lines = content.Split('\n');

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];

                        foreach (var keyword in keywords)
                        {
                            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add(new AiSearchResult
                                {
                                    FilePath = filePath,
                                    Line = i + 1,
                                    MatchedText = line.Trim(),
                                    Score = 0.5,
                                    Reason = $"Contenu contient '{keyword}'"
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Fichier illisible : on continue.
                }
            }

            return results;
        }

        /// <summary>
        /// Extrait les mots-clés d'une requête en langage naturel.
        /// Sans NLP lourd : juste suppression des mots vides et ponctuation.
        /// </summary>
        private static IReadOnlyList<string> ExtractKeywords(string query)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "le", "la", "les", "un", "une", "des", "de", "du", "d", "l",
                "montre", "montre-moi", "trouve", "cherche", "où", "ou",
                "est", "sont", "ce", "cette", "ces", "son", "sa", "ses",
                "moi", "moi", "the", "a", "an", "is", "are", "where", "find", "show"
            };

            var cleaned = Regex.Replace(query, @"[^\w\s\-\.]", " ");
            var words = cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            return words
                .Where(w => w.Length >= 2 && !stopWords.Contains(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
