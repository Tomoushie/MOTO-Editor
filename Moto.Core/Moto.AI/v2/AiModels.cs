// AI/v2/AiModels.cs
using System;

namespace Moto.Editor.AI.v2
{
    /// <summary>
    /// Contexte fourni à MOTO AI v2.
    /// </summary>
    public class AiContextV2
    {
        public string WorkspacePath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int CaretIndex { get; set; }
    }

    /// <summary>
    /// Suggestion produite par MOTO AI v2.
    /// </summary>
    public class AiSuggestionV2
    {
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Kind { get; set; } = "generic";
        public Action Apply { get; set; }
    }
}
