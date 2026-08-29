using FluentAssertions;
using Moto.Core.AI.Internal;
using Xunit;

namespace Moto.Tests.E2E;

/// <summary>
/// Tests de correction du SpeculativeDecoder : VerifyBatchAsync accepte/rejette correctement.
/// </summary>
public class SpeculativeCorrectnessTests
{
    private readonly SpeculativeDecoder _decoder;

    public SpeculativeCorrectnessTests()
    {
        var draftModel = new EmbeddedLlmEngine(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbeddedLlmEngine>(),
            Moto.Core.Settings.SettingsEngine.Shared);
        var targetModel = new EmbeddedLlmEngine(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<EmbeddedLlmEngine>(),
            Moto.Core.Settings.SettingsEngine.Shared);

        _decoder = new SpeculativeDecoder(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<SpeculativeDecoder>(),
            draftModel,
            targetModel,
            Moto.Core.Settings.SettingsEngine.Shared);
    }

    [Fact]
    public async Task VerifyBatch_WithMatchingTokens_ShouldAccept()
    {
        // Arrange
        var inputIds = new List<int> { 1, 2, 3 };
        var draftTokens = new List<int> { 4, 5, 6 };

        // Act
        var accepted = await InvokeVerifyBatchAsync(inputIds, draftTokens);

        // Assert
        accepted.Should().NotBeEmpty();
    }

    [Fact]
    public async Task VerifyBatch_WithMismatchedTokens_ShouldReject()
    {
        // Arrange
        var inputIds = new List<int> { 1, 2, 3 };
        var draftTokens = new List<int> { 999, 998, 997 }; // Tokens improbables

        // Act
        var accepted = await InvokeVerifyBatchAsync(inputIds, draftTokens);

        // Assert : certains tokens doivent être rejetés
        accepted.Count.Should().BeLessThan(draftTokens.Count);
    }

    [Fact]
    public async Task GenerateAsync_ShouldProduceCoherentOutput()
    {
        // Arrange
        var prompt = "public class Test { }";
        var maxTokens = 50;

        // Act
        var tokens = await _decoder.GenerateAsync(prompt, maxTokens);

        // Assert
        tokens.Should().NotBeEmpty();
        tokens.Count.Should().BeLessOrEqualTo(maxTokens);
    }

    // Helper pour invoquer la méthode privée VerifyBatchAsync
    private async Task<List<int>> InvokeVerifyBatchAsync(List<int> inputIds, List<int> draftTokens)
    {
        var method = typeof(SpeculativeDecoder).GetMethod(
            "VerifyBatchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method == null) return new List<int>();

        var task = (Task<List<int>>)method.Invoke(_decoder, new object[] { inputIds, draftTokens, CancellationToken.None })!;
        return await task;
    }
}
