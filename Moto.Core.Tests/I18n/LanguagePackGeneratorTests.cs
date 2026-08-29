// Moto.Core.Tests/I18n/LanguagePackGeneratorTests.cs
using System.Linq;
using Moto.Core.I18n;
using Xunit;

namespace Moto.Core.Tests.I18n
{
    public class LanguagePackGeneratorTests
    {
        [Fact]
        public void GenerateAllPacks_ReturnsAtLeast50Packs()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguagePackGenerator>();
            var generator = new LanguagePackGenerator(logger);

            var packs = generator.GenerateAllPacks();

            Assert.True(packs.Count >= 50, $"Expected >= 50 packs, got {packs.Count}");
        }

        [Fact]
        public void GeneratePack_HasRequiredFields()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguagePackGenerator>();
            var generator = new LanguagePackGenerator(logger);

            var pack = generator.GeneratePack("es", "Español", "🇪🇸");

            Assert.Equal("es", pack.Id);
            Assert.Equal("Español", pack.NativeName);
            Assert.Equal("🇪🇸", pack.Flag);
            Assert.NotEmpty(pack.Translations);
        }

        [Fact]
        public void AllPacks_HaveConsistentKeys()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguagePackGenerator>();
            var generator = new LanguagePackGenerator(logger);

            var packs = generator.GenerateAllPacks();
            var firstPackKeys = packs.First().Translations.Keys.ToHashSet();

            foreach (var pack in packs)
            {
                Assert.True(firstPackKeys.SetEquals(pack.Translations.Keys.ToHashSet()),
                    $"Pack {pack.Id} has inconsistent keys");
            }
        }
    }
}
