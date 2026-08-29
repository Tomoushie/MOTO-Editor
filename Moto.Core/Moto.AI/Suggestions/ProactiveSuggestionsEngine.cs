// Moto.Core/AI/Suggestions/ProactiveSuggestionsEngine.cs
// Moteur proactif avec persistance des dismiss et branchement analytics.
using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Analytics;
using Moto.Core.DevOps;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Suggestions
{
    public sealed class ProactiveSuggestion
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = "💡";
        public string Command { get; init; } = string.Empty;
        public double Confidence { get; init; }
        public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    }

    public sealed class ProactiveSuggestionsEngine
    {
        private readonly ContextualActionsEngine _actionsEngine;
        private readonly ProactiveAnalyticsEngine? _analytics;
        private readonly FeatureFlagService _featureFlags;

        // Constructeur étendu avec FeatureFlagService (paramètre optionnel pour compatibilité ascendante)
        public ProactiveSuggestionsEngine(
            ContextualActionsEngine actionsEngine,
            ProactiveAnalyticsEngine? analytics = null,
            FeatureFlagService? featureFlags = null)
        {
            _actionsEngine = actionsEngine ?? throw new ArgumentNullException(nameof(actionsEngine));
            _analytics = analytics;
            _featureFlags = featureFlags ?? new FeatureFlagService(
                SettingsEngine.Shared,
                new StructuredLogCollector());
        }

        // Méthode productrice de suggestions avec garde FeatureFlag
        public IReadOnlyList<ProactiveSuggestion> GetSuggestions(ActionContext context)
        {
            // ★ Hook FeatureFlag : désactive les suggestions proactives si le flag est off
            if (!_featureFlags.IsEnabled("feature.proactive_suggestions"))
                return Array.Empty<ProactiveSuggestion>();

            var suggestions = new List<ProactiveSuggestion>();

            foreach (var action in _actionsEngine.GetActions(context).Take(3))
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Id = action.Id,
                    Title = action.Title,
                    Description = action.Description,
                    Icon = GetActionIcon(action.Kind),
                    Command = action.Command,
                    Confidence = action.Relevance
                });
            }

            if (context.HasOpenDocument && context.HasErrors)
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Id = "fix.errors",
                    Title = "Corriger les erreurs",
                    Description = "Ce fichier contient des erreurs.",
                    Icon = "🔧",
                    Command = "/action build",
                    Confidence = 0.95
                });
            }

            // Filtre : dismiss persistants + analytics
            var result = suggestions
                .Where(s => _analytics == null || !_analytics.IsDismissed(s.Id))
                .OrderByDescending(s => s.Confidence)
                .Take(5)
                .ToList();

            // Track l'affichage
            if (_analytics != null)
            {
                foreach (var s in result)
                    _analytics.Record(AnalyticsEventKind.SuggestionShown, s.Id);
            }

            return result;
        }

        // Nouvelle méthode wrapper pour compatibilité avec l'insertion additive demandée
        public void EvaluateAndSuggest(ActionContext context)
        {
            // ★ Hook FeatureFlag : désactive les suggestions proactives si le flag est off
            if (!_featureFlags.IsEnabled("feature.proactive_suggestions"))
                return;

            // Délègue à GetSuggestions (logique existante inchangée)
            var suggestions = GetSuggestions(context);
            // Les suggestions sont retournées ; l'UI les consommera via GetSuggestions
        }

        /// <summary>L'utilisateur clique sur une suggestion → track + dismiss temporaire.</summary>
        public void RecordExecution(ProactiveSuggestion suggestion)
        {
            _analytics?.Record(AnalyticsEventKind.SuggestionExecuted, suggestion.Id);
        }

        /// <summary>L'utilisateur ferme explicitement une suggestion → persiste le dismiss.</summary>
        public void RecordPermanentDismiss(string suggestionId)
        {
            _analytics?.Record(AnalyticsEventKind.SuggestionDismissed, suggestionId);
            _analytics?.RecordDismiss(suggestionId);
        }

        private static string GetActionIcon(ContextualActionKind kind) => kind switch
        {
            ContextualActionKind.Layout => "📐",
            ContextualActionKind.Terminal => "💻",
            ContextualActionKind.Editor => "✏️",
            ContextualActionKind.Ai => "🤖",
            ContextualActionKind.Project => "🏗️",
            _ => "💡"
        };
    }
}
