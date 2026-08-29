// Moto.Core/AI/Internal/NavigationAssistantEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// AI Navigation Assistant : "Montre-moi où ce système est utilisé."
    /// Trouve définition + usages, ouvre les fichiers, surligne, explique les liens.
    /// </summary>
    public class NavigationAssistantEngine
    {
        private readonly SearchEngine _search = new SearchEngine();

        public List<NavigationTarget> Resolve(ProjectMap map, string query)
        {
            var targets = new List<NavigationTarget>();

            var symbol = ExtractSymbol(query);

            if (string.IsNullOrWhiteSpace(symbol))
            {
                return targets;
            }

            // 1. Définition.
            foreach (var def in _search.FindDefinition(map, symbol).Take(3))
            {
                targets.Add(new NavigationTarget
                {
                    FilePath = def.FilePath,
                    Line = def.Line,
                    ContextLine = ReadLine(def.FilePath, def.Line),
                    Explanation = $"Définition de {def.Kind} '{def.SymbolName}'."
                });
            }

            // 2. Usages.
            foreach (var usage in _search.FindUsages(map, symbol).Take(10))
            {
                targets.Add(new NavigationTarget
                {
                    FilePath = usage.FilePath,
                    Line = usage.Line,
                    ContextLine = ReadLine(usage.FilePath, usage.Line),
                    Explanation = $"Utilisation de '{symbol}' : {usage.MatchedText}"
                });
            }

            return targets;
        }

        private string ExtractSymbol(string query)
        {
            var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Montre", "Où", "Est", "Ce", "Cette", "Le", "La", "Les",
                "Système", "System", "Classe", "Méthode", "Utilisé", "Défini", "MOTO", "AI"
            };

            foreach (Match m in Regex.Matches(query, @"\b([A-Z][a-zA-Z0-9]+)\b"))
            {
                if (!stop.Contains(m.Groups[1].Value))
                {
                    return m.Groups[1].Value;
                }
            }

            return null;
        }

        private string ReadLine(string filePath, int line)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                return line > 0 && line <= lines.Length ? lines[line - 1].Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
