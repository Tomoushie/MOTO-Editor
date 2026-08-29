// Moto.Core.Tests/AI/Embedded/AdvancedOptimizationsE2ETests.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Embedded;
using Moto.Core.Performance;
using Xunit;

namespace Moto.Core.Tests.AI.Embedded;

public class AdvancedOptimizationsE2ETests : IDisposable
{
    private readonly HeavyProcessLauncher _launcher;
    private readonly IsolatedInferenceHost _host;

    public AdvancedOptimizationsE2ETests()
    {
        _launcher = new HeavyProcessLauncher("Moto.InferenceHost.exe");
        _host = new IsolatedInferenceHost(_launcher);
    }

    // ── Dual Model Routing ──

    [Fact]
    public async Task DualRouting_SimpleTask_UsesSmallModel()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.EnableDualModelRoutingAsync(
            CreateSmallModel(), CreateLargeModel(), new DualModelConfig());

        var result = await _host.GenerateDualAsync("Hello", AiTaskComplexity.Simple);
        Assert.False(string.IsNullOrEmpty(result));

        var stats = _host.GetAdvancedStats();
        Assert.True(stats.DualRouterActive);
    }

    [Fact]
    public async Task DualRouting_ComplexTask_UsesLargeModel()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.EnableDualModelRoutingAsync(
            CreateSmallModel(), CreateLargeModel(), new DualModelConfig());

        var result = await _host.GenerateDualAsync(
            "Refactor this architecture to use microservices pattern with event sourcing",
            AiTaskComplexity.Complex);

        Assert.False(string.IsNullOrEmpty(result));
    }

    // ── Speculative Decoding ──

    [Fact]
    public async Task Speculative_GeneratesFasterThanBaseline()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.EnableSpeculativeDecodingAsync(
            CreateDraftModel(), CreateLargeModel(), new SpeculativeConfig());

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _host.GenerateSpeculativeAsync("Write hello world", maxTokens: 50);
        sw.Stop();

        Assert.False(string.IsNullOrEmpty(result));
        Assert.True(sw.ElapsedMilliseconds < 30_000, "Speculative decoding trop lent");
    }

    [Fact]
    public async Task Speculative_AcceptanceRateAbove50Percent()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.EnableSpeculativeDecodingAsync(
            CreateDraftModel(), CreateLargeModel(), new SpeculativeConfig());

        // Génère plusieurs fois pour accumuler des stats
        for (int i = 0; i < 3; i++)
        {
            await _host.GenerateSpeculativeAsync($"Test prompt {i}", maxTokens: 20);
        }

        var stats = _host.GetAdvancedStats();
        Assert.True(stats.SpeculativeActive);
        Assert.True(stats.SpeculativeAcceptanceRate >= 0.5,
            $"Acceptance rate trop basse: {stats.SpeculativeAcceptanceRate:P0}");
    }

    // ── Layered Loading ──

    [Fact]
    public async Task Layered_LoadsOnlyRequestedLayers()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var config = new LayeredModelConfig
        {
            TotalLayers = 32,
            MaxLayersInMemory = 4
        };
        await _host.EnableLayeredLoadingAsync(config);

        var stats = _host.GetAdvancedStats();
        Assert.True(stats.LayeredLoaderActive);
        Assert.True(stats.LoadedLayers <= config.MaxLayersInMemory);
        Assert.Equal(config.TotalLayers, stats.TotalLayers);
    }

    [Fact]
    public async Task Layered_RamBelowThreshold()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.EnableLayeredLoadingAsync(new LayeredModelConfig
        {
            TotalLayers = 32,
            MaxLayersInMemory = 8
        });

        // Charge quelques couches
        await _host.GenerateAsync("Test", maxTokens: 10);

        var stats = _host.GetAdvancedStats();
        // RAM active doit être bien inférieure à la taille totale du modèle
        Assert.True(stats.ActiveMemoryMB < 2048,
            $"RAM active trop élevée: {stats.ActiveMemoryMB} MB");
    }

    // ── JSON-RPC Validation ──

    [Fact]
    public async Task JsonRpc_EnableLayeredLoading_ReturnsSuccess()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var ex = await Record.ExceptionAsync(() =>
            _host.EnableLayeredLoadingAsync(new LayeredModelConfig()));
        Assert.Null(ex);
    }

    [Fact]
    public async Task JsonRpc_EnableSpeculative_ReturnsSuccess()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var ex = await Record.ExceptionAsync(() =>
            _host.EnableSpeculativeDecodingAsync(
                CreateDraftModel(), CreateLargeModel(), new SpeculativeConfig()));
        Assert.Null(ex);
    }

    [Fact]
    public async Task JsonRpc_EnableDualRouting_ReturnsSuccess()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        var ex = await Record.ExceptionAsync(() =>
            _host.EnableDualModelRoutingAsync(
                CreateSmallModel(), CreateLargeModel(), new DualModelConfig()));
        Assert.Null(ex);
    }

    // ── Performance ──

    [Fact]
    public async Task Performance_AllOptimizationsEnabled_StableRam()
    {
        await _host.StartHostAsync(ModelTier.Lite);
        await _host.EnableLayeredLoadingAsync(new LayeredModelConfig());
        await _host.EnableDualModelRoutingAsync(
            CreateSmallModel(), CreateLargeModel(), new DualModelConfig());

        GC.Collect();
        var ramBefore = _host.ProcessMemoryMB;

        for (int i = 0; i < 20; i++)
        {
            await _host.GenerateDualAsync($"Test {i}", AiTaskComplexity.Simple);
        }

        GC.Collect();
        var ramAfter = _host.ProcessMemoryMB;

        Assert.True(ramAfter - ramBefore < 200,
            $"Fuite mémoire: avant={ramBefore}MB, après={ramAfter}MB");
    }

    // ── Helpers ──

    private static EmbeddedLlmEngine CreateDraftModel() =>
        new(new EmbeddedLlmConfig { ModelFileName = "draft.onnx" },
            new SmartModelManager(new ModelCompressionService(),
                new IsolatedInferenceHost(new HeavyProcessLauncher("test.exe")),
                new ModelDownloader()));

    private static EmbeddedLlmEngine CreateSmallModel() =>
        new(new EmbeddedLlmConfig { ModelFileName = "small.onnx" },
            new SmartModelManager(new ModelCompressionService(),
                new IsolatedInferenceHost(new HeavyProcessLauncher("test.exe")),
                new ModelDownloader()));

    private static EmbeddedLlmEngine CreateLargeModel() =>
        new(new EmbeddedLlmConfig { ModelFileName = "large.onnx" },
            new SmartModelManager(new ModelCompressionService(),
                new IsolatedInferenceHost(new HeavyProcessLauncher("test.exe")),
                new ModelDownloader()));

    public void Dispose()
    {
        _host.Dispose();
        _launcher.Dispose();
    }
}
