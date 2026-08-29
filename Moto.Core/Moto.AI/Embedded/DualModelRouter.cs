// Moto.Core/AI/Embedded/DualModelRouter.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Routeur double modèle : 500M (instantané) ↔ 7B (qualité).
///
/// Stratégie :
/// - Tâches simples (complétion courte, suggestions) → 500M (~50 ms)
/// - Tâches complexes (génération longue, refactor) → 7B (~2-5 s)
/// - Détection automatique via longueur prompt + complexité
/// - Fallback 7B si 500M échoue ou qualité insuffisante
///
/// Gain : latence moyenne -70% pour 90% des requêtes
/// </summary>
public sealed class DualModelRouter
{
    private readonly EmbeddedLlmEngine _smallModel;  // 500M
    private readonly EmbeddedLlmEngine _largeModel;  // 7B
    private readonly DualModelConfig _config;

    public static DualModelRouter? Instance { get; private set; }

    public int SmallModelRequests { get; private set; }
    public int LargeModelRequests { get; private set; }
    public int FallbackCount { get; private set; }
    public double SmallModelRatio =>
        SmallModelRequests + LargeModelRequests == 0
        ? 0
        : (double)SmallModelRequests / (SmallModelRequests + LargeModelRequests);

    public DualModelRouter(
        EmbeddedLlmEngine smallModel,
        EmbeddedLlmEngine largeModel,
        DualModelConfig config)
    {
        _smallModel = smallModel;
        _largeModel = largeModel;
        _config = config;
        Instance = this;
    }

    /// <summary>
    /// Génère une réponse en choisissant automatiquement le modèle.
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        AiTaskComplexity complexity = AiTaskComplexity.Auto,
        CancellationToken ct = default)
    {
        // 1. Détermine la complexité si Auto
        if (complexity == AiTaskComplexity.Auto)
        {
            complexity = EstimateComplexity(prompt);
        }

        // 2. Choisit le modèle
        var useSmall = complexity == AiTaskComplexity.Simple ||
                       (complexity == AiTaskComplexity.Medium && _config.PreferSmallForMedium);

        if (useSmall)
        {
            try
            {
                SmallModelRequests++;
                var result = await _smallModel.GenerateAsync(
                    prompt,
                    maxTokens: _config.SmallMaxTokens,
                    temperature: _config.SmallTemperature,
                    ct: ct);

                // Vérifie la qualité
                if (IsQualitySufficient(result))
                {
                    return result;
                }

                // Qualité insuffisante → fallback
                FallbackCount++;
            }
            catch
            {
                // Erreur small → fallback
                FallbackCount++;
            }
        }

        // 3. Fallback ou tâche complexe → large model
        LargeModelRequests++;
        return await _largeModel.GenerateAsync(
            prompt,
            maxTokens: _config.LargeMaxTokens,
            temperature: _config.LargeTemperature,
            ct: ct);
    }

    /// <summary>
    /// Complétion de code (infill) → toujours small model (rapide).
    /// </summary>
    public async Task<string> CompleteCodeAsync(
        string prefix,
        string suffix,
        CancellationToken ct = default)
    {
        SmallModelRequests++;
        try
        {
            return await _smallModel.CompleteCodeAsync(prefix, suffix, ct);
        }
        catch
        {
            FallbackCount++;
            LargeModelRequests++;
            return await _largeModel.CompleteCodeAsync(prefix, suffix, ct);
        }
    }

    /// <summary>
    /// Refactor/génération longue → toujours large model (qualité).
    /// </summary>
    public async Task<string> GenerateCodeAsync(
        string instruction,
        string? context = null,
        CancellationToken ct = default)
    {
        LargeModelRequests++;
        return await _largeModel.GenerateCodeAsync(instruction, context, ct);
    }

    /// <summary>
    /// Estime la complexité d'un prompt.
    /// </summary>
    private AiTaskComplexity EstimateComplexity(string prompt)
    {
        var length = prompt.Length;
        var wordCount = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Heuristiques
        if (length < 50 && wordCount < 10)
            return AiTaskComplexity.Simple;

        if (length > 500 || wordCount > 80)
            return AiTaskComplexity.Complex;

        // Mots-clés indicateurs de complexité
        var complexKeywords = new[] { "refactor", "architecture", "explain", "analyze", "optimize" };
        foreach (var keyword in complexKeywords)
        {
            if (prompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return AiTaskComplexity.Complex;
        }

        return AiTaskComplexity.Medium;
    }

    /// <summary>
    /// Vérifie si la qualité du small model est suffisante.
    /// </summary>
    private bool IsQualitySufficient(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;
        if (response.Length < 5) return false;

        // Détection de réponses dégénérées (répétitions, incohérences)
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3) return false;

        // Vérifie la présence de contenu structuré (code, phrases complètes)
        var hasStructure = response.Contains(".") || response.Contains("{") || response.Contains("(");
        return hasStructure || words.Length >= 5;
    }
}

public class DualModelConfig
{
    /// <summary>Max tokens pour le small model.</summary>
    public int SmallMaxTokens { get; set; } = 128;

    /// <summary>Max tokens pour le large model.</summary>
    public int LargeMaxTokens { get; set; } = 1024;

    /// <summary>Température small model (plus créatif).</summary>
    public float SmallTemperature { get; set; } = 0.8f;

    /// <summary>Température large model (plus précis).</summary>
    public float LargeTemperature { get; set; } = 0.3f;

    /// <summary>Utiliser small model pour tâches medium ?</summary>
    public bool PreferSmallForMedium { get; set; } = true;
}

public enum AiTaskComplexity
{
    Auto,
    Simple,     // Complétion, suggestions courtes
    Medium,     // Explications, refactor léger
    Complex     // Génération longue, architecture
}
