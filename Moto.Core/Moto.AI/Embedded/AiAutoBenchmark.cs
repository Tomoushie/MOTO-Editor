// Moto.Core/AI/Embedded/AiAutoBenchmark.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Auto-benchmark : mesure les performances de chaque tier
/// et choisit automatiquement le meilleur.
/// </summary>
public sealed class AiAutoBenchmark
{
    private readonly SmartModelManager _modelManager;
    private readonly SystemLoadMonitor _loadMonitor;
    private readonly IsolatedInferenceHost _host;
    private readonly Dictionary<ModelTier, BenchmarkResult> _results = new();

    public static AiAutoBenchmark? Instance { get; private set; }
    public IReadOnlyDictionary<ModelTier, BenchmarkResult> Results => _results;
    public ModelTier? RecommendedTier { get; private set; }

    public event Action<BenchmarkProgress>? ProgressChanged;
    public event Action<ModelTier>? BenchmarkCompleted;

    public AiAutoBenchmark(
        SmartModelManager modelManager,
        SystemLoadMonitor loadMonitor,
        IsolatedInferenceHost host)
    {
        _modelManager = modelManager;
        _loadMonitor = loadMonitor;
        _host = host;
        Instance = this;
    }

    /// <summary>
    /// Lance le benchmark complet sur tous les tiers.
    /// </summary>
    public async Task RunFullBenchmarkAsync(CancellationToken ct = default)
    {
        _results.Clear();

        var tiers = new[] { ModelTier.Lite, ModelTier.Compact, ModelTier.Balanced, ModelTier.Full };

        for (int i = 0; i < tiers.Length; i++)
        {
            var tier = tiers[i];
            ProgressChanged?.Invoke(new BenchmarkProgress
            {
                CurrentTier = tier,
                Step = i + 1,
                TotalSteps = tiers.Length,
                Status = $"Benchmarking {tier}..."
            });

            var result = await BenchmarkTierAsync(tier, ct);
            _results[tier] = result;
        }

        // Choisit le meilleur tier
        RecommendedTier = SelectBestTier();
        BenchmarkCompleted?.Invoke(RecommendedTier.Value);
    }

    /// <summary>
    /// Benchmark un tier spécifique.
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkTierAsync(ModelTier tier, CancellationToken ct = default)
    {
        var result = new BenchmarkResult { Tier = tier };

        // 1. Mesure RAM
        var ramBefore = _loadMonitor.InferenceHostRamMB;

        // 2. Change de tier
        await _modelManager.SwitchTierAsync(tier, ct);
        await Task.Delay(1000, ct); // Laisse le temps au modèle de charger

        var ramAfter = _loadMonitor.InferenceHostRamMB;
        result.RamUsedMB = ramAfter - ramBefore;

        // 3. Mesure tokens/s + latence
        var sw = Stopwatch.StartNew();
        var prompt = "Write a simple hello world function in C#.";
        var response = await _host.GenerateAsync(prompt, 100, ct);
        sw.Stop();

        var tokens = EstimateTokens(response);
        result.TokensPerSecond = tokens / sw.Elapsed.TotalSeconds;
        result.LatencyMs = sw.ElapsedMilliseconds;

        // 4. Mesure CPU
        result.CpuPercent = _loadMonitor.SystemCpuPercent;

        // 5. Score de qualité (heuristique : longueur + cohérence)
        result.QualityScore = ComputeQualityScore(response);

        // 6. Score global
        result.OverallScore = ComputeOverallScore(result);

        return result;
    }

    /// <summary>
    /// Sélectionne le meilleur tier selon les résultats.
    /// </summary>
    private ModelTier SelectBestTier()
    {
        if (_results.Count == 0) return ModelTier.Balanced;

        // Priorité : qualité > vitesse > RAM
        return _results.Values
            .OrderByDescending(r => r.OverallScore)
            .First()
            .Tier;
    }

    private static int EstimateTokens(string text)
    {
        // Heuristique : ~4 caractères par token
        return text.Length / 4;
    }

    private static double ComputeQualityScore(string response)
    {
        // Heuristique simple : longueur + présence de mots-clés
        var score = 0.0;

        if (response.Length > 50) score += 0.3;
        if (response.Contains("void") || response.Contains("Main")) score += 0.3;
        if (response.Contains("{") && response.Contains("}")) score += 0.2;
        if (response.Contains("Console")) score += 0.2;

        return Math.Clamp(score, 0, 1);
    }

    private static double ComputeOverallScore(BenchmarkResult result)
    {
        // Pondération : qualité 40%, vitesse 30%, RAM 20%, CPU 10%
        var qualityNorm = result.QualityScore;
        var speedNorm = Math.Min(result.TokensPerSecond / 50.0, 1.0); // 50 t/s = max
        var ramNorm = 1.0 - Math.Min(result.RamUsedMB / 4096.0, 1.0); // Moins = mieux
        var cpuNorm = 1.0 - Math.Min(result.CpuPercent / 100.0, 1.0);

        return qualityNorm * 0.4 + speedNorm * 0.3 + ramNorm * 0.2 + cpuNorm * 0.1;
    }
}

public class BenchmarkResult
{
    public ModelTier Tier { get; set; }
    public double TokensPerSecond { get; set; }
    public long LatencyMs { get; set; }
    public long RamUsedMB { get; set; }
    public double CpuPercent { get; set; }
    public double QualityScore { get; set; }
    public double OverallScore { get; set; }
}

public class BenchmarkProgress
{
    public ModelTier CurrentTier { get; set; }
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public string Status { get; set; } = "";
}
