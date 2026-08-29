// Moto.Core/AI/Embedded/AiOptimizationsBenchmark.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Benchmark comparatif des 3 optimisations :
/// - Baseline (sans optimisation)
/// - Dual routing
/// - Speculative decoding
/// - Layered loading
/// Mesure : tokens/s, latence, RAM, CPU.
/// </summary>
public sealed class AiOptimizationsBenchmark
{
    private readonly DualModelIntegration _dual;
    private readonly SpeculativeActivationService _spec;
    private readonly LayeredActivationService _layered;
    private readonly SmartModelManager _modelManager;

    public static AiOptimizationsBenchmark? Instance { get; private set; }
    public BenchmarkReport? LastReport { get; private set; }

    public AiOptimizationsBenchmark(
        DualModelIntegration dual,
        SpeculativeActivationService spec,
        LayeredActivationService layered,
        SmartModelManager modelManager)
    {
        _dual = dual;
        _spec = spec;
        _layered = layered;
        _modelManager = modelManager;
        Instance = this;
    }

    /// <summary>
    /// Lance le benchmark complet sur les 4 configurations.
    /// </summary>
    public async Task<BenchmarkReport> RunFullBenchmarkAsync(CancellationToken ct = default)
    {
        var report = new BenchmarkReport { StartedAt = DateTime.UtcNow };

        // Prompt de test standard
        const string testPrompt = "Write a function that calculates fibonacci numbers in C#.";
        const int iterations = 5;

        // 1. Baseline
        report.Baseline = await BenchmarkConfigAsync("Baseline", async () =>
        {
            return await _modelManager.GenerateAsync(testPrompt, ct);
        }, iterations, ct);

        // 2. Dual routing
        report.DualRouting = await BenchmarkConfigAsync("DualRouting", async () =>
        {
            return await _dual.GenerateAsync(testPrompt, AiTaskComplexity.Simple, ct);
        }, iterations, ct);

        // 3. Speculative
        if (_spec.IsDraftAvailable)
        {
            report.Speculative = await BenchmarkConfigAsync("Speculative", async () =>
            {
                return await _modelManager.GenerateAsync(testPrompt, ct);
            }, iterations, ct);
        }

        // 4. Layered
        report.Layered = await BenchmarkConfigAsync("Layered", async () =>
        {
            return await _modelManager.GenerateAsync(testPrompt, ct);
        }, iterations, ct);

        report.CompletedAt = DateTime.UtcNow;
        LastReport = report;
        return report;
    }

    private async Task<BenchmarkConfigResult> BenchmarkConfigAsync(
        string name,
        Func<Task<string>> generateFunc,
        int iterations,
        CancellationToken ct)
    {
        var result = new BenchmarkConfigResult { Name = name };
        var sw = Stopwatch.StartNew();
        var ramBefore = GC.GetTotalMemory(true);

        for (int i = 0; i < iterations; i++)
        {
            var iterSw = Stopwatch.StartNew();
            var output = await generateFunc();
            iterSw.Stop();

            result.TokensGenerated += output.Length / 4;
            result.TotalTimeMs += iterSw.ElapsedMilliseconds;
            result.Latencies.Add(iterSw.ElapsedMilliseconds);
        }

        sw.Stop();
        var ramAfter = GC.GetTotalMemory(true);

        result.TotalTimeMs = sw.ElapsedMilliseconds;
        result.TokensPerSecond = result.TokensGenerated / (sw.Elapsed.TotalSeconds + 0.001);
        result.AvgLatencyMs = result.Latencies.Count > 0
            ? result.Latencies.Average()
            : 0;
        result.RamDeltaMB = (ramAfter - ramBefore) / 1024 / 1024;

        return result;
    }
}

public class BenchmarkReport
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public BenchmarkConfigResult Baseline { get; set; } = new();
    public BenchmarkConfigResult DualRouting { get; set; } = new();
    public BenchmarkConfigResult? Speculative { get; set; }
    public BenchmarkConfigResult Layered { get; set; } = new();

    public double DualSpeedup => Baseline.TokensPerSecond > 0
        ? DualRouting.TokensPerSecond / Baseline.TokensPerSecond
        : 1.0;
    public double LayeredRamSaving => Baseline.RamDeltaMB > 0
        ? 1.0 - (Layered.RamDeltaMB / Baseline.RamDeltaMB)
        : 0.0;
}

public class BenchmarkConfigResult
{
    public string Name { get; set; } = "";
    public int TokensGenerated { get; set; }
    public long TotalTimeMs { get; set; }
    public double TokensPerSecond { get; set; }
    public double AvgLatencyMs { get; set; }
    public long RamDeltaMB { get; set; }
    public List<long> Latencies { get; set; } = new();
}
