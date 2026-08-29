// Moto.Editor/Navigation/RelevanceReport.cs
using System.Collections.Generic;

namespace Moto.Editor.Navigation
{
    /// <summary>
    /// Niveau de pertinence d'un fichier.
    /// </summary>
    public enum RelevanceLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Raison pour laquelle un fichier est jugé pertinent.
    /// </summary>
    public enum RelevanceReason
    {
        Important,          // Hub de dépendances
        Broken,             // Erreurs de syntaxe détectées
        Inconsistent,       // Interface sans implémentation, etc.
        NeedsImprovement,   // TODO, FIXME, fichier trop gros
        FrequentlyReferenced
    }

    /// <summary>
    /// Résultat de l'analyse de pertinence pour un fichier.
    /// </summary>
    public class RelevanceEntry
    {
        public string FilePath { get; set; } = string.Empty;
        public RelevanceLevel Level { get; set; }
        public RelevanceReason Reason { get; set; }
        public double Score { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rapport complet de pertinence pour un workspace.
    /// </summary>
    public class RelevanceReport
    {
        public List<RelevanceEntry> Entries { get; } = new List<RelevanceEntry>();

        /// <summary>Nombre total de fichiers analysés.</summary>
        public int TotalFilesAnalyzed { get; set; }

        /// <summary>Fichiers critiques, triés par score décroissant.</summary>
        public IReadOnlyList<RelevanceEntry> GetCritical()
        {
            var critical = new List<RelevanceEntry>();
            foreach (var entry in Entries)
            {
                if (entry.Level == RelevanceLevel.Critical)
                {
                    critical.Add(entry);
                }
            }
            critical.Sort((a, b) => b.Score.CompareTo(a.Score));
            return critical;
        }

        /// <summary>Fichiers à améliorer, triés par score décroissant.</summary>
        public IReadOnlyList<RelevanceEntry> GetNeedsImprovement()
        {
            var list = new List<RelevanceEntry>();
            foreach (var entry in Entries)
            {
                if (entry.Reason == RelevanceReason.NeedsImprovement)
                {
                    list.Add(entry);
                }
            }
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }
    }
}
