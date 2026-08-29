using System.Diagnostics;
using System.Text.Json;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Benchmark étendu des optimisations IA.
/// Mesure tokens/s, RAM, latence par tier avec export JSON/CSV.
/// </summary>
public partial class AiOptimizationsBenchmark
{
    private readonly EmbeddedLlmEngine _engine;
    private readonly SettingsEngine _settings;

    public AiOptimizationsBenchmark(
        EmbeddedLlmEngine engine,
        SettingsEngine settings)
    {
        _engine = engine;
        _settings = settings;
    }

    /// <summary>
    /// Lance un benchmark complet pour tous les tiers.
    /// </summary>
    public async Task<BenchmarkSuiteResult> RunFullBenchmarkAsync(
        CancellationToken ct = default)
    {
        var results = new List<BenchmarkResult>();
        var tiers = new[] { "lite", "standard", "full" };

        foreach (var tier in tiers)
        {
            var result = await BenchmarkTierAsync(tier, ct);
            results.Add(result);
        }

        return new BenchmarkSuiteResult
        {
            Timestamp = DateTime.UtcNow,
            Results = results,
            Comparison = GenerateComparisonTable(results)
        };
    }

    /// <summary>
    /// Benchmark un tier spécifique.
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkTierAsync(
        string tier,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var initialRam = GetCurrentRamUsageMb();

        // Charge le modèle pour ce tier
        var modelPath = GetModelPathForTier(tier);
        await _engine.LoadModelAsync(modelPath, ct);

        // Exécute une inférence de test
        var prompt = "Explique le pattern Singleton en C#.";
        var maxTokens = 100;
        var response = await _engine.InferAsync(prompt, maxTokens, ct);

        sw.Stop();
        var finalRam = GetCurrentRamUsageMb();

        // Décharge le modèle
        _engine.UnloadModel();

        return new BenchmarkResult
        {
            Tier = tier,
            TokensPerSecond = maxTokens / (sw.ElapsedMilliseconds / 1000.0),
            RamUsageMb = finalRam - initialRam,
            LatencyMs = sw.ElapsedMilliseconds,
            TokensGenerated = maxTokens,
            ModelPath = modelPath
        };
    }

    /// <summary>
    /// Exporte les résultats en JSON.
    /// </summary>
    public async Task ExportToJsonAsync(
        BenchmarkSuiteResult result,
        string outputPath,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputPath, json, ct);
    }

    /// <summary>
    /// Exporte les résultats en CSV.
    /// </summary>
    public async Task ExportToCsvAsync(
        BenchmarkSuiteResult result,
        string outputPath,
        CancellationToken ct = default)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Tier,Tokens/s,RAM (MB),Latency (ms),Tokens Generated");

        foreach (var r in result.Results)
        {
            csv.AppendLine($"{r.Tier},{r.TokensPerSecond:F2},{r.RamUsageMb},{r.LatencyMs},{r.TokensGenerated}");
        }

        await File.WriteAllTextAsync(outputPath, csv.ToString(), ct);
    }

    /// <summary>
    /// Génère un tableau de comparaison.
    /// </summary>
    private static string GenerateComparisonTable(List<BenchmarkResult> results)
    {
        var table = new System.Text.StringBuilder();
        table.AppendLine("Tier | Tokens/s | RAM (MB) | Latency (ms)");
        table.AppendLine("-----|----------|----------|-------------");

        foreach (var r in results)
        {
            table.AppendLine($"{r.Tier,-5} | {r.TokensPerSecond,8:F2} | {r.RamUsageMb,8} | {r.LatencyMs,10}");
        }

        return table.ToString();
    }

    private static long GetCurrentRamUsageMb()
    {
        var process = Process.GetCurrentProcess();
        return process.WorkingSet64 / 1024 / 1024;
    }

    private static string GetModelPathForTier(string tier)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "Models");

        return Path.Combine(baseDir, $"model-{tier}.onnx");
    }
}

/// <summary>
/// Résultat d'un benchmark pour un tier.
/// </summary>
public class BenchmarkResult
{
    public string Tier { get; set; } = "";
    public double TokensPerSecond { get; set; }
    public long RamUsageMb { get; set; }
    public long LatencyMs { get; set; }
    public int TokensGenerated { get; set; }
    public string ModelPath { get; set; } = "";
}

/// <summary>
/// Résultat d'une suite de benchmarks.
/// </summary>
public class BenchmarkSuiteResult
{
    public DateTime Timestamp { get; set; }
    public List<BenchmarkResult> Results { get; set; } = new();
    public string Comparison { get; set; } = "";
}
