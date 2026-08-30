// Moto.Core/AI/Cortex/CortexMemory.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.AI.Cortex
{
    /// <summary>
    /// Mémoire cognitive persistante du Cortex Engine.
    /// Stocke : habitudes, patterns, conventions, erreurs, corrections, intentions.
    /// </summary>
    public class CortexMemory
    {
        private readonly string _memoryPath;
        private readonly CortexState _state = new();
        private readonly object _lock = new();

        // ── Collections internes pour les stats ──
        private readonly List<HabitRecord> _habits = new();
        private readonly List<PatternRecord> _patterns = new();
        private readonly List<CorrectionRecord> _corrections = new();

        /// <summary>
        /// Habitudes exposées en lecture seule pour CortexEngine.GetStats().
        /// </summary>
        public IReadOnlyCollection<HabitRecord> Habits
        {
            get { lock (_lock) return _habits.AsReadOnly(); }
        }

        /// <summary>
        /// Patterns exposés en lecture seule pour CortexEngine.GetStats().
        /// </summary>
        public IReadOnlyCollection<PatternRecord> Patterns
        {
            get { lock (_lock) return _patterns.AsReadOnly(); }
        }

        public CortexMemory(string workspace)
        {
            _memoryPath = Path.Combine(workspace, ".moto", "cortex", "memory.json");
            Load();
        }

        /// <summary>Enregistre une habitude (ex: "utilise toujours var au lieu de types explicites").</summary>
        public void RecordHabit(string category, string pattern, double weight = 1.0)
        {
            lock (_lock)
            {
                if (!_state.Habits.ContainsKey(category))
                    _state.Habits[category] = new Dictionary<string, double>();
                if (!_state.Habits[category].ContainsKey(pattern))
                    _state.Habits[category][pattern] = 0;
                _state.Habits[category][pattern] += weight;

                // Synchronise avec la liste interne pour les stats
                var existingHabit = _habits.FirstOrDefault(h => h.Key == $"{category}:{pattern}");
                if (existingHabit != null)
                {
                    _habits.Remove(existingHabit);
                }
                _habits.Add(new HabitRecord($"{category}:{pattern}", _state.Habits[category][pattern], DateTime.UtcNow));

                Save();
            }
        }

        /// <summary>Enregistre un pattern de code récurrent.</summary>
        public void RecordPattern(string signature, string example, string context = "")
        {
            lock (_lock)
            {
                var patternRecord = new PatternRecordLegacy
                {
                    Signature = signature,
                    Example = example,
                    Context = context,
                    Timestamp = DateTime.UtcNow,
                    Frequency = _state.Patterns.Count(p => p.Signature == signature) + 1,
                };

                _state.Patterns.Add(patternRecord);
                _patterns.Add(new PatternRecord(signature, example, context, 0.8));
                Save();
            }
        }

        /// <summary>Enregistre une correction effectuée par l'utilisateur.</summary>
        public void RecordCorrection(string before, string after, string reason = "")
        {
            lock (_lock)
            {
                var correctionRecord = new CorrectionRecordLegacy
                {
                    Before = before,
                    After = after,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow
                };

                _state.Corrections.Add(correctionRecord);
                _corrections.Add(new CorrectionRecord(before, after, reason, DateTime.UtcNow));
                Save();
            }
        }

        /// <summary>Enregistre une convention de nommage.</summary>
        public void RecordNamingConvention(string type, string pattern)
        {
            lock (_lock)
            {
                _state.NamingConventions[type] = pattern;
                Save();
            }
        }

        /// <summary>Récupère les habitudes les plus fréquentes pour une catégorie.</summary>
        public Dictionary<string, double> GetHabits(string category, int top = 5)
        {
            lock (_lock)
            {
                if (!_state.Habits.ContainsKey(category))
                    return new Dictionary<string, double>();
                return _state.Habits[category]
                    .OrderByDescending(kv => kv.Value)
                    .Take(top)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }
        }

        /// <summary>Récupère les patterns les plus fréquents.</summary>
        public List<PatternRecordLegacy> GetPatterns(int top = 10)
        {
            lock (_lock)
            {
                return _state.Patterns
                    .OrderByDescending(p => p.Frequency)
                    .Take(top)
                    .ToList();
            }
        }

        /// <summary>Récupère les corrections récentes.</summary>
        public List<CorrectionRecordLegacy> GetCorrections(int top = 10)
        {
            lock (_lock)
            {
                return _state.Corrections
                    .OrderByDescending(c => c.Timestamp)
                    .Take(top)
                    .ToList();
            }
        }

        /// <summary>Récupère les conventions de nommage.</summary>
        public Dictionary<string, string> GetNamingConventions()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_state.NamingConventions);
            }
        }

        /// <summary>
        /// Retourne les stats agrégées de la mémoire cognitive.
        /// Utilisé par CortexEngine.GetStats() pour l'affichage dans HomeView.
        /// </summary>
        public CortexStats GetStats()
        {
            lock (_lock)
            {
                return new CortexStats(
                    TotalHabits: _habits.Count,
                    TotalPatterns: _patterns.Count,
                    TotalCorrections: _corrections.Count,
                    ConfidenceAvg: CalculateAverageConfidence()
                );
            }
        }

        /// <summary>
        /// Calcule la confiance moyenne des patterns appris.
        /// </summary>
        private double CalculateAverageConfidence()
        {
            if (_patterns.Count == 0) return 0.0;
            double sum = 0;
            foreach (var p in _patterns)
                sum += p.Confidence;
            return sum / _patterns.Count;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_memoryPath))
                {
                    var json = File.ReadAllText(_memoryPath);
                    var loaded = JsonSerializer.Deserialize<CortexState>(json);
                    if (loaded != null)
                    {
                        _state.Habits = loaded.Habits;
                        _state.Patterns = loaded.Patterns;
                        _state.Corrections = loaded.Corrections;
                        _state.NamingConventions = loaded.NamingConventions;

                        // Recharge les listes internes pour les stats
                        foreach (var category in _state.Habits)
                        {
                            foreach (var pattern in category.Value)
                            {
                                _habits.Add(new HabitRecord($"{category.Key}:{pattern.Key}", pattern.Value, DateTime.UtcNow));
                            }
                        }
                        foreach (var p in _state.Patterns)
                        {
                            _patterns.Add(new PatternRecord(p.Signature, p.Example, p.Context, 0.8));
                        }
                        foreach (var c in _state.Corrections)
                        {
                            _corrections.Add(new CorrectionRecord(c.Before, c.After, c.Reason, c.Timestamp));
                        }
                    }
                }
            }
            catch
            {
                // Mémoire corrompue : on repart vide
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_memoryPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_memoryPath, json);
            }
            catch
            {
                // Sauvegarde impossible : ignoré
            }
        }
    }

    // ── État persistant (JSON) ──
    public class CortexState
    {
        public Dictionary<string, Dictionary<string, double>> Habits { get; set; } = new();
        public List<PatternRecordLegacy> Patterns { get; set; } = new();
        public List<CorrectionRecordLegacy> Corrections { get; set; } = new();
        public Dictionary<string, string> NamingConventions { get; set; } = new();
    }

    // ── Modèles pour persistance JSON (legacy) ──
    public abstract class MemoryRecordLegacy
    {
        public DateTime Timestamp { get; set; }
    }

    public class PatternRecordLegacy : MemoryRecordLegacy
    {
        public string Signature { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public int Frequency { get; set; }
    }

    public class CorrectionRecordLegacy : MemoryRecordLegacy
    {
        public string Before { get; set; } = string.Empty;
        public string After { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    // ── Modèles pour stats en mémoire (records immuables) ──
    public record HabitRecord(string Key, double Weight, DateTime LearnedUtc);
    public record PatternRecord(string Signature, string Example, string Context, double Confidence);
    public record CorrectionRecord(string Before, string After, string Reason, DateTime TimestampUtc);

    // CortexStats est défini dans CortexEngine.cs (même namespace) : pas de redéfinition ici.
}
