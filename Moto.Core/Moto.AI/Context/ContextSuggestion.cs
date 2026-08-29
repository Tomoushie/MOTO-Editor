// Moto.Core/AI/Context/ContextSuggestion.cs
using System;

namespace Moto.Core.AI.Context
{
    public enum ContextSuggestionKind
    {
        CreateFile,
        ConnectSystem,
        AddUsing,
        CompleteInterface,
        AddComment,
        RenameVariable,
        GenerateModule,
        FixPattern,
        OptimizeCode
    }

    public enum ContextSuggestionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Suggestion contextuelle proposée par MOTO Context Engine.
    /// </summary>
    public class ContextSuggestion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ContextSuggestionKind Kind { get; set; }
        public ContextSuggestionPriority Priority { get; set; } = ContextSuggestionPriority.Medium;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "💡";
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public string GeneratedContent { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public bool IsInsertion { get; set; }
        public double Confidence { get; set; } = 0.5;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Icône selon le type de suggestion.
        /// </summary>
        public string GetIcon()
        {
            return Kind switch
            {
                ContextSuggestionKind.CreateFile => "📄",
                ContextSuggestionKind.ConnectSystem => "🔗",
                ContextSuggestionKind.AddUsing => "📥",
                ContextSuggestionKind.CompleteInterface => "✅",
                ContextSuggestionKind.AddComment => "💬",
                ContextSuggestionKind.RenameVariable => "✏️",
                ContextSuggestionKind.GenerateModule => "🧩",
                ContextSuggestionKind.FixPattern => "🔧",
                ContextSuggestionKind.OptimizeCode => "⚡",
                _ => "💡"
            };
        }
    }

    /// <summary>
    /// Rapport complet d'analyse contextuelle.
    /// </summary>
    public class ContextReport
    {
        public string FilePath { get; set; } = string.Empty;
        public System.Collections.Generic.List<ContextSuggestion> Suggestions { get; } = new();
        public int TotalIssues { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }
}
