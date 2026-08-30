// Moto.Core/AI/Internal/MotoAiKernel.cs
// Routeur central des requêtes IA : Ollama en priorité, réponse de repli sinon.
// Le routage vers un moteur embarqué (EmbeddedModelRouter/DualModelIntegration)
// est mis de côté pour cette passe (voir Moto.Core.csproj) ; RouteAsync reste
// l'unique point d'entrée utilisé par les agents (LlmBackedAgents, etc.).
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal;

public partial class MotoAiKernel
{
    private readonly OllamaClient _ollama;
    private readonly string _workspace;

    public MotoAiKernel(OllamaClient? ollama = null)
        : this(string.Empty, ollama)
    {
    }

    /// <summary>Surcharge liée à un workspace (utilisée par MotoAiService côté éditeur).</summary>
    public MotoAiKernel(string workspace, OllamaClient? ollama = null)
    {
        _workspace = workspace ?? string.Empty;
        _ollama = ollama ?? new OllamaClient();
    }

    /// <summary>
    /// Route une requête vers le meilleur provider disponible.
    /// Ordre : Ollama → réponse de repli.
    /// </summary>
    public async Task<AiResponse?> RouteAsync(
        string prompt,
        int maxTokens = 256,
        CancellationToken ct = default)
    {
        var ollamaResult = await TryOllamaAsync(prompt, maxTokens, ct);
        if (ollamaResult is not null) return ollamaResult;

        return await FallbackAsync(prompt, maxTokens, ct);
    }

    /// <summary>Variante texte simple, utilisée par les agents spécialisés.</summary>
    public async Task<string> RouteAsync(string prompt, CancellationToken ct = default)
    {
        var result = await RouteAsync(prompt, 256, ct);
        return result?.Content ?? string.Empty;
    }

    private async Task<AiResponse?> TryOllamaAsync(string prompt, int maxTokens, CancellationToken ct)
    {
        try
        {
            if (!await _ollama.IsAvailableAsync(ct)) return null;

            var content = await _ollama.GenerateAsync(prompt, ct);
            return new AiResponse
            {
                Success = true,
                Content = content,
                Provider = "ollama",
            };
        }
        catch
        {
            return null;
        }
    }

    private Task<AiResponse?> FallbackAsync(string prompt, int maxTokens, CancellationToken ct)
    {
        return Task.FromResult<AiResponse?>(new AiResponse
        {
            Success = false,
            Content = string.Empty,
            Provider = "none",
            Summary = "Aucun moteur IA disponible (Ollama injoignable).",
        });
    }
}
