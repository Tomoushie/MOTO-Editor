// AI/v2/IntelligentAutocomplete.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Moto.Editor.AI.v2
{
    /// <summary>
    /// Autocomplétion intelligente locale.
    /// Utilise le document courant, sans dépendance cloud.
    /// </summary>
    public class IntelligentAutocomplete
    {
        private static readonly Regex WordRegex = new Regex(
            @"\\b\\w+\\b",
            RegexOptions.Compiled
        );

        public IEnumerable<AiSuggestionV2> GetSuggestions(AiContextV2 context)
        {
            if (string.IsNullOrWhiteSpace(context.Text) || context.CaretIndex < 0)
            {
                yield break;
            }

            var word = GetWordBeforeCaret(context.Text, context.CaretIndex);

            if (word.Length < 2)
            {
                yield break;
            }

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in WordRegex.Matches(context.Text))
            {
                var candidate = match.Value;

                if (candidate.Length >= word.Length &&
                    candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(candidate);
                }
            }

            foreach (var candidate in candidates.Take(10))
            {
                yield return new AiSuggestionV2
                {
                    Title = candidate,
                    Reason = "Local symbol completion.",
                    Confidence = 0.5 + Math.Min(0.3, candidate.Length / 20.0),
                    Kind = "autocomplete"
                };
            }
        }

        private string GetWordBeforeCaret(string text, int caret)
        {
            caret = Math.Min(caret, text.Length);

            int start = caret;

            while (start > 0 && char.IsLetterOrDigit(text[start - 1]))
            {
                start--;
            }

            return text.Substring(start, caret - start);
        }
    }
}
