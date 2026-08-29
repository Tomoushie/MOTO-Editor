// Moto.Core/AI/Internal/PerformanceEngine.cs
using System;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Modes de puissance de MOTO AI.
    /// S'adapte à la puissance du PC et aux besoins de l'utilisateur.
    /// </summary>
    public enum AiPowerMode
    {
        /// <summary>
        /// Suggestions légères, analyse minimale.
        /// Pour les PC modestes ou les sessions rapides.
        /// </summary>
        Eco,

        /// <summary>
        /// Suggestions normales, analyse contextuelle.
        /// Mode par défaut.
        /// </summary>
        Balanced,

        /// <summary>
        /// Analyse profonde, suggestions agressives.
        /// Pour les sessions de refactor intensif.
        /// </summary>
        Turbo,

        /// <summary>
        /// Analyse continue, génération automatique, refactor automatique,
        /// documentation automatique. MOTO AI tourne en permanence.
        /// </summary>
        Ultra
    }

    /// <summary>
    /// Configuration du moteur selon le mode choisi.
    /// </summary>
    public class PerformanceConfig
    {
        public AiPowerMode Mode { get; set; } = AiPowerMode.Balanced;

        /// <summary>Nombre max de fichiers à analyser.</summary>
        public int MaxFilesToAnalyze { get; set; } = 500;

        /// <summary>Nombre max de symboles à indexer.</summary>
        public int MaxSymbolsToIndex { get; set; } = 2000;

        /// <summary>Activer l'analyse continue.</summary>
        public bool ContinuousAnalysis { get; set; } = false;

        /// <summary>Activer la génération automatique.</summary>
        public bool AutoGenerate { get; set; } = false;

        /// <summary>Activer le refactor automatique.</summary>
        public bool AutoRefactor { get; set; } = false;

        /// <summary>Activer la documentation automatique.</summary>
        public bool AutoDoc { get; set; } = false;

        /// <summary>Délai entre deux analyses (ms).</summary>
        public int AnalysisIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Crée la configuration pour un mode donné.
        /// </summary>
        public static PerformanceConfig ForMode(AiPowerMode mode)
        {
            return mode switch
            {
                AiPowerMode.Eco => new PerformanceConfig
                {
                    Mode = AiPowerMode.Eco,
                    MaxFilesToAnalyze = 100,
                    MaxSymbolsToIndex = 500,
                    ContinuousAnalysis = false,
                    AutoGenerate = false,
                    AutoRefactor = false,
                    AutoDoc = false,
                    AnalysisIntervalMs = 10000
                },

                AiPowerMode.Balanced => new PerformanceConfig
                {
                    Mode = AiPowerMode.Balanced,
                    MaxFilesToAnalyze = 500,
                    MaxSymbolsToIndex = 2000,
                    ContinuousAnalysis = false,
                    AutoGenerate = false,
                    AutoRefactor = false,
                    AutoDoc = true,
                    AnalysisIntervalMs = 5000
                },

                AiPowerMode.Turbo => new PerformanceConfig
                {
                    Mode = AiPowerMode.Turbo,
                    MaxFilesToAnalyze = 2000,
                    MaxSymbolsToIndex = 10000,
                    ContinuousAnalysis = true,
                    AutoGenerate = true,
                    AutoRefactor = true,
                    AutoDoc = true,
                    AnalysisIntervalMs = 2000
                },

                AiPowerMode.Ultra => new PerformanceConfig
                {
                    Mode = AiPowerMode.Ultra,
                    MaxFilesToAnalyze = 5000,
                    MaxSymbolsToIndex = 50000,
                    ContinuousAnalysis = true,
                    AutoGenerate = true,
                    AutoRefactor = true,
                    AutoDoc = true,
                    AnalysisIntervalMs = 500
                },

                _ => new PerformanceConfig()
            };
        }
    }

    /// <summary>
    /// Moteur de performance.
    /// Adapte le comportement de MOTO AI selon la puissance disponible.
    /// </summary>
    public class PerformanceEngine
    {
        public PerformanceConfig CurrentConfig { get; private set; } = PerformanceConfig.ForMode(AiPowerMode.Balanced);

        public event Action<AiPowerMode> ModeChanged;

        /// <summary>
        /// Change le mode de puissance.
        /// </summary>
        public void SetMode(AiPowerMode mode)
        {
            CurrentConfig = PerformanceConfig.ForMode(mode);
            ModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// Détermine si MOTO AI doit lancer une analyse maintenant.
        /// </summary>
        public bool ShouldAnalyze(DateTime lastAnalysisUtc)
        {
            if (!CurrentConfig.ContinuousAnalysis)
            {
                return false;
            }

            var elapsed = (DateTime.UtcNow - lastAnalysisUtc).TotalMilliseconds;
            return elapsed >= CurrentConfig.AnalysisIntervalMs;
        }

        /// <summary>
        /// Détermine si MOTO AI peut générer automatiquement.
        /// </summary>
        public bool CanAutoGenerate => CurrentConfig.AutoGenerate;

        /// <summary>
        /// Détermine si MOTO AI peut refactoriser automatiquement.
        /// </summary>
        public bool CanAutoRefactor => CurrentConfig.AutoRefactor;

        /// <summary>
        /// Détermine si MOTO AI doit documenter automatiquement.
        /// </summary>
        public bool ShouldAutoDoc => CurrentConfig.AutoDoc;
    }
}
