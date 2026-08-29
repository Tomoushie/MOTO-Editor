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
    public class FileTreeService
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
