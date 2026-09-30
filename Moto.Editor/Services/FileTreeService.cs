// Moto.Editor/Services/FileTreeService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Editor.Models;
using Moto.Editor.Settings;

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

        // ★ AJOUT (01/10) : règles de visibilité pilotées par les réglages pp_*.
        //
        // POURQUOI : jusqu'ici le service appliquait des règles FIGÉES — `.Where(d => !d.Name
        // .StartsWith("."))` masquait toujours les entrées cachées, en dur, dans les 4 endroits
        // ci-dessous. Les réglages `pp_hide_hidden` et `pp_hide_gitignore` promettaient donc
        // exactement ce que le code faisait sans jamais les consulter.
        //
        // ⚠️ La liste `Excluded` ci-dessus (bin/obj/.git/.vs/node_modules/.idea) reste FIGÉE et
        // volontairement non pilotée : c'est une liste d'exclusions techniques (dossiers de build
        // et de gestion de version), pas une préférence d'affichage — aucun réglage pp_* ne la
        // revendique, l'exposer serait inventer un réglage qui n'existe pas.
        //
        // Les règles sont posées par l'appelant AVANT LoadChildren (voir FileExplorerView) ;
        // par défaut on reproduit EXACTEMENT l'ancien comportement (cachés masqués), pour qu'un
        // appelant qui ne dit rien ne voie aucune différence.
        private bool _hideHidden = true;
        private bool _hideGitIgnore;
        private GitIgnoreMatcher? _gitIgnore;

        /// <summary>★ AJOUT (01/10) : applique les règles de visibilité des réglages pp_*.</summary>
        internal void ApplyVisibilitySettings(PanelSettings.VisibilityRules rules)
        {
            _hideHidden = rules.HideHidden;
            _hideGitIgnore = rules.HideGitIgnore;
        }

        /// <summary>
        /// ★ AJOUT (01/10) : charge les motifs du .gitignore du dossier ouvert, s'il existe.
        /// Un seul fichier lu (celui de la racine) : c'est le cas courant, et ça évite de
        /// relire le disque à chaque dépliage de dossier.
        /// </summary>
        public void LoadGitIgnore(string rootPath)
        {
            _gitIgnore = GitIgnoreMatcher.Load(rootPath);
        }

        /// <summary>★ AJOUT (01/10) : vrai si l'entrée doit être masquée d'après les règles courantes.</summary>
        private bool IsHiddenByRules(DirectoryInfo dir) => IsHiddenByRules(dir.Name, isDirectory: true);
        private bool IsHiddenByRules(FileInfo file) => IsHiddenByRules(file.Name, isDirectory: false);

        private bool IsHiddenByRules(string name, bool isDirectory)
        {
            if (_hideHidden && name.StartsWith(".")) return true;
            if (_hideGitIgnore && _gitIgnore is not null && _gitIgnore.IsIgnored(name, isDirectory)) return true;
            return false;
        }

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
                    .Where(d => !Excluded.Contains(d.Name) && !IsHiddenByRules(d))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new FileNode
                    {
                        Name = d.Name,
                        Path = d.FullName,
                        IsDirectory = true,
                        Depth = node.Depth + 1
                    });

                var files = dir.GetFiles()
                    .Where(f => !IsHiddenByRules(f))
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
                    // ★ (01/10) : même règle de visibilité que l'arborescence (pp_hide_hidden),
                    // au lieu du StartWith(".") figé — sinon la recherche montrerait des fichiers
                    // que l'explorateur cache, ou l'inverse.
                    if (IsHiddenByRules(name, isDirectory: false)) continue;
                    if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        results.Add(file);
                }

                foreach (var sub in subDirs)
                {
                    if (results.Count >= maxResults) break;
                    var name = Path.GetFileName(sub);
                    if (Excluded.Contains(name) || IsHiddenByRules(name, isDirectory: true)) continue;
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
