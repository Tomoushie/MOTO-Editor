// Moto.Core/AI/Beginner/ExplainEverythingEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Beginner
{
    public class LineExplanation
    {
        public int Line { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    public class ExplanationReport
    {
        public string FileSummary { get; set; } = string.Empty;
        public List<LineExplanation> Lines { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Systems { get; } = new();
        public List<string> Dependencies { get; } = new();
    }

    /// <summary>
    /// 22. Mode "Explain Everything" : explique chaque fichier, chaque ligne,
    /// chaque erreur, chaque système, chaque dépendance, en français simple.
    /// </summary>
    public class ExplainEverythingEngine
    {
        private readonly PedagogyEngine _pedagogy = new();

        public ExplanationReport Explain(ProjectMap map, string filePath)
        {
            var report = new ExplanationReport();

            var symbols = map.Symbols.Where(s => s.FilePath == filePath).ToList();

            // 1. Résumé du fichier
            var classes = symbols.Where(s => s.Kind == SymbolKind.Class || s.Kind == SymbolKind.System).Select(s => s.Name).ToList();
            var interfaces = symbols.Where(s => s.Kind == SymbolKind.Interface).Select(s => s.Name).ToList();

            report.FileSummary =
                $"Ce fichier contient {classes.Count} classe(s) et {interfaces.Count} interface(s). " +
                (classes.Count > 0 ? $"Classes : {string.Join(", ", classes)}. " : "") +
                (interfaces.Count > 0 ? $"Interfaces : {string.Join(", ", interfaces)}. " : "");

            // 2. Chaque ligne (plafonné à 200 lignes pour rester léger)
            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);

                for (int i = 0; i < Math.Min(lines.Length, 200); i++)
                {
                    var explanation = ExplainLine(lines[i]);

                    if (explanation != null)
                    {
                        report.Lines.Add(new LineExplanation
                        {
                            Line = i + 1,
                            Code = lines[i].TrimEnd(),
                            Explanation = explanation
                        });
                    }
                }
            }
            catch
            {
                report.FileSummary += " (fichier illisible)";
            }

            // 3. Chaque erreur (issues du projet sur ce fichier)
            foreach (var issue in map.Issues.Where(i => i.FilePath == filePath))
            {
                report.Errors.Add($"{issue.Message} → {ExplainError(issue.Message)}");
            }

            // 4. Chaque système
            foreach (var system in symbols.Where(s => s.Kind == SymbolKind.System))
            {
                report.Systems.Add($"{system.Name} : un module qui fait une chose précise (logique de jeu ou de moteur).");
            }

            // 5. Chaque dépendance
            if (map.Relations.TryGetValue(filePath, out var deps))
            {
                foreach (var dep in deps.Take(10))
                {
                    report.Dependencies.Add($"Utilise {dep} → ce fichier a besoin de ce symbole pour fonctionner.");
                }
            }

            return report;
        }

        /// <summary>Explique une ligne selon sa forme (heuristique pédagogique).</summary>
        private string ExplainLine(string line)
        {
            var t = line.Trim();

            if (t.Length == 0) return null;
            if (t.StartsWith("//")) return "Commentaire : " + t.TrimStart('/').Trim();
            if (t.StartsWith("using")) return "Importe un espace de noms pour réutiliser ses classes.";
            if (t.StartsWith("namespace")) return "Déclare l'espace de noms : la boîte de rangement du fichier.";
            if (Regex.IsMatch(t, @"\binterface\b")) return "Déclare une interface : un contrat que les classes devront respecter.";
            if (Regex.IsMatch(t, @"\b(class|record)\b")) return "Déclare une classe : un plan pour créer des objets.";
            if (Regex.IsMatch(t, @"\bvoid\s+\w+\s*\(")) return "Déclare une méthode (une action) sans valeur de retour.";
            if (Regex.IsMatch(t, @"\b(if|else)\b")) return "Condition : exécute le bloc seulement si la condition est vraie.";
            if (Regex.IsMatch(t, @"\b(for|foreach|while)\b")) return "Boucle : répète le bloc plusieurs fois.";
            if (t.StartsWith("return")) return "Renvoie une valeur à celui qui a appelé la méthode.";
            if (Regex.IsMatch(t, @"\bnew\s+\w+")) return "Crée une nouvelle instance d'un objet.";
            if (t.Contains("==") || t.Contains("!=")) return "Comparaison de deux valeurs.";
            if (t.Contains("=")) return "Affectation : range une valeur dans une variable.";

            return "Instruction du programme.";
        }

        private string ExplainError(string message)
        {
            if (message.Contains("MissingImplementation", StringComparison.OrdinalIgnoreCase))
                return "une interface n'a pas encore de classe qui l'utilise ; il faut créer cette classe.";

            if (message.Contains("MissingInterface", StringComparison.OrdinalIgnoreCase))
                return "un système n'a pas de contrat ; il faut créer son interface.";

            return "regarde la ligne indiquée et vérifie les noms et les accolades.";
        }
    }
}
