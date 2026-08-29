// Moto.Core/AI/Internal/Models/ProjectModel.cs
using System.Collections.Generic;

namespace Moto.Core.AI.Internal.Models
{
    /// <summary>
    /// Type de symbole détecté dans le projet.
    /// </summary>
    public enum SymbolKind
    {
        Unknown,
        Namespace,
        Class,
        Interface,
        Struct,
        Enum,
        Method,
        System,
        Component
    }

    /// <summary>
    /// Symbole détecté dans un fichier.
    /// </summary>
    public class ProjectSymbol
    {
        public string Name { get; set; } = string.Empty;
        public SymbolKind Kind { get; set; } = SymbolKind.Unknown;
        public string FilePath { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public int Line { get; set; }
    }

    /// <summary>
    /// Type de problème détecté.
    /// </summary>
    public enum IssueKind
    {
        Todo,
        NotImplementedException,
        UnbalancedBraces,
        MissingImplementation,
        MissingInterfaceForSystem,
        LargeFile,
        EmptyInterface,
        EmptyClass
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Problème détecté dans le projet.
    /// </summary>
    public class ProjectIssue
    {
        public IssueKind Kind { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string SymbolName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
    }

    /// <summary>
    /// Carte mentale du projet.
    /// C'est la représentation interne utilisée par MOTO AI.
    /// </summary>
    public class ProjectMap
    {
        public string RootPath { get; set; } = string.Empty;

        public List<string> Files { get; } = new List<string>();
        public List<ProjectSymbol> Symbols { get; } = new List<ProjectSymbol>();
        public List<ProjectIssue> Issues { get; } = new List<ProjectIssue>();

        public HashSet<string> Namespaces { get; } = new HashSet<string>();
        public HashSet<string> Modules { get; } = new HashSet<string>();

        /// <summary>
        /// Relations simples entre fichiers.
        /// Clé : fichier qui référence.
        /// Valeur : liste de symboles référencés.
        /// </summary>
        public Dictionary<string, List<string>> Relations { get; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// Nombre de lignes par fichier.
        /// </summary>
        public Dictionary<string, int> FileLineCounts { get; } = new Dictionary<string, int>();
    }
}
