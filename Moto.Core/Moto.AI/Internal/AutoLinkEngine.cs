// Moto.Core/AI/Internal/AutoLinkEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// AutoLink Engine.
    /// Détecte les liens manquants et propose des connexions intelligentes.
    /// Agit comme un développeur senior qui voit les dépendances cassées.
    /// </summary>
    public class AutoLinkEngine
    {
        /// <summary>
        /// Lien manquant détecté.
        /// </summary>
        public class MissingLink
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string SuggestedAction { get; set; } = string.Empty;
            public string TargetFile { get; set; } = string.Empty;
            public string CodeSnippet { get; set; } = string.Empty;
        }

        /// <summary>
        /// Analyse le projet et détecte tous les liens manquants.
        /// </summary>
        public List<MissingLink> DetectMissingLinks(ProjectMap map)
        {
            var links = new List<MissingLink>();

            // 1. Interfaces non implémentées
            foreach (var issue in map.Issues.Where(i => i.Kind == IssueKind.MissingImplementation))
            {
                links.Add(new MissingLink
                {
                    Type = "missing_implementation",
                    Description = $"Interface '{issue.SymbolName}' sans implémentation.",
                    SuggestedAction = $"Créer une classe qui implémente {issue.SymbolName}",
                    TargetFile = issue.FilePath,
                    CodeSnippet = $"public class {issue.SymbolName.TrimStart('I')} : {issue.SymbolName} {{ }}"
                });
            }

            // 2. Systèmes sans interface
            foreach (var issue in map.Issues.Where(i => i.Kind == IssueKind.MissingInterfaceForSystem))
            {
                links.Add(new MissingLink
                {
                    Type = "missing_interface",
                    Description = $"Système '{issue.SymbolName}' sans interface.",
                    SuggestedAction = $"Créer I{issue.SymbolName}",
                    TargetFile = issue.FilePath,
                    CodeSnippet = $"public interface I{issue.SymbolName} {{ void Initialize(); void Update(float deltaTime); }}"
                });
            }

            // 3. Systèmes non connectés au pipeline
            var systems = map.Symbols.Where(s => s.Kind == SymbolKind.System).ToList();
            var connectedSystems = map.Relations
                .SelectMany(r => r.Value)
                .ToList();

            foreach (var system in systems)
            {
                bool isConnected = connectedSystems.Any(c => c.Contains(system.Name));

                if (!isConnected)
                {
                    links.Add(new MissingLink
                    {
                        Type = "system_not_connected",
                        Description = $"Système '{system.Name}' non connecté au pipeline.",
                        SuggestedAction = $"Connecter {system.Name} au pipeline XENO-SSS∞",
                        TargetFile = system.FilePath,
                        CodeSnippet = $"var {system.Name.ToLower()} = new {system.Name}(new {system.Name.Replace("System", "Component")}());"
                    });
                }
            }

            // 4. Classes référencées mais non trouvées
            var allClassNames = map.Symbols
                .Where(s => s.Kind == SymbolKind.Class || s.Kind == SymbolKind.System || s.Kind == SymbolKind.Component)
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var relation in map.Relations)
            {
                foreach (var refSymbol in relation.Value)
                {
                    var symbolName = refSymbol.Split(' ').FirstOrDefault() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(symbolName) && !allClassNames.Contains(symbolName))
                    {
                        links.Add(new MissingLink
                        {
                            Type = "class_not_found",
                            Description = $"Classe '{symbolName}' référencée mais non trouvée.",
                            SuggestedAction = $"Créer {symbolName}.cs",
                            TargetFile = relation.Key,
                            CodeSnippet = $"public class {symbolName} {{ }}"
                        });
                    }
                }
            }

            return links
                .GroupBy(l => l.Description)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Génère les fichiers de correction pour les liens manquants.
        /// </summary>
        public List<AiFileChange> GenerateLinkFixes(ProjectMap map, List<MissingLink> links)
        {
            var changes = new List<AiFileChange>();

            foreach (var link in links)
            {
                if (link.Type == "missing_interface" || link.Type == "missing_implementation")
                {
                    changes.Add(new AiFileChange
                    {
                        Path = $"Generated/{link.SuggestedAction.Replace("Créer ", "").Replace(" ", "")}.cs",
                        Content = link.CodeSnippet,
                        Reason = link.Description,
                        ChangeType = FileChangeType.Create
                    });
                }
            }

            return changes;
        }
    }
}
