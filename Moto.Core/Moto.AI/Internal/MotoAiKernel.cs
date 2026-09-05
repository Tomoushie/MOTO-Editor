// Moto.Core/AI/Internal/MotoAiKernel.cs
// Routeur central des requêtes IA : Ollama en priorité, réponse de repli sinon.
// Le routage vers un moteur embarqué (EmbeddedModelRouter/DualModelIntegration)
// est mis de côté pour cette passe (voir Moto.Core.csproj) ; RouteAsync reste
// l'unique point d'entrée utilisé par les agents (LlmBackedAgents, etc.).
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal;

public partial class MotoAiKernel
{
    private readonly LocalModelService _localAi;
    private readonly string _workspace;

    public MotoAiKernel(LocalModelService? localAi = null)
        : this(string.Empty, localAi)
    {
    }

    /// <summary>Surcharge liée à un workspace (utilisée par MotoAiService côté éditeur).</summary>
    public MotoAiKernel(string workspace, LocalModelService? localAi = null)
    {
        _workspace = workspace ?? string.Empty;
        _localAi = localAi ?? new LocalModelService();
    }

    /// <summary>
    /// Route une requête vers le meilleur provider disponible.
    /// Ordre : Ollama → réponse de repli.
    /// </summary>
    public async Task<AiResponse?> RouteAsync(
        string prompt,
        int maxTokens = 256,
        CancellationToken ct = default,
        string? system = null)
    {
        var ollamaResult = await TryOllamaAsync(prompt, maxTokens, ct, system);
        if (ollamaResult is not null) return ollamaResult;

        return await FallbackAsync(prompt, maxTokens, ct);
    }

    /// <summary>Variante texte simple, utilisée par les agents spécialisés.</summary>
    public async Task<string> RouteAsync(string prompt, CancellationToken ct = default)
    {
        var result = await RouteAsync(prompt, 256, ct);
        return result?.Content ?? string.Empty;
    }

    private async Task<AiResponse?> TryOllamaAsync(string prompt, int maxTokens, CancellationToken ct, string? system = null)
    {
        try
        {
            // ★ CORRECTIF (05/09) : _localAi (LocalModelService) a remplacé
            // l'ancien _ollama (OllamaClient) — renommage fait dans le
            // constructeur mais jamais répercuté ici (CS0103, trouvé en
            // recompilant), chantier en cours non terminé, pas le mien. Pas de
            // IsAvailableAsync sur la nouvelle classe : elle gère déjà son
            // propre repli (AiProviderManager.CompleteWithFallbackAsync), donc
            // un Success=false ici joue exactement le même rôle qu'avant.
            var result = await _localAi.GenerateAsync(prompt, system, ct);
            if (!result.Success) return null;

            return new AiResponse
            {
                Success = true,
                Content = result.Content,
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
