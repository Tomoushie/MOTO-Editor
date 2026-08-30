// Moto.Editor/Services/FileTreeService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Editor.Models;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Service d'arborescence : lecture paresseuse des dossiers,
    /// aplatissement pour CollectionView (MAUI n'a pas de TreeView natif).
    /// </summary>
    public partial class FileTreeService
    {
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "node_modules", ".idea"
        };

        /// <summary>Crée le nœud racine d'un dossier.</summary>
        public FileNode CreateRoot(string rootPath)
        {
            return new FileNode
            {
                Name = new DirectoryInfo(rootPath).Name,
                Path = rootPath,
                IsDirectory = true,
                Depth = 0,
                IsExpanded = true
            };
        }

        /// <summary>Charge les enfants d'un dossier (premier niveau).</summary>
        public void LoadChildren(FileNode node)
        {
            if (node.IsLoaded || !node.IsDirectory)
            {
                return;
            }

            try
            {
                var dir = new DirectoryInfo(node.Path);

                var folders = dir.GetDirectories()
                    .Where(d => !Excluded.Contains(d.Name) && !d.Name.StartsWith("."))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new FileNode
                    {
                        Name = d.Name,
                        Path = d.FullName,
                        IsDirectory = true,
                        Depth = node.Depth + 1
                    });

                var files = dir.GetFiles()
                    .Where(f => !f.Name.StartsWith("."))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new FileNode
                    {
                        Name = f.Name,
                        Path = f.FullName,
                        IsDirectory = false,
                        Depth = node.Depth + 1
                    });

                node.Children.Clear();

                foreach (var child in folders.Concat(files))
                {
                    node.Children.Add(child);
                }

                node.IsLoaded = true;
            }
            catch
            {
                // Dossier inaccessible : on ignore silencieusement.
            }
        }

        /// <summary>
        /// ★ AJOUT (30/08, 2e passe) : recherche récursive de fichiers par nom
        /// (onglet "Recherche" du menu horizontal — demandé par Tom après 3 tours
        /// où le bouton se contentait de rouvrir le bandeau IA sans rapport).
        /// Réutilise les mêmes exclusions que l'arborescence (bin/obj/.git/...)
        /// pour rester cohérent et rapide même sur un projet .NET complet.
        /// </summary>
        public List<string> SearchFiles(string rootPath, string query, int maxResults = 200)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(query)
                || !Directory.Exists(rootPath))
                return results;

            void Walk(string dir)
            {
                if (results.Count >= maxResults) return;

                IEnumerable<string> subDirs;
                IEnumerable<string> files;
                try
                {
                    subDirs = Directory.EnumerateDirectories(dir);
                    files = Directory.EnumerateFiles(dir);
                }
                catch
                {
                    return; // dossier inaccessible : ignoré silencieusement.
                }

                foreach (var file in files)
                {
                    if (results.Count >= maxResults) break;
                    var name = Path.GetFileName(file);
                    if (name.StartsWith(".")) continue;
                    if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        results.Add(file);
                }

                foreach (var sub in subDirs)
                {
                    if (results.Count >= maxResults) break;
                    var name = Path.GetFileName(sub);
                    if (Excluded.Contains(name) || name.StartsWith(".")) continue;
                    Walk(sub);
                }
            }

            Walk(rootPath);
            return results;
        }

        /// <summary>
        /// Aplatissement DFS : ne retourne que les nœuds visibles
        /// (enfants des dossiers dépliés).
        /// </summary>
        public List<FileNode> Flatten(FileNode root)
        {
            var result = new List<FileNode>();

            if (root == null)
            {
                return result;
            }

            result.Add(root);
            AppendChildren(root, result);

            return result;
        }

        private void AppendChildren(FileNode node, List<FileNode> result)
        {
            if (!node.IsExpanded)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                result.Add(child);

                if (child.IsDirectory)
                {
                    AppendChildren(child, result);
                }
            }
        }
    }
}
