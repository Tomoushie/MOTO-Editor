// Moto.Editor/Indexing/ProjectIndex.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Editor.Indexing
{
    /// <summary>
    /// Index mémoire du projet.
    /// Thread-safe pour permettre l'indexation en arrière-plan
    /// pendant que l'utilisateur navigue ou recherche.
    /// </summary>
    public class ProjectIndex
    {
        /// <summary>
        /// Index principal : nom de symbole (clé) → liste d'entrées.
        /// ConcurrentDictionary car l'indexation tourne en parallèle.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<SymbolIndexEntry>> _byName =
            new ConcurrentDictionary<string, List<SymbolIndexEntry>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Index secondaire : fichier → horodatage de dernière indexation.
        /// Utilisé pour l'invalidation incrémentale.
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> _fileTimestamps =
            new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Nombre total de symboles indexés.</summary>
        public int SymbolCount => _byName.Values.Sum(list => list.Count);

        /// <summary>Nombre de fichiers indexés.</summary>
        public int FileCount => _fileTimestamps.Count;

        /// <summary>
        /// Ajoute une entrée. Thread-safe.
        /// </summary>
        public void Add(SymbolIndexEntry entry)
        {
            var list = _byName.GetOrAdd(entry.Name, _ => new List<SymbolIndexEntry>());

            lock (list)
            {
                list.Add(entry);
            }
        }

        /// <summary>
        /// Supprime toutes les entrées d'un fichier.
        /// Utilisé lors de la ré-indexation incrémentale.
        /// </summary>
        public void RemoveFile(string filePath)
        {
            _fileTimestamps.TryRemove(filePath, out _);

            // Parcourt tous les symboles et retire ceux du fichier.
            // Acceptable car appelé uniquement sur fichiers modifiés.
            foreach (var kv in _byName)
            {
                lock (kv.Value)
                {
                    kv.Value.RemoveAll(e =>
                        string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        /// <summary>
        /// Recherche exacte par nom.
        /// </summary>
        public IReadOnlyList<SymbolIndexEntry> FindByName(string name)
        {
            if (_byName.TryGetValue(name, out var list))
            {
                lock (list)
                {
                    return list.ToList();
                }
            }
            return Array.Empty<SymbolIndexEntry>();
        }

        /// <summary>
        /// Recherche préfixe pour l'autocomplétion.
        /// </summary>
        public IReadOnlyList<SymbolIndexEntry> FindByPrefix(string prefix, int maxResults = 50)
        {
            var results = new List<SymbolIndexEntry>();

            foreach (var kv in _byName)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    lock (kv.Value)
                    {
                        results.AddRange(kv.Value);
                    }

                    if (results.Count >= maxResults)
                    {
                        break;
                    }
                }
            }

            return results.Take(maxResults).ToList();
        }

        /// <summary>
        /// Recherche par type de symbole.
        /// Exemple : trouver tous les systèmes.
        /// </summary>
        public IReadOnlyList<SymbolIndexEntry> FindByKind(SymbolKind kind, int maxResults = 200)
        {
            var results = new List<SymbolIndexEntry>();

            foreach (var kv in _byName)
            {
                lock (kv.Value)
                {
                    var matches = kv.Value.Where(e => e.Kind == kind);

                    foreach (var entry in matches)
                    {
                        results.Add(entry);
                        if (results.Count >= maxResults)
                        {
                            return results;
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Recherche par namespace.
        /// </summary>
        public IReadOnlyList<SymbolIndexEntry> FindByNamespace(string ns, int maxResults = 200)
        {
            var results = new List<SymbolIndexEntry>();

            foreach (var kv in _byName)
            {
                lock (kv.Value)
                {
                    var matches = kv.Value.Where(e =>
                        e.Namespace.StartsWith(ns, StringComparison.OrdinalIgnoreCase));

                    foreach (var entry in matches)
                    {
                        results.Add(entry);
                        if (results.Count >= maxResults)
                        {
                            return results;
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Marque un fichier comme indexé à un instant donné.
        /// </summary>
        public void MarkFileIndexed(string filePath, DateTime timestamp)
        {
            _fileTimestamps[filePath] = timestamp;
        }

        /// <summary>
        /// Vérifie si un fichier doit être ré-indexé.
        /// </summary>
        public bool NeedsReindex(string filePath, DateTime currentLastWrite)
        {
            if (!_fileTimestamps.TryGetValue(filePath, out var indexedAt))
            {
                return true;
            }

            return currentLastWrite > indexedAt;
        }

        /// <summary>
        /// Vide l'index. Utilisé lors d'un changement de workspace.
        /// </summary>
        public void Clear()
        {
            _byName.Clear();
            _fileTimestamps.Clear();
        }
    }
}
