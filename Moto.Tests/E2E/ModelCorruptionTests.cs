using FluentAssertions;
using Moto.Core.AI.Internal;
using Moto.Core.Security;
using System.Security.Cryptography;
using Xunit;

namespace Moto.Tests.E2E;

/// <summary>
/// Tests E2E : détection de modèle corrompu (SHA256 mismatch).
/// </summary>
public class ModelCorruptionTests
{
    private readonly ModelSecurityService _securityService;
    private readonly string _tempDir;

    public ModelCorruptionTests()
    {
        _securityService = new ModelSecurityService();
        _tempDir = Path.Combine(Path.GetTempPath(), "moto_tests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task LoadCorruptedModel_ShouldThrowException()
    {
        // Arrange : créer un fichier modèle factice
        var modelPath = Path.Combine(_tempDir, "corrupted_model.onnx");
        await File.WriteAllBytesAsync(modelPath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

        // Attendu : SHA256 correct pour un fichier non corrompu
        var expectedHash = "deadbeef"; // Hash délibérément incorrect

        // Act & Assert : la vérification doit échouer
        var exception = await Assert.ThrowsAsync<ModelIntegrityException>(
            () => _securityService.VerifyModelAsync(modelPath, expectedHash));

        exception.Message.Should().Contain("SHA256 mismatch");
    }

    [Fact]
    public async Task LoadValidModel_ShouldPassVerification()
    {
        // Arrange : créer un fichier modèle valide
        var modelPath = Path.Combine(_tempDir, "valid_model.onnx");
        var content = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        await File.WriteAllBytesAsync(modelPath, content);

        // Calcule le hash attendu
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(File.OpenRead(modelPath));
        var expectedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Act : la vérification doit passer
        var result = await _securityService.VerifyModelAsync(modelPath, expectedHash);

        // Assert
        result.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
