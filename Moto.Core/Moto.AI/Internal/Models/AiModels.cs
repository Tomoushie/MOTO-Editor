// Moto.Core/AI/Internal/Models/AiModels.cs (v2)
using System.Collections.Generic;

namespace Moto.Core.AI.Internal.Models
{
    public enum AiMode { Beginner, Expert }

    public enum AiIntentKind
    {
        Unknown, UnderstandProject, GenerateModule, GenerateArchitecture,
        FixProject, ImproveProject, ExplainCode, TeachConcept,
        AutoLink, AutoDoc, AutoPort, Search
    }

    public class AiIntent
    {
        public AiIntentKind Kind { get; set; } = AiIntentKind.Unknown;
        public string RawText { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    public class AiRequest
    {
        public string UserText { get; set; } = string.Empty;
        public string WorkspacePath { get; set; } = string.Empty;
        public string TargetFile { get; set; } = string.Empty;
        public AiMode Mode { get; set; } = AiMode.Beginner;
    }

    /// <summary>Cible de navigation : fichier + ligne + explication du lien.</summary>
    public class NavigationTarget
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public string ContextLine { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    public class AiResponse
    {
        public bool Success { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;

        /// <summary>Score de santé si une analyse santé a été faite (-1 sinon).</summary>
        public int HealthScore { get; set; } = -1;

        public List<AiStep> Steps { get; } = new List<AiStep>();
        public List<AiFileChange> FileChanges { get; } = new List<AiFileChange>();
        public List<AiSuggestion> Suggestions { get; } = new List<AiSuggestion>();
        public List<NavigationTarget> NavigationTargets { get; } = new List<NavigationTarget>();
    }

    public class AiStep
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "done";
        public string Details { get; set; } = string.Empty;
    }

    public class AiFileChange
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public FileChangeType ChangeType { get; set; } = FileChangeType.Create;
    }

    public enum FileChangeType { Create, Update, Delete }

    public class AiSuggestion
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string ActionId { get; set; } = string.Empty;
    }
}
