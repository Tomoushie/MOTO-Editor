// ⚠️ NE PAS SUPPRIMER LES MÉTHODES EXISTANTES
// Ajout du handler pour le moteur embarqué

namespace Moto.Core.AI.Internal;

public partial class MotoAiKernel
{
    private readonly EmbeddedModelRouter? _embeddedRouter;

    /// <summary>
    /// Constructeur étendu (injection du routeur embarqué).
    /// Les constructeurs existants restent fonctionnels.
    /// </summary>
    public MotoAiKernel(
        /* paramètres existants à conserver */
        EmbeddedModelRouter? embeddedRouter = null)
    {
        // ... initialisation existante ...
        _embeddedRouter = embeddedRouter;
    }

    /// <summary>
    /// Route une requête vers le meilleur provider disponible.
    /// Ordre : Ollama → Embarqué → Fallback.
    /// </summary>
    public async Task<AiResponse?> RouteAsync(
        string prompt,
        int maxTokens = 256,
        CancellationToken ct = default)
    {
        // 1. Tentative Ollama (existant)
        var ollamaResult = await TryOllamaAsync(prompt, maxTokens, ct);
        if (ollamaResult is not null) return ollamaResult;

        // 2. Tentative moteur embarqué (nouveau)
        if (_embeddedRouter is not null && await _embeddedRouter.CanHandleAsync(ct))
        {
            var embeddedResult = await _embeddedRouter.CompleteAsync(prompt, maxTokens, ct);
            if (embeddedResult is not null) return embeddedResult;
        }

        // 3. Fallback existant
        return await FallbackAsync(prompt, maxTokens, ct);
    }

    // Moto.Core/AI/Internal/MotoAiKernel.cs (à vérifier)
    public async Task<string> RouteAsync(string prompt, CancellationToken ct = default)
    {
        var result = await RouteAsync(prompt, 256, ct);
        return result ?? string.Empty;
    }

    // Les méthodes TryOllamaAsync et FallbackAsync existent déjà
    // et ne doivent PAS être modifiées.
}
