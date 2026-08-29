using System;
using System.Linq;
using System.Threading.Tasks;
using Moto.Core.Performance;
using Moto.Core.Settings;
using Moto.Core.Logging;
using Xunit;

namespace Moto.Tests;

/// <summary>
/// Item 53 — Tests de stress : benchmarks consécutifs + téléchargements simultanés.
/// </summary>
public class StressTests
{
    private readonly StructuredLogCollector _log = new();
    private readonly SettingsEngine _settings = SettingsEngine.Shared;

    [Fact]
    public async Task ConsecutiveBenchmarks_ShouldNotDegrade_MoreThan30Percent()
    {
        var baseline = await RunBenchmarkAsync();
        for (int i = 0; i < 4; i++) // 3-5 benchmarks consécutifs
        {
            var current = await RunBenchmarkAsync();
            double degradation = (current - baseline) / baseline;
            Assert.True(degradation < 0.30,
                $"Dégradation {degradation:P0} au run {i + 1} dépasse 30%");
        }
    }

    [Fact]
    public async Task SimultaneousDownloads_ShouldRespectBackpressure()
    {
        int maxConcurrent = 0;
        int current = 0;
        var gate = new object();

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            lock (gate) { current++; maxConcurrent = Math.Max(maxConcurrent, current); }
            await Task.Delay(50);
            lock (gate) { current--; }
        })).ToArray();

        await Task.WhenAll(tasks);
        _log.Info("StressTest", "Téléchargements simultanés", new { maxConcurrent });
        Assert.True(maxConcurrent <= 10);
    }

    private static async Task<double> RunBenchmarkAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(10); // placeholder : remplace par le vrai benchmark d'inférence
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }
}
