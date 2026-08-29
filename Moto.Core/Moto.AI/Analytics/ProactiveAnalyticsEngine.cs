// Moto.Core/AI/Analytics/ProactiveAnalyticsEngine.cs
// Analytics centralisé : suggestions proactives + commandes palette.
// Persiste dans .moto/analytics.json pour améliorer le moteur.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Moto.Core.AI.Analytics
{
    /// <summary>Type d'événement analytics.</summary>
    public enum AnalyticsEventKind
    {
        SuggestionShown,
        SuggestionExecuted,
        SuggestionDismissed,
        PaletteCommandExecuted
    }

    /// <summary>Événement analytics horodaté.</summary>
    public sealed class AnalyticsEvent
    {
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public AnalyticsEventKind Kind { get; init; }
        public string ItemId { get; init; } = string.Empty;
        public string? Context { get; init; }
    }

    /// <summary>Stats agrégées pour un item (commande ou suggestion).</summary>
    public sealed class ItemStats
    {
        public string ItemId { get; init; } = string.Empty;
        public int ShownCount { get; init; }
        public int ExecutedCount { get; init; }
        public int DismissedCount { get; init; }
        public double ExecutionRate => ShownCount > 0 ? (double)ExecutedCount / ShownCount : 0.0;
    }

    /// <summary>
    /// Moteur d'analytics IA.
    /// - Tracke suggestions proactives et commandes palette
    /// - Persiste les stats dans .moto/analytics.json
    /// - Fournit des rapports pour améliorer le moteur
    /// </summary>
    public sealed class ProactiveAnalyticsEngine : IDisposable
    {
        private readonly string _analyticsPath;
        private readonly AnalyticsState _state = new();
        private readonly object _lock = new();
        private readonly SemaphoreSlim _saveGate = new(1, 1);

        // Limite pour éviter une croissance infinie
        private const int MaxEvents = 10_000;
        private const int MaxDismissed = 1_000;

        public ProactiveAnalyticsEngine(string workspaceRoot)
        {
            ArgumentNullException.ThrowIfNull(workspaceRoot);
            var motoDir = Path.Combine(workspaceRoot, ".moto");
            Directory.CreateDirectory(motoDir);
            _analyticsPath = Path.Combine(motoDir, "analytics.json");
            Load();
        }

        /// <summary>Enregistre un événement analytics.</summary>
        public void Record(AnalyticsEventKind kind, string itemId, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;

            lock (_lock)
            {
                _state.Events.Add(new AnalyticsEvent
                {
                    Kind = kind,
                    ItemId = itemId,
                    Context = context
                });

                // Maintient la taille raisonnable
                if (_state.Events.Count > MaxEvents)
                {
                    _state.Events = _state.Events
                        .Skip(_state.Events.Count - MaxEvents)
                        .ToList();
                }

                UpdateAggregates(kind, itemId);
            }

            _ = SaveAsync();
        }

        /// <summary>Marque une suggestion comme dismissée (plus jamais montrée).</summary>
        public void RecordDismiss(string suggestionId)
        {
            lock (_lock)
            {
                _state.DismissedIds.Add(suggestionId);

                // Limite la liste des dismissés
                if (_state.DismissedIds.Count > MaxDismissed)
                {
                    var toRemove = _state.DismissedIds.Count - MaxDismissed;
                    for (int i = 0; i < toRemove; i++)
                        _state.DismissedIds.RemoveAt(0);
                }
            }

            _ = SaveAsync();
        }

        /// <summary>Vérifie si une suggestion a été dismissée.</summary>
        public bool IsDismissed(string suggestionId)
        {
            lock (_lock)
            {
                return _state.DismissedIds.Contains(suggestionId);
            }
        }

        /// <summary>Retourne toutes les suggestions dismissées.</summary>
        public IReadOnlyList<string> GetDismissedIds()
        {
            lock (_lock)
            {
                return _state.DismissedIds.ToList().AsReadOnly();
            }
        }

        /// <summary>Retourne les stats agrégées pour tous les items.</summary>
        public IReadOnlyList<ItemStats> GetAllStats()
        {
            lock (_lock)
            {
                return _state.Aggregates.Values.ToList().AsReadOnly();
            }
        }

        /// <summary>Top N commandes palette les plus utilisées.</summary>
        public IReadOnlyList<ItemStats> GetTopPaletteCommands(int top = 10)
        {
            lock (_lock)
            {
                return _state.Aggregates.Values
                    .Where(s => s.ItemId.StartsWith("palette.", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.ExecutedCount)
                    .Take(top)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>Top N suggestions les plus exécutées.</summary>
        public IReadOnlyList<ItemStats> GetTopSuggestions(int top = 10)
        {
            lock (_lock)
            {
                return _state.Aggregates.Values
                    .Where(s => s.ItemId.StartsWith("suggestion.", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.ExecutionRate)
                    .Take(top)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>Suggestions avec le plus faible taux d'exécution (à améliorer).</summary>
        public IReadOnlyList<ItemStats> GetUnderperformingSuggestions(int top = 10, double maxRate = 0.1)
        {
            lock (_lock)
            {
                return _state.Aggregates.Values
                    .Where(s => s.ItemId.StartsWith("suggestion.", StringComparison.OrdinalIgnoreCase))
                    .Where(s => s.ShownCount >= 5) // au moins 5 affichages pour être significatif
                    .Where(s => s.ExecutionRate <= maxRate)
                    .OrderBy(s => s.ExecutionRate)
                    .Take(top)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>Rapport synthétique pour affichage UI.</summary>
        public string GetReport()
        {
            lock (_lock)
            {
                var totalShown = _state.Events.Count(e => e.Kind == AnalyticsEventKind.SuggestionShown);
                var totalExecuted = _state.Events.Count(e => e.Kind == AnalyticsEventKind.SuggestionExecuted);
                var totalDismissed = _state.Events.Count(e => e.Kind == AnalyticsEventKind.SuggestionDismissed);
                var totalPalette = _state.Events.Count(e => e.Kind == AnalyticsEventKind.PaletteCommandExecuted);

                return $"📊 Analytics : {totalShown} suggestions vues · " +
                       $"{totalExecuted} exécutées · {totalDismissed} dismissées · " +
                       $"{totalPalette} commandes palette";
            }
        }

        private void UpdateAggregates(AnalyticsEventKind kind, string itemId)
        {
            var key = kind == AnalyticsEventKind.PaletteCommandExecuted
                ? $"palette.{itemId}"
                : $"suggestion.{itemId}";

            if (!_state.Aggregates.TryGetValue(key, out var stats))
            {
                stats = new ItemStats { ItemId = key };
                _state.Aggregates[key] = stats;
            }

            // Recalcule les compteurs depuis les événements récents
            var events = _state.Events.Where(e => e.ItemId == itemId).ToList();
            var newStats = new ItemStats
            {
                ItemId = key,
                ShownCount = events.Count(e => e.Kind == AnalyticsEventKind.SuggestionShown),
                ExecutedCount = events.Count(e =>
                    e.Kind == AnalyticsEventKind.SuggestionExecuted ||
                    e.Kind == AnalyticsEventKind.PaletteCommandExecuted),
                DismissedCount = events.Count(e => e.Kind == AnalyticsEventKind.SuggestionDismissed)
            };
            _state.Aggregates[key] = newStats;
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_analyticsPath)) return;
                var json = File.ReadAllText(_analyticsPath);
                var loaded = JsonSerializer.Deserialize<AnalyticsState>(json);
                if (loaded != null)
                {
                    _state.Events = loaded.Events;
                    _state.Aggregates = loaded.Aggregates;
                    _state.DismissedIds = loaded.DismissedIds;
                }
            }
            catch
            {
                // Analytics corrompue : on repart vide (non critique)
            }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            await _saveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(_analyticsPath, json).ConfigureAwait(false);
            }
            catch
            {
                // Sauvegarde best-effort (non bloquant)
            }
            finally
            {
                _saveGate.Release();
            }
        }

        public void Dispose() => _saveGate.Dispose();

        private sealed class AnalyticsState
        {
            public List<AnalyticsEvent> Events { get; set; } = new();
            public Dictionary<string, ItemStats> Aggregates { get; set; } = new();
            public List<string> DismissedIds { get; set; } = new();
        }
    }
}
