// Moto.Core/Doc/DocGenerators.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Moto.Core.AI.Internal;

namespace Moto.Core.Doc
{
    /// <summary>
    /// Générateurs de documentation markdown.
    /// Chaque méthode produit un fichier markdown riche et pédagogique.
    /// </summary>
    public static class DocGenerators
    {
        // ==================================================================
        // README.md
        // ==================================================================
        public static string GenerateReadme(ProjectMap map, string projectName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# {projectName}");
            sb.AppendLine();
            sb.AppendLine("> Documentation générée automatiquement par **MOTO Editor**.");
            sb.AppendLine();

            // Résumé
            sb.AppendLine("## 📖 Résumé");
            sb.AppendLine();
            sb.AppendLine($"Ce projet contient **{map.Files.Count} fichiers** et **{map.Symbols.Count} symboles** ");
            sb.AppendLine($"répartis dans plusieurs modules et systèmes.");
            sb.AppendLine();

            // Stats
            var classes = map.Symbols.Count(s => s.Kind == SymbolKind.Class);
            var interfaces = map.Symbols.Count(s => s.Kind == SymbolKind.Interface);
            var systems = map.Symbols.Count(s => s.Kind == SymbolKind.System);
            var components = map.Symbols.Count(s => s.Kind == SymbolKind.Component);

            sb.AppendLine("| Type | Nombre |");
            sb.AppendLine("|------|--------|");
            sb.AppendLine($"| Classes | {classes} |");
            sb.AppendLine($"| Interfaces | {interfaces} |");
            sb.AppendLine($"| Systèmes | {systems} |");
            sb.AppendLine($"| Composants | {components} |");
            sb.AppendLine();

            // Modules principaux
            sb.AppendLine("## 🧩 Modules principaux");
            sb.AppendLine();

            var modules = map.Symbols
                .Where(s => s.Kind == SymbolKind.System || s.Kind == SymbolKind.Class)
                .GroupBy(s => Path.GetDirectoryName(s.FilePath) ?? "")
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var module in modules)
            {
                var name = Path.GetFileName(module.Key);
                if (string.IsNullOrWhiteSpace(name)) name = "Racine";
                sb.AppendLine($"- **{name}** ({module.Count()} éléments)");
            }

            sb.AppendLine();

            // Démarrage rapide
            sb.AppendLine("## 🚀 Démarrage rapide");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine("# Compiler le projet");
            sb.AppendLine("dotnet build");
            sb.AppendLine();
            sb.AppendLine("# Lancer le projet");
            sb.AppendLine("dotnet run");
            sb.AppendLine("```");
            sb.AppendLine();

            // Architecture
            sb.AppendLine("## 🏗 Architecture");
            sb.AppendLine();
            sb.AppendLine("Voir [Architecture.md](Architecture.md) pour les détails.");
            sb.AppendLine();

            // Footer
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Généré le {DateTime.Now:dd/MM/yyyy HH:mm} par MOTO Doc Engine*");

            return sb.ToString();
        }

