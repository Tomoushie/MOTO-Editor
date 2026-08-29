// Moto.Core/AI/AutoLink/AutoLinkModels.cs
using System.Collections.Generic;

namespace Moto.Core.AI.AutoLink
{
    public enum AutoLinkIssueKind
    {
        MissingClass,
        MissingInterface,
        MissingUsing,
        MissingSystem,
        MissingMethod,
        IncompleteClass,
        IncompletePattern,
        BrokenDependency
    }

    /// <summary>
    /// Problème détecté par AutoLink dans le fichier actif.
    /// </summary>
    public class AutoLinkIssue
    {
        public AutoLinkIssueKind Kind { get; set; }
        public string SymbolName { get; set; } = string.Empty;
        public int Line { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Action proposée pour résoudre un problème.
    /// </summary>
    public class AutoLinkAction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AutoLinkIssueKind Kind { get; set; }
        public string TargetSymbol { get; set; } = string.Empty;
        public string GeneratedContent { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public bool IsInsertion { get; set; } // true = insérer dans fichier actuel, false = créer nouveau fichier
    }

    /// <summary>
    /// Résultat de l'analyse AutoLink : liste de problèmes + actions proposées.
    /// </summary>
    public class AutoLinkReport
    {
        public List<AutoLinkIssue> Issues { get; } = new();
        public List<AutoLinkAction> Actions { get; } = new();
        public string FilePath { get; set; } = string.Empty;
    }
}
