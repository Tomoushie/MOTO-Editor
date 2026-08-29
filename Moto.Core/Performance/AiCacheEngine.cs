// Moto.Core/Performance/AiCacheEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moto.Core.Performance
{
    /// <summary>
    /// 21. Cache IA local.
    /// Mémorise suggestions, prédictions, refactors et explications.
    /// - Clé = SHA256(catégorie + prompt normalisé + hash du contexte).
    /// - Éviction LRU + TTL 7 jours + 500 entrées max.
    /// - Persistance JSON dans %AppData%/MotoEditor/ai_cache.json.
    /// </summary>
    public class AiCacheEngine
    {
        private sealed class Entry
        {
            public string Key { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public int Hits { get; set; }
            public long CreatedUtc { get; set; }
            public long LastAccessUtc { get; set; }
        }

        private const int MaxEntries = 500;
        private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly string _storagePath;

        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public double HitRate => Hits + Misses == 0 ? 0 : Hits / (double)(Hits + Misses);

        public AiCacheEngine()
        {
            _storagePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor",
                "ai_cache.json");

            Load();
        }

        /// <summary>
        /// Cherche une réponse cachée.
        /// Catégories : "chat", "explain", "fix", "refactor", "prediction", "xeno".
        /// </summary>
        public bool TryGet(string category, string prompt, string context, out string value)
        {
            var key = BuildKey(category, prompt, context);

            if (_entries.TryGetValue(key, out var entry) && !IsExpired(entry))
            {
                entry.Hits++;
                entry.LastAccessUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Hits++;
                value = entry.Value;
                return true;
            }

            Misses++;
            value = string.Empty;
            return false;
        }

        /// <summary>Mémorise une réponse IA.</summary>
        public void Put(string category, string prompt, string context, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var key = BuildKey(category, prompt, context);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            _entries[key] = new Entry
            {
                Key = key,
                Category = category,
                Value = value,
                Hits = 0,
                CreatedUtc = now,
                LastAccessUtc = now
            };

            Evict();
            Save();
        }

        /// <summary>Statistiques par catégorie (pour le panneau paramètres).</summary>
        public IReadOnlyDictionary<string, int> StatsByCategory()
        {
            return _entries.Values
                .GroupBy(e => e.Category)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Hits));
        }

        private static bool IsExpired(Entry e)
        {
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - e.CreatedUtc;
            return TimeSpan.FromSeconds(age) > Ttl;
        }

        /// <summary>Éviction LRU au-delà de MaxEntries.</summary>
        private void Evict()
        {
            while (_entries.Count > MaxEntries)
            {
                var oldest = _entries.Values
                    .OrderBy(e => e.LastAccessUtc)
                    .First();

                _entries.Remove(oldest.Key);
            }
        }

        /// <summary>Normalise le prompt : minuscules, espaces réduits.</summary>
        private static string Normalize(string prompt)
        {
            return Regex.Replace((prompt ?? string.Empty).ToLowerInvariant(), @"\s+", " ").Trim();
        }

        private static string BuildKey(string category, string prompt, string context)
        {
            using var sha = SHA256.Create();

            var raw = $"{category}|{Normalize(prompt)}|{Hash(context ?? string.Empty)}";
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));

            return Convert.ToBase64String(bytes);
        }

        private static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    var loaded = JsonSerializer.Deserialize<List<Entry>>(json);

                    if (loaded != null)
                    {
                        foreach (var e in loaded.Where(e => !IsExpired(e)))
                        {
                            _entries[e.Key] = e;
                        }
                    }
                }
            }
            catch
            {
                // Cache corrompu : on repart vide, sans bloquer l'éditeur.
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storagePath);

                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(_storagePath, JsonSerializer.Serialize(_entries.Values.ToList()));
            }
            catch
            {
                // La persistance du cache est optionnelle.
            }
        }
    }
}
