using FluentAssertions;
using Moto.Core.AI.Internal;
using Xunit;

namespace Moto.Tests.E2E;

/// <summary>
/// Tests du LayeredModelLoader : préfetch non bloquant, lecture asynchrone, timeouts.
/// </summary>
public class LayeredLoaderTests
{
    private readonly LayeredModelLoader _loader;

    public LayeredLoaderTests()
    {
        _loader = new LayeredModelLoader(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<LayeredModelLoader>());
    }

    [Fact]
    public async Task PrefetchNextLayer_ShouldNotBlock()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-layered-prefetch.onnx");

        // Act : le préfetch doit retourner immédiatement
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var prefetchTask = _loader.PrefetchNextLayerAsync(modelPath, layerIndex: 0);
        sw.Stop();

        // Assert : le préfetch ne doit pas bloquer (< 100ms)
        sw.ElapsedMilliseconds.Should().BeLessThan(100);

        await prefetchTask;
    }

    [Fact]
    public async Task LoadLayerAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-layered-timeout.onnx");
        var timeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert : doit respecter le timeout
        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => _loader.LoadLayerAsync(modelPath, layerIndex: 0, timeout));
    }

    [Fact]
    public async Task LoadAllLayersAsync_ShouldLoadSequentially()
    {
        // Arrange
        var modelPath = CreateTestModelFile("test-layered-all.onnx");

        // Act
        var layers = await _loader.LoadAllLayersAsync(modelPath);

        // Assert
        layers.Should().NotBeEmpty();
    }

    private static string CreateTestModelFile(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), name);
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01, 0x02, 0x03 });
        return path;
    }
}
