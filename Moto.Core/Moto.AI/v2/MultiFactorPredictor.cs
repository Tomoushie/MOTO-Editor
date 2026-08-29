// AI/v2/MultiFactorPredictor.cs
using System;
using System.Collections.Generic;

namespace Moto.Editor.AI.v2
{
    /// <summary>
    /// Prédiction multi-facteurs.
    /// Facteurs : fréquence, récence, type de fichier, contexte workspace.
    /// </summary>
    public class MultiFactorPredictor
    {
        private sealed class EventStats
        {
            public int Count;
            public DateTime LastSeenUtc;
        }

        private readonly Dictionary<string, EventStats> _events =
            new Dictionary<string, EventStats>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enregistre un événement utilisateur.
        /// Types possibles : command, file, action, search.
        /// </summary>
        public void Record(string type, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var key = $"{type}:{value}";

            if (!_events.TryGetValue(key, out var stats))
            {
                stats = new EventStats();
                _events[key] = stats;
            }

            stats.Count++;
            stats.LastSeenUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Produit des suggestions Zen AI.
        /// </summary>
        public IEnumerable<AiSuggestionV2> GetSuggestions(AiContextV2 context)
        {
            var now = DateTime.UtcNow;

            foreach (var kv in _events)
            {
                var parts = kv.Key.Split(':', 2);

                if (parts.Length < 2)
                {
                    continue;
                }

                var type = parts[0];
                var value = parts[1];

                double recency = Math.Max(
                    0.1,
                    1.0 - (now - kv.Value.LastSeenUtc).TotalHours / 24.0
                );

                double frequency = Math.Min(1.0, kv.Value.Count / 10.0);

                double contextBoost = 1.0;

                if (type == "file" &&
                    context.FilePath != null &&
                    context.FilePath.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                {
                    contextBoost = 1.5;
                }

                if (type == "command" &&
                    context.FilePath != null &&
                    context.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                    value.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
                {
                    contextBoost = 1.2;
                }

                double score = frequency * recency * contextBoost;

                if (score < 0.15)
                {
                    continue;
                }

                yield return new AiSuggestionV2
                {
                    Title = type == "command"
                        ? $"Run {value}"
                        : $"Consider {value}",
                    Reason = "Predicted from usage patterns.",
                    Confidence = Math.Min(0.95, score),
                    Kind = "zen"
                };
            }
        }
    }
}
