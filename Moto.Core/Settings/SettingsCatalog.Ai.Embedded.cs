namespace Moto.Core.Settings;

/// <summary>
/// Paramètres du moteur IA embarqué (InferenceHost).
/// Fichier partial de SettingsCatalog.
/// </summary>
public static partial class SettingsCatalog
{
    private static void RegisterEmbeddedAiSettings()
    {
        // ─── Activation du moteur embarqué ───
        Register(new SettingDefinition(
            id: "ai.embedded.enabled",
            category: "AI",
            title: "Activer le moteur IA embarqué",
            description: "Utilise un modèle local embarqué quand Ollama est indisponible.",
            type: SettingType.Toggle,
            defaultValue: false));

        // ─── Choix du modèle ───
        Register(new SettingDefinition(
            id: "ai.embedded.modelChoice",
            category: "AI",
            title: "Modèle embarqué",
            description: "Modèle à utiliser pour l'inférence locale.",
            type: SettingType.Enum,
            defaultValue: "phi-3-mini",
            enumValues: new[]
            {
                ("phi-3-mini", "Phi-3 Mini (3.8B, Q4)"),
                ("qwen2.5-1.5b", "Qwen 2.5 (1.5B, Q4)"),
                ("llama-3.2-1b", "Llama 3.2 (1B, Q4)"),
                ("llama-3.2-3b", "Llama 3.2 (3B, Q4)")
            }));

        // ─── Tier de performance forcé ───
        Register(new SettingDefinition(
            id: "ai.embedded.forcedTier",
            category: "AI",
            title: "Tier de performance",
            description: "Force un tier spécifique ou laisse le gouverneur décider.",
            type: SettingType.Enum,
            defaultValue: "auto",
            enumValues: new[]
            {
                ("auto", "Automatique (AdaptiveResourceGovernor)"),
                ("lite", "Lite (500M, latence minimale)"),
                ("standard", "Standard (1.5B, équilibré)"),
                ("full", "Full (7B, qualité maximale)")
            }));

        // ─── Mode éco : décharge automatique ───
        Register(new SettingDefinition(
            id: "ai.embedded.ecoMode",
            category: "AI",
            title: "Mode éco (décharge RAM)",
            description: "Décharge le modèle quand la RAM système passe sous le seuil.",
            type: SettingType.Toggle,
            defaultValue: true));

        // ─── Seuil RAM pour le mode éco (Mo) ───
        Register(new SettingDefinition(
            id: "ai.embedded.ecoThresholdMb",
            category: "AI",
            title: "Seuil RAM mode éco (Mo)",
            description: "RAM disponible minimale avant décharge du modèle.",
            type: SettingType.Int,
            defaultValue: 1024,
            min: 256,
            max: 8192));

        // ─── Mode éco permanent ───
        Register(new SettingDefinition(
            id: "ai.embedded.ecoPermanent",
            category: "AI",
            title: "Mode éco permanent",
            description: "Force le modèle en Idle même en activité.",
            type: SettingType.Toggle,
            defaultValue: false));

        // ─── Notifications toast ───
        Register(new SettingDefinition(
            id: "ai.embedded.notifyDownload",
            category: "AI",
            title: "Notifications de téléchargement",
            description: "Affiche un toast quand un modèle est téléchargé.",
            type: SettingType.Toggle,
            defaultValue: true));

        Register(new SettingDefinition(
            id: "ai.embedded.notifyBenchmark",
            category: "AI",
            title: "Notifications de benchmark",
            description: "Affiche un toast quand un benchmark se termine.",
            type: SettingType.Toggle,
            defaultValue: true));

        // ─── Mode mémoire-mapped ───
        Register(new SettingDefinition(
            id: "ai.embedded.useMemoryMapping",
            category: "AI",
            title: "Mode mémoire-mapped",
            description: "Utilise le chargement via memory-mapped pour réduire la RAM.",
            type: SettingType.Toggle,
            defaultValue: true));

        // Ajouter dans RegisterEmbeddedAiSettings()
        Register(new SettingDefinition(
            id: "ai.embedded.useMemoryMapping",
            category: "AI",
            title: "Memory-mapped inference",
            description: "Charge les modèles via memory-mapping pour réduire la RAM de ~40%.",
            type: SettingType.Toggle,
            defaultValue: true));

        Register(new SettingDefinition(
            id: "ai.embedded.parallelThreads",
            category: "AI",
            title: "Nombre de threads",
            description: "Nombre de threads à utiliser pour le parallel decoding.",
            type: SettingType.Int,
            defaultValue: 4,
            min: 1,
            max: 16));

        Register(new SettingDefinition(
            id: "ai.embedded.enableParallelDecoding",
            category: "AI",
            title: "Parallel decoding",
            description: "Génère plusieurs tokens simultanément via thread pool.",
            type: SettingType.Toggle,
            defaultValue: true));

        Register(new SettingDefinition(
            id: "ai.embedded.parallelThreads",
            category: "AI",
            title: "Threads parallèles",
            description: "Nombre de threads pour le parallel decoding.",
            type: SettingType.Int,
            defaultValue: 4,
            min: 1,
            max: 16));

        Register(new SettingDefinition(
            id: "ai.embedded.enableKvCacheCompression",
            category: "AI",
            title: "KV-cache compression",
            description: "Quantifie le cache attention (FP16 → INT8) pour réduire la RAM de ~50%",
            type: SettingType.Toggle,
            defaultValue: true));

        Register(new SettingDefinition(
            id: "ai.embedded.enableQuantizationSwitching",
            category: "AI",
            title: "Dynamic quantization switching",
            description: "Bascule automatiquement Q4 → Q3 → Q2 selon la charge RAM/CPU.",
            type: SettingType.Toggle,
            defaultValue: true));

        Register(new SettingDefinition(
            id: "ai.embedded.enableThermalSwitching",
            category: "AI",
            title: "Auto-tier switching thermique",
            description: "Bascule vers le tier Lite si la température CPU/GPU dépasse le seuil.",
            type: SettingType.Toggle,
            defaultValue: true));

        Register(new SettingDefinition(
            id: "ai.embedded.thermalThreshold",
            category: "AI",
            title: "Seuil thermique (°C)",
            description: "Température maximale avant bascule vers tier Lite.",
            type: SettingType.Int,
            defaultValue: 85,
            min: 60,
            max: 100));

        Register(new SettingDefinition(
            id: "ai.embedded.performanceMaxMode",
            category: "AI",
            title: "Mode Performance Max",
            description: "Active simultanément mmap + parallel decoding + KV-cache compression.",
            type: SettingType.Toggle,
            defaultValue: false));
    }
}