        // ==================================================================
        // Structure.md
        // ==================================================================
        public static string GenerateStructure(ProjectMap map, string projectName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Structure du projet {projectName}");
            sb.AppendLine();
            sb.AppendLine("> Vue d'ensemble de l'organisation des fichiers et dossiers.");
            sb.AppendLine();

            // Groupement par dossier
            var byFolder = map.Files
                .GroupBy(f => Path.GetDirectoryName(f) ?? "")
                .OrderBy(g => g.Key);

            sb.AppendLine("## 📁 Organisation par dossiers");
            sb.AppendLine();

            foreach (var folder in byFolder)
            {
                var folderName = Path.GetFileName(folder.Key);
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "Racine";

                sb.AppendLine($"### `{folderName}/`");
                sb.AppendLine();

                // Compte par extension
                var byExt = folder.GroupBy(f => Path.GetExtension(f))
                                  .OrderByDescending(g => g.Count());

                sb.AppendLine("| Extension | Nombre |");
                sb.AppendLine("|-----------|--------|");

                foreach (var ext in byExt.Take(5))
                {
                    sb.AppendLine($"| `{ext.Key}` | {ext.Count()} |");
                }

                sb.AppendLine();

                // Fichiers importants (classes, systèmes)
                var important = folder
                    .Where(f => map.Symbols.Any(s => s.FilePath == f &&
                        (s.Kind == SymbolKind.Class || s.Kind == SymbolKind.System)))
                    .Take(5);

                if (important.Any())
                {
                    sb.AppendLine("**Fichiers principaux :**");
                    sb.AppendLine();
                    foreach (var f in important)
                    {
                        sb.AppendLine($"- `{Path.GetFileName(f)}`");
                    }
                    sb.AppendLine();
                }
            }

            // Diagramme
            sb.AppendLine("## 📊 Diagramme de structure");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine($"{projectName}/");

            foreach (var folder in byFolder.Take(8))
            {
                var name = Path.GetFileName(folder.Key);
                if (string.IsNullOrWhiteSpace(name)) continue;
                sb.AppendLine($"├── {name}/ ({folder.Count()} fichiers)");
            }

            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Généré le {DateTime.Now:dd/MM/yyyy HH:mm} par MOTO Doc Engine*");

            return sb.ToString();
        }

        // ==================================================================
        // Arborescence.md
        // ==================================================================
        public static string GenerateArborescence(ProjectMap map, string projectName, string workspaceRoot)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Arborescence complète de {projectName}");
            sb.AppendLine();
            sb.AppendLine("> Liste complète des fichiers du projet, organisée hiérarchiquement.");
            sb.AppendLine();

            sb.AppendLine("```");
            sb.AppendLine($"{projectName}/");

            // Construit l'arbre
            var tree = BuildTree(map.Files, workspaceRoot);
            RenderTree(sb, tree, "");

            sb.AppendLine("```");
            sb.AppendLine();

            // Stats
            sb.AppendLine("## 📈 Statistiques");
            sb.AppendLine();
            sb.AppendLine($"- **Total de fichiers** : {map.Files.Count}");
            sb.AppendLine($"- **Dossiers** : {map.Files.Select(f => Path.GetDirectoryName(f)).Distinct().Count()}");

            var byExt = map.Files
                .GroupBy(f => Path.GetExtension(f))
                .OrderByDescending(g => g.Count())
                .Take(10);

            sb.AppendLine();
            sb.AppendLine("### Répartition par extension");
            sb.AppendLine();
            sb.AppendLine("| Extension | Nombre |");
            sb.AppendLine("|-----------|--------|");

            foreach (var g in byExt)
            {
                sb.AppendLine($"| `{g.Key}` | {g.Count()} |");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Généré le {DateTime.Now:dd/MM/yyyy HH:mm} par MOTO Doc Engine*");

            return sb.ToString();
        }

        private class TreeNode
        {
            public string Name { get; set; } = "";
            public bool IsFile { get; set; }
            public Dictionary<string, TreeNode> Children { get; } = new();
        }

