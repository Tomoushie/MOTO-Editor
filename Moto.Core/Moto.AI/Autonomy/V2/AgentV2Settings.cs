// Moto.Core/Moto.AI/Autonomy/V2/AgentV2Settings.cs
// ★ AJOUT (24/09, "écriture agentique fonctionnelle") : les réglages de l'agent v2, lus dans le catalogue « IA Locale »
// (SettingsCatalog.cs). Tous ont une valeur par défaut choisie d'après le banc d'essai du 24/09 (Moto.Tools.AgentBench),
// pas d'après une impression : voir les commentaires de PreferredModels et de ToolMode.
using Moto.Core.Settings;

namespace Moto.Core.AI.Autonomy.V2;

public sealed class AgentV2Settings
{
    public const string EngineV2 = "v2";
    public const string EngineV1 = "v1";

    /// <summary>
    /// Modèles essayés dans cet ordre quand « agent_model » est vide. Classement du banc d'essai (8 tâches sur un mini-projet,
    /// sortie contrainte, 2 passages par modèle — voir la mémoire « moto-editor-agent-v2-bench-2026-09 ») :
    /// qwen3:8b 14/16, qwen2.5-coder:7b 12/16, llama3.1:8b 8/16. qwen3:8b est plus lent (~24 s contre ~9 s par tâche) mais plus
    /// fiable. Les 14 milliards de paramètres ne font pas mieux (un seul passage : qwen2.5-coder:14b 7/8 en 42 s par tâche,
    /// qwen2.5:14b 6/8 en 51 s) et sont deux fois plus lents. Le modèle par défaut du chat (qwen2.5-coder:7b) reste le dernier recours.
    /// </summary>
    public static readonly IReadOnlyList<string> PreferredModels = new[] { "qwen3:8b", "qwen2.5-coder:7b" };

    /// <summary>« v2 » (défaut) ou « v1 » (ancienne boucle texte, gardée en secours).</summary>
    public string Engine { get; init; } = EngineV2;

    public string Endpoint { get; init; } = "http://127.0.0.1:11434";

    /// <summary>« agent_model » : vide = choix automatique (voir <see cref="ChooseModel"/>).</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>« ollama_model » : le modèle du chat, dernier recours du choix automatique.</summary>
    public string FallbackModel { get; init; } = "qwen2.5-coder:7b";

    /// <summary>Fenêtre de contexte demandée. Ollama garde sinon ~4096, qui tronque silencieusement la consigne dès qu'un fichier est lu.</summary>
    public int NumCtx { get; init; } = 16384;

    public int MaxSteps { get; init; } = 30;
    public int MaxMinutes { get; init; } = 15;

    /// <summary>
    /// Sortie contrainte par défaut : sur le banc, elle bat les appels d'outils natifs pour chaque modèle mesuré (le modèle ne peut
    /// plus « raconter » au lieu d'appeler l'outil) et marche même avec un modèle sans la capacité « tools ».
    /// </summary>
    public AgentToolMode ToolMode { get; init; } = AgentToolMode.Structured;

    /// <summary>Phrase de raisonnement avant chaque appel d'outil (mode structuré). Désactivé : sur le banc, plus de jetons et pas de gain net.</summary>
    public bool Thought { get; init; }

    /// <summary>Commande de vérification imposée (ex. « dotnet build MonProjet.csproj »). Vide = l'agent la déduit du projet.</summary>
    public string? VerifyCommand { get; init; }

    public bool UseV2 => string.Equals(Engine, EngineV2, StringComparison.OrdinalIgnoreCase);

    public static AgentV2Settings Load(SettingsEngine? engine = null)
    {
        var s = engine ?? SettingsEngine.Shared;
        var verify = s.GetString("agent_verify_command", string.Empty).Trim();

        return new AgentV2Settings
        {
            Engine = s.GetString("agent_engine", EngineV2).Trim().ToLowerInvariant() is EngineV1 ? EngineV1 : EngineV2,
            Endpoint = Moto.Core.AI.Llm.OllamaChatClient.NormalizeEndpoint(s.GetString("ollama_endpoint", string.Empty)),
            Model = s.GetString("agent_model", string.Empty).Trim(),
            FallbackModel = s.GetString("ollama_model", "qwen2.5-coder:7b").Trim(),
            NumCtx = Math.Clamp(s.GetInt("agent_num_ctx", 16384), 2048, 131072),
            MaxSteps = Math.Clamp(s.GetInt("agent_max_steps", 30), 3, 100),
            MaxMinutes = Math.Clamp(s.GetInt("agent_max_minutes", 15), 1, 180),
            ToolMode = ParseToolMode(s.GetString("agent_tool_mode", "structured")),
            Thought = s.GetBool("agent_thought", false),
            VerifyCommand = verify.Length == 0 ? null : verify,
        };
    }

    public static AgentToolMode ParseToolMode(string? text) => (text ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "native" => AgentToolMode.Native,
        "auto" => AgentToolMode.Auto,
        _ => AgentToolMode.Structured,
    };

    /// <summary>Le modèle est-il dans la liste d'Ollama ? « qwen3 » sans étiquette désigne « qwen3:latest » (comme pour Ollama).</summary>
    public static bool IsInstalled(string model, IReadOnlyCollection<string> installed)
    {
        if (string.IsNullOrWhiteSpace(model)) return false;

        var name = model.Trim();
        return installed.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            || (!name.Contains(':') && installed.Any(n => string.Equals(n, name + ":latest", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Le modèle à utiliser : celui du réglage s'il est renseigné ; sinon le premier de <see cref="PreferredModels"/> qui est installé ;
    /// sinon le modèle du chat local.
    /// </summary>
    public static string ChooseModel(string configured, string fallback, IReadOnlyCollection<string> installed)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();

        foreach (var preferred in PreferredModels)
        {
            var match = installed.FirstOrDefault(n => string.Equals(n, preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return string.IsNullOrWhiteSpace(fallback) ? PreferredModels[^1] : fallback.Trim();
    }
}
