// Moto.Core/Performance/PerformanceEngine.cs
using System;
using System.Collections.Generic;

namespace Moto.Core.Performance
{
    /// <summary>Modes de puissance, comme un moteur graphique.</summary>
    public enum AiPowerMode
    {
        Eco,
        Balanced,
        Turbo,
        Ultra
    }

    /// <summary>Profondeur d'analyse.</summary>
    public enum AnalysisDepth
    {
        Shallow = 1,
        Normal = 2,
        Deep = 3,
        Full = 4
    }

    /// <summary>
    /// Profil de performance : tous les réglages ajustables par le mode.
    /// </summary>
    public class PerformanceProfile
    {
        public AiPowerMode Mode { get; set; }

        /// <summary>Fréquence des scans (secondes).</summary>
        public int ScanIntervalSec { get; set; }

        /// <summary>Profondeur des analyses.</summary>
        public AnalysisDepth Depth { get; set; }

        /// <summary>Taille max des caches (entrées).</summary>
        public int CacheMaxEntries { get; set; }

        /// <summary>Vitesse des prédictions (debounce en ms).</summary>
        public int PredictionDebounceMs { get; set; }

        /// <summary>Agressivité : nombre max de suggestions.</summary>
        public int MaxSuggestions { get; set; }

        /// <summary>Agressivité : confiance minimale pour proposer.</summary>
        public double MinConfidence { get; set; }

        /// <summary>Refactor automatique activé.</summary>
        public bool AutoRefactor { get; set; }

        /// <summary>Fréquence des refactors (minutes).</summary>
        public int RefactorIntervalMin { get; set; }

        /// <summary>Documentation automatique activée.</summary>
        public bool AutoDoc { get; set; }

        /// <summary>Fréquence des mises à jour de doc (secondes).</summary>
        public int DocIntervalSec { get; set; }

        /// <summary>Auto-linking automatique.</summary>
        public bool AutoLinkAuto { get; set; }

        /// <summary>Analyse en arrière-plan.</summary>
        public bool BackgroundAnalysis { get; set; }

        /// <summary>Suggestions proactives.</summary>
        public bool ProactiveSuggestions { get; set; }

        public string Label => Mode switch
        {
            AiPowerMode.Eco => "🌱 Eco",
            AiPowerMode.Balanced => "⚖️ Balanced",
            AiPowerMode.Turbo => "🚀 Turbo",
            AiPowerMode.Ultra => "⚡ Ultra",
            _ => "Balanced"
        };

        public string Description => Mode switch
        {
            AiPowerMode.Eco => "IA lente, faible consommation, suggestions légères.",
            AiPowerMode.Balanced => "IA normale, suggestions contextuelles, prédiction multi-facteurs.",
            AiPowerMode.Turbo => "IA agressive, analyse profonde, suggestions massives, refactor avancé.",
            AiPowerMode.Ultra => "IA en continu : analyse background, auto-linking, doc auto, refactor auto.",
            _ => ""
        };

