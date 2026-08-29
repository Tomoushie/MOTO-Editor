// Moto.Core/AI/Orchestration/AgentScorer.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.AI.Orchestration
{
    /// <summary>
    /// Agent Scorer : dernière étape du pipeline XENO-SSS∞ v3.
    /// Pondère les suggestions selon pertinence, confiance, historique, fraîcheur
    /// et bonus multi-agents.
    /// </summary>
    public sealed class AgentScorer
    {
        private const double RelevanceWeight = 0.35;
        private const double ConfidenceWeight = 0.25;
        private const double HistoryWeight = 0.20;
        private const double FreshnessWeight = 0.10;
        private const double MultiAgentBonus = 0.10;

        /// <summary>Score une suggestion combinée selon plusieurs facteurs.</summary>
        public double Score(CombinedSuggestion suggestion, ScoringContext context)
        {
            if (suggestion is null) return 0;

            double relevance = ComputeRelevance(suggestion, context);
            double confidence = suggestion.Score;
            double history = ComputeHistoryBonus(suggestion, context);
            double freshness = ComputeFreshness(suggestion);
            double multiAgent = suggestion.Source.Contains("+") ? MultiAgentBonus : 0;

            var finalScore =
                (relevance * RelevanceWeight) +
                (confidence * ConfidenceWeight) +
                (history * HistoryWeight) +
                (freshness * FreshnessWeight) +
                multiAgent;

            return Math.Clamp(finalScore, 0, 1);
        }

        /// <summary>Score et classe les suggestions, en gardant les topN.</summary>
        public IReadOnlyList<CombinedSuggestion> ScoreAndRank(
            IReadOnlyList<CombinedSuggestion> suggestions,
            ScoringContext context,
            int topN = 5)
        {
            return suggestions
                .Select(s => new CombinedSuggestion
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    Source = s.Source,
                    Score = Score(s, context),
                    Command = s.Command
                })
                .OrderByDescending(s => s.Score)
                .Take(topN)
                .ToList();
        }

        private static double ComputeRelevance(CombinedSuggestion suggestion, ScoringContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Content)) return 0.5;

            var titleWords = suggestion.Title
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (titleWords.Length == 0) return 0.5;

            var contentLower = context.Content.ToLowerInvariant();
            var matches = titleWords.Count(w => contentLower.Contains(w));
            return (double)matches / titleWords.Length;
        }

        private static double ComputeHistoryBonus(CombinedSuggestion suggestion, ScoringContext context)
        {
            if (context.HistoricalStats.TryGetValue(suggestion.Id, out var count))
                return Math.Min(1.0, count / 10.0);
            return 0;
        }

        private static double ComputeFreshness(CombinedSuggestion suggestion)
            => 0.8; // extensible avec un timestamp réel
    }
}
