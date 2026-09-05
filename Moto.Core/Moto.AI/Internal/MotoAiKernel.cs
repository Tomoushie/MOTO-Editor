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

    // ★ CORRECTIF (05/09, sur demande de Tom) : appel Ollama DIRECT, indépendant
    // de _localAi/AiProviderManager (chantier en cours ailleurs, pas terminé —
    // voir commentaire de TryOllamaAsync). _localAi reste tel quel pour ce
    // chantier ; ce champ-ci restaure le seul chemin déjà éprouvé (bonjour.txt,
    // "del test.txt", jalons 1-3) sans rien retirer de l'autre.
    private readonly OllamaClient _ollamaDirect = new();

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
            // ★ CORRECTIF (05/09, revu le même jour sur demande de Tom) : la
            // 1re version de ce correctif rebranchait sur _localAi
            // (LocalModelService → AiProviderManager) pour recompiler après le
            // renommage _ollama→_localAi trouvé cassé (CS0103) — mais ce chemin
            // s'est révélé être un SQUELETTE qui échoue toujours instantanément
            // (moteur interne jamais implémenté + aucun fournisseur jamais
            // configuré sur CETTE instance précise d'AiProviderManager) :
            // /agent, /refactor, /test, /doc ne produisaient plus RIEN, sans
            // erreur visible. _ollamaDirect (ci-dessus) restaure le seul chemin
            // déjà éprouvé en test réel (bonjour.txt, "del test.txt", jalons
            // 1-3) — _localAi reste intact pour le chantier en cours ailleurs,
            // simplement plus utilisé par CET appel précis tant qu'il n'est pas
            // terminé.
            if (!await _ollamaDirect.IsAvailableAsync(ct)) return null;

            var content = await _ollamaDirect.GenerateAsync(prompt, ct, system);
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
