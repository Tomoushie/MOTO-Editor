// Moto.Core/AI/Internal/ActionEngine.cs
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Actions IA intelligentes :
    /// AutoDoc, AutoLink, AutoPort.
    /// </summary>
    public class ActionEngine
    {
        /// <summary>
        /// Génère la documentation automatique du projet.
        /// </summary>
        public List<AiFileChange> AutoDoc(ProjectMap map)
        {
            var changes = new List<AiFileChange>();

            changes.Add(new AiFileChange
            {
                Path = "README.md",
                Reason = "Documentation principale du projet.",
                ChangeType = FileChangeType.Update,
                Content = GenerateReadme(map)
            });

            changes.Add(new AiFileChange
            {
                Path = "Docs/STRUCTURE.md",
                Reason = "Description de la structure du projet.",
                ChangeType = FileChangeType.Create,
                Content = GenerateStructure(map)
            });

            changes.Add(new AiFileChange
            {
                Path = "Docs/ARBORESCENCE.md",
                Reason = "Arborescence du projet.",
                ChangeType = FileChangeType.Create,
                Content = GenerateTree(map)
            });

            return changes;
        }

        /// <summary>
        /// Génère un plan de connexion des modules.
        /// </summary>
        public List<AiFileChange> AutoLink(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# AutoLink");
            sb.AppendLine();
            sb.AppendLine("Ce fichier propose les connexions entre modules, systèmes et interfaces.");
            sb.AppendLine();

            var systems = map.Symbols.Where(s => s.Kind == SymbolKind.System).ToList();
            var interfaces = map.Symbols.Where(s => s.Kind == SymbolKind.Interface).ToList();

            sb.AppendLine("## Systèmes détectés");
            sb.AppendLine();

            foreach (var system in systems)
            {
                sb.AppendLine($"- {system.Name}");
            }

            sb.AppendLine();
            sb.AppendLine("## Interfaces détectées");
            sb.AppendLine();

            foreach (var iface in interfaces)
            {
                sb.AppendLine($"- {iface.Name}");
            }

            sb.AppendLine();
            sb.AppendLine("## Intégration suggérée");
            sb.AppendLine();
            sb.AppendLine("Chaque système devrait :");
            sb.AppendLine("1. Posséder une interface.");
            sb.AppendLine("2. Posséder un composant.");
            sb.AppendLine("3. Être enregistré dans le pipeline ou le conteneur de dépendances.");
            sb.AppendLine("4. Être initialisé avant la boucle de mise à jour.");
            sb.AppendLine("5. Être mis à jour dans la boucle principale.");

            return new List<AiFileChange>
            {
                new AiFileChange
                {
                    Path = "Docs/AUTOLINK.md",
                    Reason = "Plan de connexion automatique des modules.",
                    ChangeType = FileChangeType.Create,
                    Content = sb.ToString()
                }
            };
        }

        /// <summary>
        /// Génère un plan de portage multiplateforme.
        /// </summary>
        public List<AiFileChange> AutoPort(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# AutoPort");
            sb.AppendLine();
            sb.AppendLine("Plan de portage multiplateforme généré par MOTO AI.");
            sb.AppendLine();
            sb.AppendLine("## Plateformes cibles");
            sb.AppendLine("- Windows");
            sb.AppendLine("- Android");
            sb.AppendLine("- iOS");
            sb.AppendLine("- macOS");
            sb.AppendLine("- Linux");
            sb.AppendLine();
            sb.AppendLine("## Actions recommandées");
            sb.AppendLine("1. Séparer la logique métier dans Moto.Core.");
            sb.AppendLine("2. Garder l'UI dans Moto.Editor MAUI.");
            sb.AppendLine("3. Ne jamais mettre de dépendance UI dans Moto.Core.");
            sb.AppendLine("4. Créer des services platform-specific si nécessaire.");
            sb.AppendLine("5. Tester chaque plateforme progressivement.");

            return new List<AiFileChange>
            {
                new AiFileChange
                {
                    Path = "Docs/AUTOPORT.md",
                    Reason = "Plan de portage multiplateforme.",
                    ChangeType = FileChangeType.Create,
                    Content = sb.ToString()
                }
            };
        }

        private string GenerateReadme(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Projet");
            sb.AppendLine();
            sb.AppendLine("Documentation générée par MOTO AI.");
            sb.AppendLine();
            sb.AppendLine($"- Fichiers analysés : {map.Files.Count}");
            sb.AppendLine($"- Symboles détectés : {map.Symbols.Count}");
            sb.AppendLine($"- Namespaces détectés : {map.Namespaces.Count}");
            sb.AppendLine($"- Modules détectés : {map.Modules.Count}");
            sb.AppendLine($"- Problèmes détectés : {map.Issues.Count}");

            return sb.ToString();
        }

        private string GenerateStructure(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Structure du projet");
            sb.AppendLine();

            foreach (var module in map.Modules.OrderBy(m => m))
            {
                sb.AppendLine($"## {module}");

                var files = map.Files
                    .Where(f => f.Contains(module))
                    .Take(30);

                foreach (var file in files)
                {
                    sb.AppendLine($"- {file}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateTree(ProjectMap map)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Arborescence");
            sb.AppendLine();

            foreach (var file in map.Files.OrderBy(f => f).Take(500))
            {
                sb.AppendLine(file);
            }

            return sb.ToString();
        }
    }
}
