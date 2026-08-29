// Moto.Core/AI/Cortex/CortexBehavior.cs
namespace Moto.Core.AI.Cortex
{
    /// <summary>
    /// Modèles de comportement adaptatifs du Cortex Engine.
    /// </summary>
    public enum CortexBehaviorMode
    {
        Beginner,   // Pédagogique, explique tout, guide pas à pas
        Balanced,   // Normal, suggestions contextuelles
        Expert,     // Concis, suggestions avancées, moins d'explications
        Turbo,      // Agressif, refactor auto, génération massive
        Ultra       // Continu, analyse background, auto-everything
    }

    /// <summary>
    /// Configuration du comportement adaptatif.
    /// </summary>
    public class CortexBehaviorConfig
    {
        public CortexBehaviorMode Mode { get; set; } = CortexBehaviorMode.Balanced;

        /// <summary>Fréquence des suggestions (0 = jamais, 1 = toujours).</summary>
        public double SuggestionFrequency { get; set; } = 0.5;

        /// <summary>Niveau de détail des explications (0 = minimal, 1 = maximal).</summary>
        public double ExplanationLevel { get; set; } = 0.5;

        /// <summary>Active le refactor automatique.</summary>
        public bool AutoRefactor { get; set; } = false;

        /// <summary>Active la génération automatique de code.</summary>
        public bool AutoGenerate { get; set; } = false;

        /// <summary>Active la documentation automatique.</summary>
        public bool AutoDocument { get; set; } = false;

        /// <summary>Seuil de confiance minimal pour proposer une action.</summary>
        public double ConfidenceThreshold { get; set; } = 0.6;

        /// <summary>Crée la configuration pour un mode donné.</summary>
        public static CortexBehaviorConfig ForMode(CortexBehaviorMode mode)
        {
            return mode switch
            {
                CortexBehaviorMode.Beginner => new CortexBehaviorConfig
                {
                    Mode = CortexBehaviorMode.Beginner,
                    SuggestionFrequency = 0.8,
                    ExplanationLevel = 1.0,
                    AutoRefactor = false,
                    AutoGenerate = false,
                    AutoDocument = false,
                    ConfidenceThreshold = 0.4
                },
                CortexBehaviorMode.Balanced => new CortexBehaviorConfig
                {
                    Mode = CortexBehaviorMode.Balanced,
                    SuggestionFrequency = 0.5,
                    ExplanationLevel = 0.5,
                    AutoRefactor = false,
                    AutoGenerate = false,
                    AutoDocument = true,
                    ConfidenceThreshold = 0.6
                },
                CortexBehaviorMode.Expert => new CortexBehaviorConfig
                {
                    Mode = CortexBehaviorMode.Expert,
                    SuggestionFrequency = 0.3,
                    ExplanationLevel = 0.2,
                    AutoRefactor = true,
                    AutoGenerate = false,
                    AutoDocument = true,
                    ConfidenceThreshold = 0.7
                },
                CortexBehaviorMode.Turbo => new CortexBehaviorConfig
                {
                    Mode = CortexBehaviorMode.Turbo,
                    SuggestionFrequency = 0.9,
                    ExplanationLevel = 0.1,
                    AutoRefactor = true,
                    AutoGenerate = true,
                    AutoDocument = true,
                    ConfidenceThreshold = 0.5
                },
                CortexBehaviorMode.Ultra => new CortexBehaviorConfig
                {
                    Mode = CortexBehaviorMode.Ultra,
                    SuggestionFrequency = 1.0,
                    ExplanationLevel = 0.0,
                    AutoRefactor = true,
                    AutoGenerate = true,
                    AutoDocument = true,
                    ConfidenceThreshold = 0.3
                },
                _ => ForMode(CortexBehaviorMode.Balanced)
            };
        }
    }
}