        /// <summary>Construit le profil correspondant à un mode.</summary>
        public static PerformanceProfile ForMode(AiPowerMode mode)
        {
            return mode switch
            {
                AiPowerMode.Eco => new PerformanceProfile
                {
                    Mode = AiPowerMode.Eco,
                    ScanIntervalSec = 60,
                    Depth = AnalysisDepth.Shallow,
                    CacheMaxEntries = 100,
                    PredictionDebounceMs = 1500,
                    MaxSuggestions = 3,
                    MinConfidence = 0.8,
                    AutoRefactor = false,
                    RefactorIntervalMin = 0,
                    AutoDoc = false,
                    DocIntervalSec = 0,
                    AutoLinkAuto = false,
                    BackgroundAnalysis = false,
                    ProactiveSuggestions = false
                },

                AiPowerMode.Balanced => new PerformanceProfile
                {
                    Mode = AiPowerMode.Balanced,
                    ScanIntervalSec = 15,
                    Depth = AnalysisDepth.Normal,
                    CacheMaxEntries = 500,
                    PredictionDebounceMs = 800,
                    MaxSuggestions = 6,
                    MinConfidence = 0.6,
                    AutoRefactor = false,
                    RefactorIntervalMin = 0,
                    AutoDoc = true,
                    DocIntervalSec = 30,
                    AutoLinkAuto = false,
                    BackgroundAnalysis = true,
                    ProactiveSuggestions = true
                },

                AiPowerMode.Turbo => new PerformanceProfile
                {
                    Mode = AiPowerMode.Turbo,
                    ScanIntervalSec = 5,
                    Depth = AnalysisDepth.Deep,
                    CacheMaxEntries = 2000,
                    PredictionDebounceMs = 400,
                    MaxSuggestions = 10,
                    MinConfidence = 0.5,
                    AutoRefactor = true,
                    RefactorIntervalMin = 5,
                    AutoDoc = true,
                    DocIntervalSec = 10,
                    AutoLinkAuto = true,
                    BackgroundAnalysis = true,
                    ProactiveSuggestions = true
                },

                AiPowerMode.Ultra => new PerformanceProfile
                {
                    Mode = AiPowerMode.Ultra,
                    ScanIntervalSec = 2,
                    Depth = AnalysisDepth.Full,
                    CacheMaxEntries = 5000,
                    PredictionDebounceMs = 200,
                    MaxSuggestions = 15,
                    MinConfidence = 0.4,
                    AutoRefactor = true,
                    RefactorIntervalMin = 1,
                    AutoDoc = true,
                    DocIntervalSec = 5,
                    AutoLinkAuto = true,
                    BackgroundAnalysis = true,
                    ProactiveSuggestions = true
                },

                _ => ForMode(AiPowerMode.Balanced)
            };
        }
    }

    /// <summary>
    /// Interface pour tout moteur qui veut s'adapter au profil de performance.
    /// </summary>
    public interface IPerformanceConsumer
    {
        void ApplyProfile(PerformanceProfile profile);
    }

    /// <summary>
    /// MOTO Performance Engine : ajuste tous les moteurs selon le mode choisi.
    /// </summary>
    public class PerformanceEngine
    {
        private readonly List<IPerformanceConsumer> _consumers = new();
        private AiPowerMode _modeBeforeEco = AiPowerMode.Balanced;

        /// <summary>Instance partagée, utilisée par les appels statiques EnterEcoMode/ExitEcoMode.</summary>
        public static PerformanceEngine Shared { get; } = new();

        public PerformanceProfile Current { get; private set; } =
            PerformanceProfile.ForMode(AiPowerMode.Balanced);

        /// <summary>Bascule en mode Eco (ex: un modèle local externe tourne, cf. LocalModelResourceGovernor).</summary>
        public static void EnterEcoMode()
        {
            if (Shared.Current.Mode != AiPowerMode.Eco)
                Shared._modeBeforeEco = Shared.Current.Mode;
            Shared.SetMode(AiPowerMode.Eco);
        }

        /// <summary>Revient au mode de puissance précédent (avant EnterEcoMode).</summary>
        public static void ExitEcoMode() => Shared.SetMode(Shared._modeBeforeEco);

        /// <summary>Déclenché à chaque changement de profil.</summary>
        public event Action<PerformanceProfile> ProfileChanged;

        /// <summary>Enregistre un moteur consommateur.</summary>
        public void Register(IPerformanceConsumer consumer)
        {
            if (!_consumers.Contains(consumer))
            {
                _consumers.Add(consumer);
                consumer.ApplyProfile(Current);
            }
        }

        /// <summary>Change de mode de puissance.</summary>
        public void SetMode(AiPowerMode mode)
        {
            Current = PerformanceProfile.ForMode(mode);
            ApplyToAll();
            ProfileChanged?.Invoke(Current);
        }

        /// <summary>Charge le mode depuis les paramètres.</summary>
        public void LoadFromSettings(Moto.Core.Settings.SettingsEngine settings)
        {
            var modeStr = settings.GetString("power_mode");

            var mode = modeStr switch
            {
                "Eco" => AiPowerMode.Eco,
                "Turbo" => AiPowerMode.Turbo,
                "Ultra" => AiPowerMode.Ultra,
                _ => AiPowerMode.Balanced
            };

            SetMode(mode);
        }

        private void ApplyToAll()
        {
            foreach (var consumer in _consumers)
            {
                try
                {
                    consumer.ApplyProfile(Current);
                }
                catch
                {
                    // Un consumer défaillant ne doit pas bloquer les autres.
                }
            }
        }
    }
}
