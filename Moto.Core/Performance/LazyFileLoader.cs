// Moto.Core/Performance/LazyFileLoader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Moto.Core.Performance
{
    /// <summary>
    /// 18. Lazy Loading des fichiers.
    /// Principe : ne JAMAIS précharger le projet.
    /// - Le contenu n'est lu qu'à l'ouverture/sélection d'un onglet.
    /// - Un cache LRU borné garde seulement les documents récents en mémoire.
    /// - Les documents évincés sont sauvegardés automatiquement si modifiés.
    /// </summary>
    public class LazyFileLoader
    {
        private sealed class LoadedDocument
        {
            public string Content = string.Empty;
            public DateTime LastAccessUtc = DateTime.UtcNow;
            public bool IsDirty;
        }

        private readonly Dictionary<string, LoadedDocument> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly int _maxDocuments;

        /// <summary>Déclenché quand un document sort de la mémoire.</summary>
        public event Action<string> DocumentEvicted;

        /// <param name="maxDocuments">Nombre max de documents en mémoire (défaut 20).</param>
        public LazyFileLoader(int maxDocuments = 20)
        {
            _maxDocuments = maxDocuments;
        }

        /// <summary>Nombre de documents actuellement en mémoire.</summary>
        public int LoadedCount => _cache.Count;

        /// <summary>Estimation mémoire (octets) des documents cachés.</summary>
        public long EstimatedMemoryBytes =>
            _cache.Values.Sum(d => (long)d.Content.Length * 2);

        /// <summary>
        /// Retourne le contenu, en le chargeant à la demande.
        /// Aucun accès disque si le document est déjà en mémoire.
        /// </summary>
        public async Task<string> GetContentAsync(string path)
        {
            if (_cache.TryGetValue(path, out var doc))
            {
                doc.LastAccessUtc = DateTime.UtcNow;
                return doc.Content;
            }

            var content = await File.ReadAllTextAsync(path);

            _cache[path] = new LoadedDocument { Content = content };
            EvictIfNeeded();

            return content;
        }

        /// <summary>Met à jour le contenu en mémoire, sans toucher le disque.</summary>
        public void UpdateContent(string path, string content)
        {
            if (_cache.TryGetValue(path, out var doc))
            {
                doc.Content = content;
                doc.IsDirty = true;
                doc.LastAccessUtc = DateTime.UtcNow;
            }
            else
            {
                _cache[path] = new LoadedDocument { Content = content, IsDirty = true };
                EvictIfNeeded();
            }
        }

        /// <summary>Écrit sur disque uniquement si le document a été modifié.</summary>
        public async Task SaveAsync(string path)
        {
            if (_cache.TryGetValue(path, out var doc) && doc.IsDirty)
            {
                await File.WriteAllTextAsync(path, doc.Content);
                doc.IsDirty = false;
            }
        }

        /// <summary>
        /// Évince les documents les moins récents.
        /// Sécurité : un document modifié est sauvegardé avant éviction.
        /// </summary>
        private void EvictIfNeeded()
        {
            while (_cache.Count > _maxDocuments)
            {
                var oldest = _cache
                    .OrderBy(kv => kv.Value.LastAccessUtc)
                    .First();

                if (oldest.Value.IsDirty)
                {
                    try
                    {
                        File.WriteAllText(oldest.Key, oldest.Value.Content);
                    }
                    catch
                    {
                        // Échec disque : on stoppe l'éviction pour ne rien perdre.
                        break;
                    }
                }

                _cache.Remove(oldest.Key);
                DocumentEvicted?.Invoke(oldest.Key);
            }
        }
    }
}
