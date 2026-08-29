// AI/v2/LocalRefactorEngine.cs
using System.Collections.Generic;

namespace Moto.Editor.AI.v2
{
    /// <summary>
    /// Refactor local léger.
    /// Actions sûres, rapides, sans analyse projet complète.
    /// </summary>
    public class LocalRefactorEngine
    {
        public IEnumerable<AiSuggestionV2> GetSuggestions(AiContextV2 context)
        {
            if (context.FilePath != null &&
                context.FilePath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase) &&
                context.Text.Contains("using "))
            {
                yield return new AiSuggestionV2
                {
                    Title = "Sort using directives",
                    Reason = "Light local refactor.",
                    Confidence = 0.62,
                    Kind = "refactor"
                };
            }

            if (context.Text.Contains("\t"))
            {
                yield return new AiSuggestionV2
                {
                    Title = "Convert tabs to spaces",
                    Reason = "Improves formatting consistency.",
                    Confidence = 0.55,
                    Kind = "refactor"
                };
            }

            if (context.Text.Contains("  \n") || context.Text.Contains(" \r\n"))
            {
                yield return new AiSuggestionV2
                {
                    Title = "Trim trailing whitespace",
                    Reason = "Cleaner diff and cleaner file.",
                    Confidence = 0.58,
                    Kind = "refactor"
                };
            }
        }
    }
}
