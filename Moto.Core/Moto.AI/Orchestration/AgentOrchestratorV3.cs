// Moto.Core/AI/Orchestration/AgentOrchestratorV3.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Cortex;

namespace Moto.Core.AI.Orchestration
{
    // NOTE : CombinedSuggestion est déjà défini dans AgentOrchestrator.cs (même namespace,
    // même forme) ; on réutilise ce type ici plutôt que d'en redéclarer un second (CS0101).

    /// <summary>Contexte fourni à l'Agent Scorer pour la pondération.</summary>
    public sealed class ScoringContext
    {
        public string FilePath { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public IReadOnlyList<string> RecentActions { get; init; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, int> HistoricalStats { get; init; }
            = new Dictionary<string, int>();
    }

    /// <summary>
    /// Pipeline XENO-SSS∞ v3 :
    /// Scanner → Analyzer → Synthesizer → Connector → Validator → Scorer.
    /// Combine Cortex + Actions (+ Neural/Workspace optionnels) et pondère via AgentScorer.
    /// </summary>
    public sealed class AgentOrchestratorV3
    {
        private readonly CortexEngine? _cortex;
        private readonly ContextualActionsEngine _actions;
        private readonly ProactiveAnalyticsEngine _analytics;
        private readonly AgentScorer _scorer = new();

        public AgentOrchestratorV3(
            ContextualActionsEngine actions,
            ProactiveAnalyticsEngine analytics,
            CortexEngine? cortex = null)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _cortex = cortex; // nullable pour les tests sans workspace réel
        }

        public IReadOnlyList<CombinedSuggestion> GetCombinedSuggestionsV3(
            string filePath, string content, ActionContext context)
        {
            // 1) Scanner : collecte brute depuis tous les agents
            var scanned = ScanAllAgents(filePath, content, context);

            // 2) Analyzer : filtrage contextuel
            var analyzed = AnalyzeSuggestions(scanned, context);

            // 3) Synthesizer : fusion des suggestions similaires
            var synthesized = Synthesize(analyzed);

            // 4) Connector : lien avec l'historique (placeholder extensible)
            var connected = ConnectWithHistory(synthesized);

            // 5) Validator : cohérence + seuil minimal
            var validated = Validate(connected);

            // 6) Scorer : pondération finale
            var scoringContext = new ScoringContext
            {
                FilePath = filePath,
                Content = content,
                RecentActions = Array.Empty<string>(),
                HistoricalStats = new Dictionary<string, int>()
            };

            var ranked = _scorer.ScoreAndRank(validated, scoringContext, topN: 8);

            // Track l'affichage dans l'analytics
            foreach (var s in ranked)
                _analytics.Record(AnalyticsEventKind.SuggestionShown, s.Id, s.Source);

            return ranked;
        }

        private IReadOnlyList<CombinedSuggestion> ScanAllAgents(
            string filePath, string content, ActionContext context)
        {
            var all = new List<CombinedSuggestion>();

            // Agent Cortex (optionnel)
            if (_cortex != null)
            {
                foreach (var s in _cortex.GetSuggestions(filePath, content))
                {
                    all.Add(new CombinedSuggestion
                    {
                        Id = $"cortex.{Math.Abs(s.Title.GetHashCode())}",
                        Title = s.Title,
                        Description = s.Description,
                        Source = "Cortex",
                        Score = s.Confidence * 0.8,
                        Command = string.Empty
                    });
                }
            }

            // Agent Actions contextuelles
            foreach (var a in _actions.GetActions(context))
            {
                all.Add(new CombinedSuggestion
                {
                    Id = $"action.{a.Id}",
                    Title = a.Title,
                    Description = a.Description,
                    Source = "Actions",
                    Score = a.Relevance * 0.7,
                    Command = a.Command
                });
            }

            return all;
        }

        private static IReadOnlyList<CombinedSuggestion> AnalyzeSuggestions(
            IReadOnlyList<CombinedSuggestion> suggestions, ActionContext context)
        {
            return suggestions
                .Where(s =>
                {
                    // Les actions éditeur/IA nécessitent un document ouvert
                    if (s.Id.StartsWith("action.editor") || s.Id.StartsWith("action.ai"))
                        return context.HasOpenDocument;
                    return true;
                })
                .ToList();
        }

        private static IReadOnlyList<CombinedSuggestion> Synthesize(
            IReadOnlyList<CombinedSuggestion> suggestions)
        {
            return suggestions
                .GroupBy(s => s.Title.ToLowerInvariant())
                .Select(g =>
                {
                    var first = g.First();
                    var bonus = g.Count() > 1 ? 0.1 * (g.Count() - 1) : 0;
                    return new CombinedSuggestion
                    {
                        Id = first.Id,
                        Title = first.Title,
                        Description = first.Description,
                        Source = string.Join(" + ", g.Select(s => s.Source).Distinct()),
                        Score = Math.Clamp(first.Score + bonus, 0, 1),
                        Command = first.Command
                    };
                })
                .ToList();
        }

        private static IReadOnlyList<CombinedSuggestion> ConnectWithHistory(
            IReadOnlyList<CombinedSuggestion> suggestions)
            => suggestions; // extensible via UserHistoryEngine

        private static IReadOnlyList<CombinedSuggestion> Validate(
            IReadOnlyList<CombinedSuggestion> suggestions)
            => suggestions.Where(s => s.Score > 0.2).ToList();
    }
}
