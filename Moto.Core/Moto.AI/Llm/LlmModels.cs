// Moto.Core/Moto.AI/Llm/LlmModels.cs
// ★ AJOUT (24/09, "écriture générative et agentique fonctionnelle") : types d'échange
// avec un modèle de CHAT (Ollama /api/chat) — messages, outils, options, réponse.
// Remplace, pour les nouveaux chemins, le simple texte-en-texte-out de
// Internal/OllamaClient (/api/generate, sans flux, sans outils, sans options).
// Aucune dépendance MAUI ni interface : testable en ligne de commande.
using System.Text.Json.Nodes;

namespace Moto.Core.AI.Llm;

/// <summary>Un appel d'outil demandé par le modèle (arguments déjà décodés en objet JSON).</summary>
public sealed record LlmToolCall(string Name, JsonObject Arguments);

/// <summary>Un message d'une conversation. Rôles : system, user, assistant, tool.</summary>
public sealed class LlmMessage
{
    public string Role { get; init; } = "user";
    public string Content { get; set; } = string.Empty;

    /// <summary>Appels d'outils émis par le modèle (rôle assistant uniquement).</summary>
    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }

    /// <summary>Rôle tool : nom de l'outil dont ce message est le résultat.</summary>
    public string? ToolName { get; init; }

    public static LlmMessage System(string content) => new() { Role = "system", Content = content };
    public static LlmMessage User(string content) => new() { Role = "user", Content = content };
    public static LlmMessage Assistant(string content, IReadOnlyList<LlmToolCall>? calls = null)
        => new() { Role = "assistant", Content = content, ToolCalls = calls };
    public static LlmMessage Tool(string toolName, string content)
        => new() { Role = "tool", Content = content, ToolName = toolName };
}

/// <summary>Description d'un outil proposé au modèle (schéma JSON des paramètres).</summary>
public sealed record LlmToolSpec(string Name, string Description, JsonObject Parameters);

/// <summary>Réglages d'un appel. Les valeurs par défaut sont celles mesurées utiles pour du code.</summary>
public sealed class LlmOptions
{
    /// <summary>Fenêtre de contexte demandée. Sans elle Ollama garde son défaut (souvent 4096) et
    /// TRONQUE silencieusement le début de la conversation — donc la consigne système.</summary>
    public int NumCtx { get; init; } = 16384;

    /// <summary>Basse par défaut : du code et des appels d'outils doivent être reproductibles.</summary>
    public double Temperature { get; init; } = 0.2;

    /// <summary>Plafond de jetons générés (null = pas de plafond).</summary>
    public int? NumPredict { get; init; }

    /// <summary>Durée pendant laquelle Ollama garde le modèle en VRAM après l'appel (évite un rechargement de ~13 s).</summary>
    public string KeepAlive { get; init; } = "30m";

    /// <summary>null = ne rien envoyer ; "false"/"true" ; ou "low"/"medium"/"high" (gpt-oss).
    /// Ignoré si le modèle n'a pas la capacité "thinking".</summary>
    public string? Think { get; init; } = "false";

    /// <summary>Mode structuré seulement : le modèle écrit d'abord une phrase de raisonnement (« pensee ») avant l'appel d'outil.</summary>
    public bool StructuredThought { get; init; }

    /// <summary>Copie avec une autre température (pour retenter un appel dont le modèle s'est emballé).</summary>
    public LlmOptions WithTemperature(double temperature) => new()
    {
        NumCtx = NumCtx, Temperature = temperature, NumPredict = NumPredict, KeepAlive = KeepAlive, Think = Think,
        StructuredThought = StructuredThought,
    };
}

/// <summary>Réponse complète d'un appel de chat, avec les mesures de vitesse.</summary>
public sealed class LlmReply
{
    public string Content { get; init; } = string.Empty;
    public string Thinking { get; init; } = string.Empty;
    public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = Array.Empty<LlmToolCall>();
    public string DoneReason { get; init; } = string.Empty;

    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }

    /// <summary>Durée totale côté serveur (chargement compris).</summary>
    public double TotalSeconds { get; init; }

    /// <summary>Part de chargement du modèle dans la durée totale (0 si déjà en VRAM).</summary>
    public double LoadSeconds { get; init; }

    /// <summary>Jetons générés par seconde (mesure du serveur).</summary>
    public double TokensPerSecond { get; init; }

    /// <summary>Délai avant le premier morceau reçu (mesure côté client).</summary>
    public double FirstChunkSeconds { get; init; }
}

/// <summary>Ce que le serveur dit d'un modèle installé.</summary>
public sealed record LlmModelInfo(
    string Name,
    string ParameterSize,
    string Quantization,
    int ContextLength,
    IReadOnlyList<string> Capabilities)
{
    public bool SupportsTools => Has("tools");
    public bool SupportsThinking => Has("thinking");
    public bool SupportsInsert => Has("insert");

    private bool Has(string capability)
        => Capabilities.Any(c => string.Equals(c, capability, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Erreur de dialogue avec le serveur de modèles (message déjà lisible par l'utilisateur).</summary>
public sealed class LlmException : Exception
{
    public LlmException(string message, Exception? inner = null) : base(message, inner) { }

    /// <summary>Vrai si le serveur a refusé les outils pour ce modèle.</summary>
    public bool ToolsNotSupported { get; init; }

    /// <summary>Vrai si le délai est dépassé (modèle bloqué ou machine saturée).</summary>
    public bool TimedOut { get; init; }

    /// <summary>Vrai si le serveur a interrompu la génération parce que le modèle s'est emballé (« token repeat limit reached »).
    /// Un nouvel essai, à température un peu plus haute, réussit presque toujours.</summary>
    public bool GenerationGlitch { get; init; }
}
