// AI/v2/MotoAiV2.cs
using System.Collections.Generic;
using System.Linq;

namespace Moto.Editor.AI.v2
{
    /// <summary>
    /// MOTO AI v2 : IA contextuelle avancée.
    /// Combine prédiction multi-facteurs, autocomplétion et refactor local.
    /// </summary>
    public class MotoAiV2
    {
        private readonly MultiFactorPredictor _predictor;
        private readonly IntelligentAutocomplete _autocomplete;
        private readonly LocalRefactorEngine _refactor;

        public MotoAiV2(
            MultiFactorPredictor predictor,
            IntelligentAutocomplete autocomplete,
            LocalRefactorEngine refactor)
        {
            _predictor = predictor;
            _autocomplete = autocomplete;
            _refactor = refactor;
        }

        /// <summary>
        /// Retourne les suggestions contextuelles classées par confiance.
        /// </summary>
        public IReadOnlyList<AiSuggestionV2> GetSuggestions(AiContextV2 context)
        {
            var suggestions = new List<AiSuggestionV2>();

            suggestions.AddRange(_autocomplete.GetSuggestions(context));
            suggestions.AddRange(_predictor.GetSuggestions(context));
            suggestions.AddRange(_refactor.GetSuggestions(context));

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .Take(20)
                .ToList();
        }
    }
}
