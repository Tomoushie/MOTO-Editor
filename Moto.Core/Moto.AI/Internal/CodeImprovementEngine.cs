// Moto.Core/AI/Internal/CodeImprovementEngine.cs
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur d'amélioration automatique.
    /// Propose des refactors légers et des améliorations structurelles.
    /// </summary>
    public class CodeImprovementEngine
    {
        /// <summary>
        /// Produit des suggestions d'amélioration.
        /// </summary>
        public List<AiSuggestion> Suggest(ProjectMap map)
        {
            var suggestions = new List<AiSuggestion>();

            foreach (var file in map.FileLineCounts)
            {
                if (file.Value > 500)
                {
                    suggestions.Add(new AiSuggestion
                    {
                        Title = "Découper un fichier volumineux",
                        Detail = $"Le fichier '{file.Key}' contient {file.Value} lignes. Il serait utile de le séparer en plusieurs fichiers.",
                        ActionId = "refactor.split-file"
                    });
                }
            }

            foreach (var issue in map.Issues)
            {
                if (issue.Kind == IssueKind.Todo)
                {
                    suggestions.Add(new AiSuggestion
                    {
                        Title = "Compléter les TODO",
                        Detail = $"Des TODO sont présents dans '{issue.FilePath}'.",
                        ActionId = "improve.complete-todo"
                    });
                }

                if (issue.Kind == IssueKind.NotImplementedException)
                {
                    suggestions.Add(new AiSuggestion
                    {
                        Title = "Implémenter les méthodes vides",
                        Detail = $"Certaines méthodes ne sont pas implémentées dans '{issue.FilePath}'.",
                        ActionId = "improve.implement-methods"
                    });
                }
            }

            var systems = map.Symbols.Count(s => s.Kind == SymbolKind.System);

            if (systems > 0)
            {
                suggestions.Add(new AiSuggestion
                {
                    Title = "Vérifier les systèmes",
                    Detail = $"Le projet contient {systems} système(s). Vérifie que chacun possède une interface et un composant.",
                    ActionId = "improve.check-systems"
                });
            }

            return suggestions
                .GroupBy(s => s.Title)
                .Select(g => g.First())
                .Take(30)
                .ToList();
        }
    }
}
