// Moto.Core/AI/Orchestration/AgentOrchestrator.cs
// Orchestrateur multi-agents : combine Cortex + Neural + Workspace + Actions + Analytics.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Suggestions;
using Moto.Core.AI.Workspace;

namespace Moto.Core.AI.Orchestration
{
    /// <summary>Résultat combiné de plusieurs agents.</summary>
    public sealed class CombinedSuggestion
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty; // cortex / neural / workspace / actions
        public double Score { get; init; }
        public string Command { get; init; } = string.Empty;
    }

    /// <summary>
    /// Orchestrateur multi-agents.
    /// Combine les suggestions de tous les agents IA avec un scoring inter-agents.
    /// Pipeline étendu : Scanner → Analyzer → Synthesizer → Connector → Validator → Scorer.
    /// </summary>
    public sealed class AgentOrchestrator
    {
        private readonly CortexEngine _cortex;
        private readonly NeuralMode _neural;
        private readonly AIWorkspace _workspace;
        private readonly ContextualActionsEngine _actions;
        private readonly ProactiveAnalyticsEngine _analytics;

        public AgentOrchestrator(
            CortexEngine cortex,
            NeuralMode neural,
            AIWorkspace workspace,
            ContextualActionsEngine actions,
            ProactiveAnalyticsEngine analytics)
        {
            _cortex = cortex ?? throw new ArgumentNullException(nameof(cortex));
            _neural = neural ?? throw new ArgumentNullException(nameof(neural));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        /// <summary>
        /// Génère des suggestions combinées de tous les agents.
        /// Scoring : pondération par agent + bonus si plusieurs agents proposent la même chose.
        /// </summary>
        public IReadOnlyList<CombinedSuggestion> GetCombinedSuggestions(
            string filePath, string content, ActionContext context)
        {
            var allSuggestions = new List<CombinedSuggestion>();

            // ── Agent 1 : Cortex (style + corrections) ──
            var cortexSuggestions = _cortex.GetSuggestions(filePath, content);
            foreach (var s in cortexSuggestions)
            {
                allSuggestions.Add(new CombinedSuggestion
                {
                    Id = $"cortex.{s.Title.GetHashCode()}",
                    Title = s.Title,
                    Description = s.Description,
                    Source = "Cortex",
                    Score = s.Confidence * 0.8, // Cortex : poids 0.8
                    Command = ""
                });
            }

            // ── Agent 2 : Actions contextuelles ──
            var actions = _actions.GetActions(context);
            foreach (var a in actions)
            {
                allSuggestions.Add(new CombinedSuggestion
                {
                    Id = $"action.{a.Id}",
                    Title = a.Title,
                    Description = a.Description,
                    Source = "Actions",
                    Score = a.Relevance * 0.7, // Actions : poids 0.7
                    Command = a.Command
                });
            }

            // ── Scoring inter-agents : bonus si même suggestion de plusieurs sources ──
            var grouped = allSuggestions
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
                .OrderByDescending(s => s.Score)
                .Take(10)
                .ToList();

            // Track l'affichage
            foreach (var s in grouped)
                _analytics.Record(AnalyticsEventKind.SuggestionShown, s.Id, s.Source);

            return grouped;
        }
    }
}
