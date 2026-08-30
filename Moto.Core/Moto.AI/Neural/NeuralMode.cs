// Moto.Core/AI/Neural/NeuralMode.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Cortex;

namespace Moto.Core.AI.Neural
{
    /// <summary>
    /// MOTO AI v3 — Neural Mode : mini-modèle IA interne spécialisé.
    /// Apprend du code de l'utilisateur et génère dans son style.
    /// </summary>
    public class NeuralMode
    {
        private readonly CodeRetrieval _retrieval = new();
        private readonly CortexMemory _memory;
        private readonly string _workspace;

        public NeuralMode(string workspace, CortexMemory memory)
        {
            _workspace = workspace;
            _memory = memory;
        }

        /// <summary>Entraîne le modèle sur le projet actuel.</summary>
        public void Train()
        {
            _retrieval.IndexWorkspace(_workspace);
        }

        /// <summary>
        /// Génère du code basé sur les patterns apprisis du projet.
        /// </summary>
        public string Generate(string intent, string context = "")
        {
            // 1. Trouve les snippets les plus similaires
            var similar = _retrieval.Search(intent, top: 3);

            if (similar.Count == 0)
            {
                // Fallback : génération basique
                return GenerateFallback(intent);
            }

            // 2. Extrait les patterns des snippets similaires
            var patterns = ExtractPatterns(similar);

            // 3. Génère en respectant les patterns
            return GenerateFromPatterns(intent, patterns);
        }

        /// <summary>Complète du code basé sur le contexte.</summary>
        public string Complete(string code, string context = "")
        {
            var similar = _retrieval.Search(code + " " + context, top: 5);

            if (similar.Count == 0)
                return "";

            // Trouve le snippet le plus similaire qui commence comme le code actuel
            var bestMatch = similar.FirstOrDefault(s =>
                s.Content.Contains(code.Trim()) ||
                code.Trim().Contains(s.Content.Substring(0, Math.Min(50, s.Content.Length))));

            if (bestMatch != null)
            {
                // Extrait la suite logique
                var index = bestMatch.Content.IndexOf(code.Trim(), StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var remaining = bestMatch.Content.Substring(index + code.Length).TrimStart();
                    var nextLine = remaining.Split('\n').FirstOrDefault();
                    return nextLine ?? "";
                }
            }

            return "";
        }

        private List<CodePattern> ExtractPatterns(List<RetrievalResult> snippets)
        {
            var patterns = new List<CodePattern>();

            foreach (var snippet in snippets)
            {
                // Extrait les signatures de méthodes
                var methodMatches = Regex.Matches(
                    snippet.Content,
                    @"\b(public|private|protected|internal)\s+(\w+)\s+(\w+)\s*\(([^)]*)\)");

                foreach (Match m in methodMatches)
                {
                    patterns.Add(new CodePattern
                    {
                        Type = PatternType.Method,
                        Signature = $"{m.Groups[2].Value} {m.Groups[3].Value}({m.Groups[4].Value})",
                        Example = m.Value,
                        Similarity = snippet.Similarity
                    });
                }

                // Extrait les signatures de classes
                var classMatches = Regex.Matches(snippet.Content, @"\bclass\s+(\w+)");
                foreach (Match m in classMatches)
                {
                    patterns.Add(new CodePattern
                    {
                        Type = PatternType.Class,
                        Signature = m.Groups[1].Value,
                        Example = m.Value,
                        Similarity = snippet.Similarity
                    });
                }
            }

            return patterns
                .GroupBy(p => p.Signature)
                .Select(g => g.OrderByDescending(p => p.Similarity).First())
                .ToList();
        }

        private string GenerateFromPatterns(string intent, List<CodePattern> patterns)
        {
            var conventions = _memory.GetNamingConventions();
            var methodName = ExtractName(intent);

            // Génère selon les conventions apprises
            if (conventions.TryGetValue("method", out var methodConvention))
            {
                methodName = ApplyConvention(methodName, methodConvention);
            }

            // Cherche un pattern similaire
            var similarPattern = patterns.FirstOrDefault(p =>
                p.Type == PatternType.Method &&
                p.Signature.Contains(intent, StringComparison.OrdinalIgnoreCase));

            if (similarPattern != null)
            {
                return $@"public void {methodName}()
{{
    // Basé sur : {similarPattern.Example}
    // TODO : implémenter
}}";
            }

            return $@"public void {methodName}()
{{
    // TODO : implémenter
}}";
        }

        private string GenerateFallback(string intent)
        {
            var name = ExtractName(intent);
            return $@"public class {name}
{{
    public {name}()
    {{
        // TODO : implémenter
    }}
}}";
        }

        private string ExtractName(string text)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lastWord = words.Length > 0 ? words[words.Length - 1] : "Generated";

            // PascalCase
            return char.ToUpper(lastWord[0]) + lastWord.Substring(1);
        }

        private string ApplyConvention(string name, string convention)
        {
            return convention switch
            {
                "PascalCase" => char.ToUpper(name[0]) + name.Substring(1),
                "camelCase" => char.ToLower(name[0]) + name.Substring(1),
                _ => name
            };
        }
    }

    public enum PatternType
    {
        Method,
        Class,
        Property,
        Field
    }

    public class CodePattern
    {
        public PatternType Type { get; set; }
        public string Signature { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
        public double Similarity { get; set; }
    }
}
