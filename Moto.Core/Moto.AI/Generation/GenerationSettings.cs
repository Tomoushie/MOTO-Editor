// Moto.Core/Moto.AI/Generation/GenerationSettings.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : réglages et choix du modèle pour l'écriture assistée (édition en ligne, chat).
// Le modèle est celui des Réglages → IA Locale (« ollama_model », défaut qwen2.5-coder:7b) : le modèle « code » rapide — le banc de l'agent
// (qwen3:8b plus fiable mais 2 à 3 fois plus lent) mesure des boucles d'outils de plusieurs étapes, pas une réponse unique.
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Moto.Core.Settings;

namespace Moto.Core.AI.Generation;

public sealed class GenerationSettings
{
    public string Endpoint { get; init; } = "http://127.0.0.1:11434";

    /// <summary>« ollama_model ».</summary>
    public string Model { get; init; } = "qwen2.5-coder:7b";

    /// <summary>Basse : du code doit être reproductible (mesuré utile sur le banc de l'agent).</summary>
    public double Temperature { get; init; } = 0.2;

    public static GenerationSettings Load(SettingsEngine? engine = null)
    {
        var s = engine ?? SettingsEngine.Shared;
        return new GenerationSettings
        {
            Endpoint = OllamaChatClient.NormalizeEndpoint(s.GetString("ollama_endpoint", string.Empty)),
            Model = s.GetString("ollama_model", "qwen2.5-coder:7b").Trim(),
        };
    }
}

public static class GenerationModels
{
    /// <summary>Essayés dans cet ordre si le modèle réglé n'est pas installé.</summary>
    public static readonly IReadOnlyList<string> Alternatives = new[] { "qwen2.5-coder:7b", "qwen3:8b", "llama3.1:8b" };

    /// <summary>
    /// Le modèle à utiliser parmi ceux qu'Ollama a installés : celui des réglages s'il est là ; sinon le premier des <see cref="Alternatives"/>
    /// installé, sinon le premier modèle installé — avec une note qui le dit. Modèle null si rien n'est installé.
    /// </summary>
    public static (string? Model, string? Note) Choose(string configured, IReadOnlyCollection<string> installed)
    {
        if (installed.Count == 0) return (null, null);
        if (AgentV2Settings.IsInstalled(configured, installed)) return (configured.Trim(), null);

        var shown = string.IsNullOrWhiteSpace(configured) ? "(aucun modèle réglé)" : $"« {configured.Trim()} » n'est pas installé";
        foreach (var alternative in Alternatives)
            if (AgentV2Settings.IsInstalled(alternative, installed))
                return (alternative, $"{shown} : utilisation de « {alternative} ».");

        var first = installed.First();
        return (first, $"{shown} : utilisation de « {first} ».");
    }
}
