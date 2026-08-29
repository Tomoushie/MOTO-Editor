// Moto.Core/AI/Internal/AutoDocEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// AutoDoc Engine.
    /// Génère et met à jour la documentation automatiquement.
    /// </summary>
    public class AutoDocEngine
    {
        /// <summary>
        /// Génère toute la documentation du projet.
        /// </summary>
        public List<AiFileChange> GenerateFullDocumentation(ProjectMap map)
        {
            var changes = new List<AiFileChange>();

            changes.Add(GenerateReadme(map));
            changes.Add(GenerateStructure(map));
            changes.Add(GenerateModules(map));
            changes.Add(GenerateArchitecture(map));
            changes.Add(GenerateArborescence(map));

            return changes;
        }

        private AiFileChange GenerateReadme(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Projet");
            sb.AppendLine();
            sb.AppendLine("Documentation générée automatiquement par MOTO AI.");
            sb.AppendLine();
            sb.AppendLine("## Statistiques");
            sb.AppendLine();
            sb.AppendLine($"| Métrique | Valeur |");
            sb.AppendLine($"|----------|--------|");
            sb.AppendLine($"| Fichiers | {map.Files.Count} |");
            sb.AppendLine($"| Symboles | {map.Symbols.Count} |");
            sb.AppendLine($"| Namespaces | {map.Namespaces.Count} |");
            sb.AppendLine($"| Modules | {map.Modules.Count} |");
            sb.AppendLine($"| Problèmes | {map.Issues.Count} |");
            sb.AppendLine();
            sb.AppendLine("## Modules");
            sb.AppendLine();

            foreach (var module in map.Modules.OrderBy(m => m))
            {
                sb.AppendLine($"- `{module}`");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"*Généré le {DateTime.Now:yyyy-MM-dd HH:mm} par MOTO AI*");

            return new AiFileChange
            {
                Path = "README.md",
                Content = sb.ToString(),
                Reason = "Documentation principale.",
                ChangeType = FileChangeType.Update
            };
        }

        private AiFileChange GenerateStructure(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Structure du projet");
            sb.AppendLine();

            foreach (var module in map.Modules.OrderBy(m => m))
            {
                sb.AppendLine($"## {module}");
                sb.AppendLine();

                var files = map.Files.Where(f => f.Contains(module)).Take(20);

                foreach (var file in files)
                {
                    var relative = System.IO.Path.GetRelativePath(map.RootPath, file);
                    sb.AppendLine($"- `{relative}`");
                }

                sb.AppendLine();
            }

            return new AiFileChange
            {
                Path = "Docs/STRUCTURE.md",
                Content = sb.ToString(),
                Reason = "Structure du projet.",
                ChangeType = FileChangeType.Create
            };
        }

        private AiFileChange GenerateModules(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Modules");
            sb.AppendLine();

            var systems = map.Symbols.Where(s => s.Kind == SymbolKind.System).ToList();
            var interfaces = map.Symbols.Where(s => s.Kind == SymbolKind.Interface).ToList();
            var components = map.Symbols.Where(s => s.Kind == SymbolKind.Component).ToList();

            sb.AppendLine("## Systèmes");
            sb.AppendLine();
            foreach (var s in systems) sb.AppendLine($"- `{s.Name}` ({s.Namespace})");
            sb.AppendLine();

            sb.AppendLine("## Interfaces");
            sb.AppendLine();
            foreach (var i in interfaces) sb.AppendLine($"- `{i.Name}` ({i.Namespace})");
            sb.AppendLine();

            sb.AppendLine("## Composants");
            sb.AppendLine();
            foreach (var c in components) sb.AppendLine($"- `{c.Name}` ({c.Namespace})");
            sb.AppendLine();

            return new AiFileChange
            {
                Path = "Docs/MODULES.md",
                Content = sb.ToString(),
                Reason = "Documentation des modules.",
                ChangeType = FileChangeType.Create
            };
        }

        private AiFileChange GenerateArchitecture(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Architecture");
            sb.AppendLine();
            sb.AppendLine("## Namespaces");
            sb.AppendLine();

            foreach (var ns in map.Namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"- `{ns}`");
            }

            sb.AppendLine();
            sb.AppendLine("## Relations");
            sb.AppendLine();

            foreach (var relation in map.Relations.Take(30))
            {
                var file = System.IO.Path.GetRelativePath(map.RootPath, relation.Key);
                sb.AppendLine($"### {file}");
                foreach (var r in relation.Value.Take(10))
                {
                    sb.AppendLine($"  - {r}");
                }
                sb.AppendLine();
            }

            return new AiFileChange
            {
                Path = "Docs/ARCHITECTURE.md",
                Content = sb.ToString(),
                Reason = "Documentation de l'architecture.",
                ChangeType = FileChangeType.Create
            };
        }

        private AiFileChange GenerateArborescence(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Arborescence");
            sb.AppendLine();
            sb.AppendLine("```");

            foreach (var file in map.Files.OrderBy(f => f).Take(300))
            {
                var relative = System.IO.Path.GetRelativePath(map.RootPath, file);
                sb.AppendLine(relative);
            }

            sb.AppendLine("```");

            return new AiFileChange
            {
                Path = "Docs/ARBORESCENCE.md",
                Content = sb.ToString(),
                Reason = "Arborescence complète.",
                ChangeType = FileChangeType.Create
            };
        }
    }
}
