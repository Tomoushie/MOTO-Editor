// Workspace/WorkspaceManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Editor.Workspace
{
    /// <summary>
    /// Description d'un workspace MOTO.
    /// </summary>
    public class WorkspaceInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public DateTime LastOpenedUtc { get; set; }
    }

    /// <summary>
    /// Gestionnaire de workspaces.
    /// Gère multi-projets, favoris, historique et persistance locale.
    /// </summary>
    public class WorkspaceManager
    {
        private readonly List<WorkspaceInfo> _workspaces = new List<WorkspaceInfo>();
        private readonly string _storagePath;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public event Action<WorkspaceInfo> WorkspaceOpened;

        public WorkspaceManager()
        {
            _storagePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor",
                "workspaces.json"
            );

            Load();
        }

        /// <summary>
        /// Ouvre un dossier comme workspace.
        /// </summary>
        public WorkspaceInfo Open(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                return null;
            }

            var existing = _workspaces.FirstOrDefault(w =>
                w.RootPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase)
            );

            if (existing == null)
            {
                existing = new WorkspaceInfo
                {
                    Name = new DirectoryInfo(rootPath).Name,
                    RootPath = rootPath
                };

                _workspaces.Add(existing);
            }

            existing.LastOpenedUtc = DateTime.UtcNow;

            Save();
            WorkspaceOpened?.Invoke(existing);

            return existing;
        }

        /// <summary>
        /// Ajoute ou retire un workspace des favoris.
        /// </summary>
        public void ToggleFavorite(Guid workspaceId)
        {
            var workspace = _workspaces.FirstOrDefault(w => w.Id == workspaceId);

            if (workspace != null)
            {
                workspace.IsFavorite = !workspace.IsFavorite;
                Save();
            }
        }

        /// <summary>
        /// Retourne les workspaces récemment ouverts.
        /// </summary>
        public IReadOnlyList<WorkspaceInfo> GetRecents(int count = 10)
        {
            return _workspaces
                .OrderByDescending(w => w.LastOpenedUtc)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Retourne les workspaces favoris.
        /// </summary>
        public IReadOnlyList<WorkspaceInfo> GetFavorites()
        {
            return _workspaces
                .Where(w => w.IsFavorite)
                .ToList();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_storagePath))
                {
                    return;
                }

                var json = File.ReadAllText(_storagePath);
                var loaded = JsonSerializer.Deserialize<List<WorkspaceInfo>>(json, JsonOptions);

                if (loaded != null)
                {
                    _workspaces.Clear();
                    _workspaces.AddRange(loaded);
                }
            }
            catch
            {
                // Fichier corrompu ou illisible : on repart sur un état propre.
                _workspaces.Clear();
            }
        }

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_storagePath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_workspaces, JsonOptions);
                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // L'éditeur doit rester utilisable même si la persistance échoue.
            }
        }
    }

    /// <summary>
    /// Quick-open pour fichiers du workspace.
    /// </summary>
    public class QuickOpenEngine
    {
        /// <summary>
        /// Recherche floue simple dans le workspace.
        /// </summary>
        public IEnumerable<string> Search(string rootPath, string query, int maxResults = 20)
        {
            if (!Directory.Exists(rootPath) || string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<string>();
            }

            var results = new List<(string Path, int Score)>();

            Scan(
                new DirectoryInfo(rootPath),
                query,
                results,
                depth: 0,
                maxDepth: 6
            );

            return results
                .OrderByDescending(r => r.Score)
                .Select(r => r.Path)
                .Take(maxResults)
                .ToList();
        }

        private void Scan(
            DirectoryInfo directory,
            string query,
            List<(string Path, int Score)> results,
            int depth,
            int maxDepth)
        {
            if (depth >= maxDepth)
            {
                return;
            }

            try
            {
                foreach (var subDirectory in directory.GetDirectories())
                {
                    if (subDirectory.Name.StartsWith(".") ||
                        subDirectory.Name == "bin" ||
                        subDirectory.Name == "obj" ||
                        subDirectory.Name == "node_modules")
                    {
                        continue;
                    }

                    Scan(subDirectory, query, results, depth + 1, maxDepth);
                }

                foreach (var file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("."))
                    {
                        continue;
                    }

                    int score = FuzzyScore(file.Name, query);

                    if (score > 0)
                    {
                        results.Add((file.FullName, score));
                    }
                }
            }
            catch
            {
                // Dossier inaccessible : on continue sans bloquer l'éditeur.
            }
        }

        private int FuzzyScore(string fileName, string query)
        {
            if (fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return 100 - fileName.Length;
            }

            int queryIndex = 0;
            int score = 0;

            foreach (char c in fileName)
            {
                if (queryIndex < query.Length &&
                    char.ToLowerInvariant(c) == char.ToLowerInvariant(query[queryIndex]))
                {
                    queryIndex++;
                    score += 2;
                }
            }

            return queryIndex == query.Length ? score : 0;
        }
    }
}
