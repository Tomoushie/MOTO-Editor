// Ajouter dans Moto.Core/AI/Embedded/IsolatedInferenceHost.cs

private LayeredModelLoader? _layeredLoader;
private SpeculativeDecoder? _speculativeDecoder;
private DualModelRouter? _dualRouter;

/// <summary>
/// Active le layered loading (chargement par couches + MoE).
/// </summary>
public async Task EnableLayeredLoadingAsync(LayeredModelConfig config, CancellationToken ct = default)
{
    if (_process == null) throw new InvalidOperationException("Host non démarré");

    var request = new JsonRpcRequest
    {
        Method = "enableLayeredLoading",
        Params = new
        {
            totalLayers = config.TotalLayers,
            totalExperts = config.TotalExperts,
            maxLayersInMemory = config.MaxLayersInMemory
        }
    };

    await _launcher.SendRequestAsync<object>("inference-host", request);
    _layeredLoader = new LayeredModelLoader("model-path", config);
}

/// <summary>
/// Active le speculative decoding (draft 500M + verify 7B).
/// </summary>
public async Task EnableSpeculativeDecodingAsync(
    EmbeddedLlmEngine draftModel,
    EmbeddedLlmEngine targetModel,
    SpeculativeConfig config,
    CancellationToken ct = default)
{
    if (_process == null) throw new InvalidOperationException("Host non démarré");

    var request = new JsonRpcRequest
    {
        Method = "enableSpeculativeDecoding",
        Params = new
        {
            draftLookahead = config.DraftLookahead,
            acceptanceThreshold = config.AcceptanceThreshold
        }
    };

    await _launcher.SendRequestAsync<object>("inference-host", request);
    _speculativeDecoder = new SpeculativeDecoder(draftModel, targetModel, config);
}

/// <summary>
/// Active le dual model routing (500M ↔ 7B).
/// </summary>
public async Task EnableDualModelRoutingAsync(
    EmbeddedLlmEngine smallModel,
    EmbeddedLlmEngine largeModel,
    DualModelConfig config,
    CancellationToken ct = default)
{
    if (_process == null) throw new InvalidOperationException("Host non démarré");

    var request = new JsonRpcRequest
    {
        Method = "enableDualModelRouting",
        Params = new
        {
            preferSmallForMedium = config.PreferSmallForMedium
        }
    };

    await _launcher.SendRequestAsync<object>("inference-host", request);
    _dualRouter = new DualModelRouter(smallModel, largeModel, config);
}

/// <summary>
/// Génère via speculative decoding (si activé).
/// </summary>
public async Task<string> GenerateSpeculativeAsync(string prompt, int maxTokens = 256, CancellationToken ct = default)
{
    if (_speculativeDecoder == null)
        throw new InvalidOperationException("Speculative decoding non activé");

    return await _speculativeDecoder.GenerateAsync(prompt, maxTokens, ct);
}

/// <summary>
/// Génère via dual model routing (si activé).
/// </summary>
public async Task<string> GenerateDualAsync(string prompt, AiTaskComplexity complexity = AiTaskComplexity.Auto, CancellationToken ct = default)
{
    if (_dualRouter == null)
        throw new InvalidOperationException("Dual model routing non activé");

    return await _dualRouter.GenerateAsync(prompt, complexity, ct);
}

/// <summary>
/// Statistiques des optimisations avancées.
/// </summary>
public AdvancedOptimizationsStats GetAdvancedStats() => new()
{
    LayeredLoaderActive = _layeredLoader != null,
    LoadedLayers = _layeredLoader?.LoadedLayers ?? 0,
    TotalLayers = _layeredLoader?.TotalLayers ?? 0,
    ActiveMemoryMB = _layeredLoader?.ActiveMemoryMB ?? 0,

    SpeculativeActive = _speculativeDecoder != null,
    SpeculativeAcceptanceRate = _speculativeDecoder?.AcceptanceRate ?? 0,
    SpeculativeSpeedup = _speculativeDecoder?.SpeedupFactor ?? 1.0,

    DualRouterActive = _dualRouter != null,
    DualRouterSmallRatio = _dualRouter?.SmallModelRatio ?? 0,
    DualRouterFallbackCount = _dualRouter?.FallbackCount ?? 0
};

public class AdvancedOptimizationsStats
{
    // Layered loading
    public bool LayeredLoaderActive { get; set; }
    public int LoadedLayers { get; set; }
    public int TotalLayers { get; set; }
    public long ActiveMemoryMB { get; set; }

    // Speculative decoding
    public bool SpeculativeActive { get; set; }
    public double SpeculativeAcceptanceRate { get; set; }
    public double SpeculativeSpeedup { get; set; }

    // Dual model routing
    public bool DualRouterActive { get; set; }
    public double DualRouterSmallRatio { get; set; }
    public int DualRouterFallbackCount { get; set; }
}
