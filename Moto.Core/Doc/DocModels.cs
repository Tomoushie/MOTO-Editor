// Moto.Core/Doc/DocModels.cs
using System.Collections.Generic;

namespace Moto.Core.Doc
{
    /// <summary>Types de documentation générés.</summary>
    public enum DocKind
    {
        Readme,
        Structure,
        Arborescence,
        Modules,
        Systems,
        Architecture
    }

    /// <summary>Fichier de documentation généré.</summary>
    public class DocFile
    {
        public DocKind Kind { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int LineCount { get; set; }
    }

    /// <summary>Rapport de génération de documentation.</summary>
    public class DocReport
    {
        public List<DocFile> Files { get; } = new();
        public string ProjectName { get; set; } = string.Empty;
        public int TotalSymbols { get; set; }
        public int TotalFiles { get; set; }
        public System.DateTime GeneratedAt { get; set; } = System.DateTime.UtcNow;
    }
}
