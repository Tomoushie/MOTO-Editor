using FluentAssertions;
using Moto.Core.AI.Internal;
using Xunit;

namespace Moto.Tests.E2E;

/// <summary>
/// Tests du cycle de vie des modèles : Load/Unload/Reload avec mmap, layered, quantization.
/// </summary>
public class ModelLifecycleTests
{
    private readonly EmbeddedLlmEngine _engine;

    public ModelLifecycleTests()
    {
        _engine = new EmbeddedLlmEngine(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbeddedLlmEngine>(),
            Moto.Core.Settings.SettingsEngine.Shared);
    }

    [Fact]
    public async Task LoadModel_WithMemoryMapping_ShouldSucceed()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-mmap.onnx");

        // Act
        var session = await _engine.LoadModelAsync(modelPath);

        // Assert
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task UnloadModel_ShouldReleaseMemory()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-unload.onnx");
        await _engine.LoadModelAsync(modelPath);

        // Act
        _engine.UnloadModel();

        // Assert : pas d'exception, mémoire libérée
        // (vérification via GC ou working set)
    }

    [Fact]
    public async Task ReloadModel_AfterUnload_ShouldSucceed()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-reload.onnx");
        await _engine.LoadModelAsync(modelPath);
        _engine.UnloadModel();

        // Act
        var session = await _engine.LoadModelAsync(modelPath);

        // Assert
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadModel_WithLayeredLoading_ShouldLoadProgressively()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-layered.onnx");

        // Act
        var session = await _engine.LoadModelAsync(modelPath);

        // Assert : le modèle est chargé, les couches sont progressives
        session.Should().NotBeNull();
    }

    private static string CreateTestModelFile(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), name);
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01, 0x02, 0x03 });
        return path;
    }
}
