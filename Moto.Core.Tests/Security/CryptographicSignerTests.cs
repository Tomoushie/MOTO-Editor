// Moto.Core.Tests/Security/CryptographicSignerTests.cs
using Moto.Core.Security;
using Xunit;

namespace Moto.Core.Tests.Security
{
    public class CryptographicSignerTests
    {
        [Fact]
        public void SignAndVerify_ValidSignature_ReturnsTrue()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CryptographicSigner>();
            var signer = new CryptographicSigner(logger);

            var (publicKey, privateKey) = signer.GenerateKeyPair("test-publisher");
            var content = "Test content to sign";

            var signature = signer.Sign(content, privateKey);
            var isValid = signer.Verify(content, signature, publicKey);

            Assert.True(isValid);
        }

        [Fact]
        public void Verify_TamperedContent_ReturnsFalse()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<CryptographicSigner>();
            var signer = new CryptographicSigner(logger);

            var (publicKey, privateKey) = signer.GenerateKeyPair("test-publisher");
            var content = "Original content";

            var signature = signer.Sign(content, privateKey);
            var tamperedContent = "Tampered content";
            var isValid = signer.Verify(tamperedContent, signature, publicKey);

            Assert.False(isValid);
        }
    }
}
