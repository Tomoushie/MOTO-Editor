// Moto.Core.Tests/Security/CryptographicSignerTests.cs
using System;
using System.IO;
using Moto.Core.Security;
using Xunit;

namespace Moto.Core.Tests.Security
{
    // Dossier de clés temporaire, supprimé après chaque test : sans lui,
    // GenerateKeyPair écrivait dans le vrai %AppData%\MotoEditor\keys.
    public class CryptographicSignerTests : IDisposable
    {
        private readonly string _tempDir;

        public CryptographicSignerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void SignAndVerify_ValidSignature_ReturnsTrue()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CryptographicSigner>();
            var signer = new CryptographicSigner(logger, _tempDir);

            var (publicKey, privateKey) = signer.GenerateKeyPair("test-publisher");
            var content = "Test content to sign";

            var signature = signer.Sign(content, privateKey);
            var isValid = signer.Verify(content, signature, publicKey);

            Assert.True(isValid);

            // Les clés sont bien écrites dans le dossier temporaire.
            Assert.True(File.Exists(Path.Combine(_tempDir, "test-publisher.pub")));
            Assert.True(File.Exists(Path.Combine(_tempDir, "test-publisher.key")));
        }

        [Fact]
        public void Verify_TamperedContent_ReturnsFalse()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CryptographicSigner>();
            var signer = new CryptographicSigner(logger, _tempDir);

            var (publicKey, privateKey) = signer.GenerateKeyPair("test-publisher");
            var content = "Original content";

            var signature = signer.Sign(content, privateKey);
            var tamperedContent = "Tampered content";
            var isValid = signer.Verify(tamperedContent, signature, publicKey);

            Assert.False(isValid);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
