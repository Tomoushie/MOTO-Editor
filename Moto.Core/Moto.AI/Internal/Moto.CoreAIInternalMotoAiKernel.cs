// ⚠️ NE PAS SUPPRIMER LES MÉTHODES EXISTANTES
// Ajout de DualModelIntegration comme point d'entrée unique

namespace Moto.Core.AI.Internal;

public partial class MotoAiKernel
{
    private DualModelIntegration? _dualModelIntegration;

    /// <summary>
    /// Initialise DualModelIntegration comme point d'entrée unique.
    /// </summary>
    public void InitializeDualModel(DualModelIntegration integration)
    {
        _dualModelIntegration = integration;
    }

    /// <summary>
    /// Route une requête via DualModelIntegration (point d'entrée unique).
    /// </summary>
    public async Task<AiResponse?> RouteViaDualModelAsync(
        string prompt,
        int maxTokens = 256,
        CancellationToken ct = default)
    {
        if (_dualModelIntegration is null)
        {
            // Fallback vers l'ancien routage
            return await RouteAsync(prompt, maxTokens, ct);
        }

        return await _dualModelIntegration.CompleteAsync(prompt, maxTokens, ct);
    }
}
