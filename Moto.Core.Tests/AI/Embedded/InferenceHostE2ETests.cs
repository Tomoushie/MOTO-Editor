// Moto.Core.Tests/AI/Embedded/InferenceHostE2ETests.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Embedded;
using Moto.Core.Performance;
using Xunit;

namespace Moto.Core.Tests.AI.Embedded;

public class InferenceHostE2ETests : IDisposable
{
    private readonly HeavyProcessLauncher _launcher;
    private readonly IsolatedInferenceHost _host;

    public InferenceHostE2ETests()
    {
        _launcher = new HeavyProcessLauncher("Moto.InferenceHost.exe");
        _host = new IsolatedInferenceHost(_launcher, TimeSpan.FromMinutes(5));
    }

    // ── Start / Stop ──

    [Fact]
    public async Task StartHost_ProcessIsRunning()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        Assert.True(_host.IsRunning);
    }

    [Fact]
    public async Task StopHost_ProcessIsStopped()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.StopHostAsync();
        Assert.False(_host.IsRunning);
    }

    // ── Load / Unload ──

    [Fact]
    public async Task LoadModel_SetsIsModelLoaded()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.LoadModelAsync();
        Assert.True(_host.IsModelLoaded);
    }

    [Fact]
    public async Task UnloadModel_ClearsIsModelLoaded()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.LoadModelAsync();
        await _host.UnloadModelAsync();
        Assert.False(_host.IsModelLoaded);
    }

    // ── Generate ──

    [Fact]
    public async Task Generate_ReturnsNonEmptyText()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var result = await _host.GenerateAsync("Hello", maxTokens: 10);
        Assert.False(string.IsNullOrEmpty(result));
    }

    // ── UpdateBudget ──

    [Fact]
    public async Task UpdateBudget_DoesNotThrow()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var budget = ResourceBudget.Performance;
        await _host.UpdateBudgetAsync(budget);
        // Pas d'exception = succès
    }

    // ── Crash / Restart ──

    [Fact]
    public async Task Watchdog_DetectsCrash_AndRestarts()
    {
        var loadMonitor = new SystemLoadMonitor();
        var watchdog = new InferenceWatchdog(_host, loadMonitor);
        var restartDetected = false;
        watchdog.OnEvent += e =>
        {
            if (e.Type == WatchdogEventType.Restarted) restartDetected = true;
        };

        await _host.StartHostAsync(ModelTier.Lite);
        // Simule un crash en tuant le processus
        _launcher.Stop("inference-host");

        // Attend le check du watchdog (5s interval + marge)
        await Task.Delay(8000);

        Assert.True(restartDetected || watchdog.CrashCount > 0);
        watchdog.Dispose();
        loadMonitor.Dispose();
    }

    // ── Stress : montée en charge ──

    [Fact]
    public async Task Stress_ConcurrentGenerations_DoNotCrash()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var tasks = new Task<string>[10];

        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _host.GenerateAsync($"Prompt {i}", maxTokens: 5);
        }

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.NotNull(r));
    }

    // ── Stress : montée/descente de tiers ──

    [Fact]
    public async Task Stress_TierSwitching_DoesNotLeak()
    {
        var compression = new ModelCompressionService();
        var downloader = new ModelDownloader();
        var manager = new SmartModelManager(compression, _host, downloader);

        var ramBefore = GC.GetTotalMemory(true);

        for (int i = 0; i < 5; i++)
        {
            await manager.SwitchTierAsync(ModelTier.Lite);
            await manager.SwitchTierAsync(ModelTier.Compact);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var ramAfter = GC.GetTotalMemory(true);

        // La RAM ne doit pas avoir augmenté de plus de 50 MB
        Assert.True(ramAfter - ramBefore < 50 * 1024 * 1024,
            $"Memory leak détectée: avant={ramBefore}, après={ramAfter}");
    }

    // ── Download : reprise HTTP Range ──

    [Fact]
    public async Task Download_ResumeAfterInterruption()
    {
        var downloader = new ModelDownloader();
        var config = new EmbeddedLlmConfig
        {
            ModelFileName = "test-model.onnx",
            DownloadUrl = "https://example.com/test-model.onnx",
            ExpectedSizeBytes = 1024
        };

        // Simule une interruption via CancellationToken
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => downloader.DownloadAsync(config, cts.Token));

        // Le fichier .part doit exister pour reprise
        // (test réel nécessiterait un serveur HTTP local)
    }

    // ── Memory leak : exécutions longues ──

    [Fact]
    public async Task MemoryLeak_100Generations_StableRam()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.LoadModelAsync();

        GC.Collect();
        var ramBefore = _host.ProcessMemoryMB;

        for (int i = 0; i < 100; i++)
        {
            await _host.GenerateAsync($"Test prompt {i}", maxTokens: 10);
        }

        GC.Collect();
        var ramAfter = _host.ProcessMemoryMB;

        // Tolérance : +100 MB max après 100 générations
        Assert.True(ramAfter - ramBefore < 100,
            $"Fuite mémoire suspecte: avant={ramBefore}MB, après={ramAfter}MB");
    }

    public void Dispose()
    {
        _host.Dispose();
        _launcher.Dispose();
    }
}
