// Moto.Core/AI/Embedded/DualModelIntegration.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Intègre le DualModelRouter dans le flux standard de MOTO AI.
/// Activé par défaut : 90% des requêtes passent par le small model (500M/1.5B).
/// </summary>
public sealed class DualModelIntegration
{
    private readonly DualModelRouter _router;
    private readonly SmartModelManager _modelManager;
    private bool _isEnabled;

    public static DualModelIntegration? Instance { get; private set; }
    public bool IsEnabled => _isEnabled;

    public DualModelIntegration(DualModelRouter router, SmartModelManager modelManager)
    {
        _router = router;
        _modelManager = modelManager;
        Instance = this;
    }

    /// <summary>
    /// Active le dual routing (appelé au démarrage si les 2 modèles sont disponibles).
    /// </summary>
    public async Task EnableAsync(CancellationToken ct = default)
    {
        if (_isEnabled) return;
        _isEnabled = true;
    }

    /// <summary>
    /// Point d'entrée unique pour toutes les générations IA.
    /// Remplace les appels directs à EmbeddedLlmEngine.
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        AiTaskComplexity complexity = AiTaskComplexity.Auto,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            // Fallback : modèle principal seul
            return await _modelManager.GenerateAsync(prompt, ct);
        }
        return await _router.GenerateAsync(prompt, complexity, ct);
    }

    /// <summary>
    /// Statistiques d'utilisation.
    /// </summary>
    public DualModelStats GetStats() => new()
    {
        IsEnabled = _isEnabled,
        SmallModelRequests = _router.SmallModelRequests,
        LargeModelRequests = _router.LargeModelRequests,
        SmallModelRatio = _router.SmallModelRatio,
        FallbackCount = _router.FallbackCount
    };
}

public class DualModelStats
{
    public bool IsEnabled { get; set; }
    public int SmallModelRequests { get; set; }
    public int LargeModelRequests { get; set; }
    public double SmallModelRatio { get; set; }
    public int FallbackCount { get; set; }
}
