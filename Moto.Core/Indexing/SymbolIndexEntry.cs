// Moto.Editor/Indexing/SymbolIndexEntry.cs
using System;

namespace Moto.Editor.Indexing
{
    /// <summary>
    /// Type de symbole détecté dans l'index.
    /// </summary>
    public enum SymbolKind
    {
        Unknown,
        Class,
        Interface,
        Struct,
        Enum,
        Record,
        Method,
        Property,
        Namespace,
        System  // Convention Snake2000 : classe finissant par "System"
    }

    /// <summary>
    /// Entrée d'index légère.
    /// Conçue pour être stockée en mémoire par milliers sans overhead.
    /// </summary>
    public readonly struct SymbolIndexEntry
    {
        /// <summary>Nom du symbole (ex: "AgentScanner").</summary>
        public string Name { get; }

        /// <summary>Chemin absolu du fichier.</summary>
        public string FilePath { get; }

        /// <summary>Namespace contenant, si détecté.</summary>
        public string Namespace { get; }

        /// <summary>Type de symbole.</summary>
        public SymbolKind Kind { get; }

        /// <summary>Numéro de ligne (1-based). Utile pour "Jump to".</summary>
        public int Line { get; }

        public SymbolIndexEntry(string name, string filePath, string ns, SymbolKind kind, int line)
        {
            Name = name;
            FilePath = filePath;
            Namespace = ns;
            Kind = kind;
            Line = line;
        }

        public override string ToString() => $"{Kind} {Name} ({Namespace}) @ {FilePath}:{Line}";
    }
}