        private static TreeNode BuildTree(IEnumerable<string> files, string root)
        {
            var rootNode = new TreeNode { Name = Path.GetFileName(root) };

            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(root, file);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var current = rootNode;
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var isLast = i == parts.Length - 1;

                    if (!current.Children.TryGetValue(part, out var child))
                    {
                        child = new TreeNode { Name = part, IsFile = isLast };
                        current.Children[part] = child;
                    }

                    current = child;
                }
            }

            return rootNode;
        }

        private static void RenderTree(StringBuilder sb, TreeNode node, string prefix)
        {
            var children = node.Children.Values
                .OrderBy(c => c.IsFile)
                .ThenBy(c => c.Name)
                .ToList();

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var isLast = i == children.Count - 1;
                var connector = isLast ? "└── " : "├── ";
                var newPrefix = prefix + (isLast ? "    " : "│   ");

                sb.AppendLine($"{prefix}{connector}{child.Name}");

                if (!child.IsFile && child.Children.Count > 0)
                {
                    RenderTree(sb, child, newPrefix);
                }
            }
        }

        // ==================================================================
        // Modules.md
        // ==================================================================
        public static string GenerateModules(ProjectMap map, string projectName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Modules de {projectName}");
            sb.AppendLine();
            sb.AppendLine("> Description des modules fonctionnels du projet.");
            sb.AppendLine();

            // Groupement par namespace / dossier
            var modules = map.Symbols
                .Where(s => s.Kind == SymbolKind.Class || s.Kind == SymbolKind.System)
                .GroupBy(s => Path.GetFileName(Path.GetDirectoryName(s.FilePath) ?? ""))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .OrderBy(g => g.Key);

            foreach (var module in modules)
            {
                sb.AppendLine($"## 🧩 Module `{module.Key}`");
                sb.AppendLine();
                sb.AppendLine($"**{module.Count()} éléments** dans ce module.");
                sb.AppendLine();

                // Classes et systèmes
                var classes = module.Where(s => s.Kind == SymbolKind.Class).Take(5);
                var systems = module.Where(s => s.Kind == SymbolKind.System).Take(5);

                if (classes.Any())
                {
                    sb.AppendLine("**Classes principales :**");
                    sb.AppendLine();
                    foreach (var c in classes)
                    {
                        sb.AppendLine($"- `{c.Name}`");
                    }
                    sb.AppendLine();
                }

                if (systems.Any())
                {
                    sb.AppendLine("**Systèmes :**");
                    sb.AppendLine();
                    foreach (var s in systems)
                    {
                        sb.AppendLine($"- `{s.Name}` — traite la logique liée");
                    }
                    sb.AppendLine();
                }

                // Description pédagogique
                sb.AppendLine("> 💡 *Ce module regroupe des éléments cohérents qui travaillent ensemble ");
                sb.AppendLine("> pour fournir une fonctionnalité spécifique au projet.*");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Généré le {DateTime.Now:dd/MM/yyyy HH:mm} par MOTO Doc Engine*");

            return sb.ToString();
        }

        // ==================================================================
        // Systems.md
        // ==================================================================
        public static string GenerateSystems(ProjectMap map, string projectName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Systèmes de {projectName}");
            sb.AppendLine();
            sb.AppendLine("> Description des systèmes (logique métier) du projet.");
            sb.AppendLine();

            var systems = map.Symbols
                .Where(s => s.Kind == SymbolKind.System)
                .OrderBy(s => s.Name);

            if (!systems.Any())
            {
                sb.AppendLine("*Aucun système détecté dans le projet.*");
                sb.AppendLine();
                sb.AppendLine("> 💡 Les systèmes sont des classes qui contiennent la logique métier ");
                sb.AppendLine("> et sont souvent nommés `XxxSystem` dans les architectures ECS.");
            }
            else
            {
                sb.AppendLine($"**{systems.Count()} systèmes détectés.**");
                sb.AppendLine();

                foreach (var system in systems)
                {
                    var baseName = system.Name.Replace("System", "");
                    sb.AppendLine($"## ⚙ `{system.Name}`");
                    sb.AppendLine();
                    sb.AppendLine($"**Fichier** : `{Path.GetFileName(system.FilePath)}`");
                    sb.AppendLine();

                    // Composant associé ?
                    var component = map.Symbols.FirstOrDefault(s =>
                        s.Kind == SymbolKind.Component &&
                        s.Name.Contains(baseName, StringComparison.OrdinalIgnoreCase));

                    if (component != null)
                    {
                        sb.AppendLine($"**Composant associé** : `{component.Name}`");
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine("> ⚠️ **Aucun composant associé détecté.**");
                        sb.AppendLine("> Ce système pourrait bénéficier d'un composant `XxxComponent` ");
                        sb.AppendLine("> pour stocker ses données d'état.");
                        sb.AppendLine();
                    }

                    // Interface ?
                    var iface = map.Symbols.FirstOrDefault(s =>
                        s.Kind == SymbolKind.Interface &&
                        s.Name.Contains(baseName, StringComparison.OrdinalIgnoreCase));

                    if (iface != null)
                    {
                        sb.AppendLine($"**Interface** : `{iface.Name}`");
                        sb.AppendLine();
                    }

                    sb.AppendLine($"**Rôle suggéré** : gérer la logique `{baseName.ToLower()}` du projet.");
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"*Généré le {DateTime.Now:dd/MM/yyyy HH:mm} par MOTO Doc Engine*");

            return sb.ToString();
        }

        // ==================================================================
        // Architecture.md
        // ==================================================================
        public static string GenerateArchitecture(ProjectMap map, string projectName, PatternDetectorEngine patterns)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Architecture de {projectName}");
            sb.AppendLine();
            sb.AppendLine("> Vue d'ensemble architecturale du projet.");
            sb.AppendLine();

            // Diagramme haut niveau
            sb.AppendLine("## 🏛 Diagramme architectural");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("┌─────────────────────────────────────────┐");
            sb.AppendLine($"│         {projectName.PadRight(30)}│");
            sb.AppendLine("└─────────────────────────────────────────┘");
            sb.AppendLine("                   │");
            sb.AppendLine("                   ▼");
            sb.AppendLine("┌───────────┬───────────┬───────────┬───────────┐");
            sb.AppendLine("│  Modules  │  Systems  │Components │Interfaces │");

            var modules = map.Symbols.Count(s => s.Kind == SymbolKind.Class);
            var systems = map.Symbols.Count(s => s.Kind == SymbolKind.System);
            var components = map.Symbols.Count(s => s.Kind == SymbolKind.Component);
            var interfaces = map.Symbols.Count(s => s.Kind == SymbolKind.Interface);

            sb.AppendLine($"│   {modules,3}     │   {systems,3}     │   {components,3}     │   {interfaces,3}     │");
            sb.AppendLine("└───────────┴───────────┴───────────┴───────────┘");
            sb.AppendLine("```");
            sb.AppendLine();

            // Patterns détectés
            sb.AppendLine("## 🔍 Patterns détectés");
            sb.AppendLine();

            var patternReport = patterns.Analyze(map);

            if (patternReport.Suggestions.Any())
            {
                foreach (var s in patternReport.Suggestions.Take(10))
                {
                    sb.AppendLine($"- **{s.Title}** : {s.Detail}");
                }
            }
            else
            {
                sb.AppendLine("*Aucun pattern spécifique détecté.*");
            }

            sb.AppendLine();

            // Dépendances
            sb.AppendLine("## 🔗 Dépendances principales");
            sb.AppendLine();

            var topDeps = map.Relations
                .OrderByDescending(kv => kv.Value.Count)
                .Take(10);

            if (topDeps.Any())
            {
                sb.AppendLine("| Fichier | Dépendances |");
                sb.AppendLine("|---------|-------------|");

                foreach (var dep in topDeps)
                {
                    sb.AppendLine($"| `{Path.GetFileName(dep.Key)}` | {dep.Value.Count} |");
                }
            }
            else
            {
                sb.AppendLine("*Pas de dépendances détectées.*");
            }

            sb.AppendLine();

            // Recommandations
            sb.AppendLine("## 💡 Recommandations architecturales");
            sb.AppendLine();

            var issues = map.Issues.Take(5);

            if (issues.Any())
            {
                foreach (var issue in issues)
                {
                    sb.AppendLine($"- ⚠️ {issue.Message}");
                }
            }
            else
            {
                sb.AppendLine("✅ L'architecture semble cohérente.");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Généré le {DateTime.Now:dd/MM/yyyy HH:mm} par MOTO Doc Engine*");

            return sb.ToString();
        }
    }
}
