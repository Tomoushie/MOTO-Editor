// Moto.Core.Tests/AI/Builders/AutoProjectBuilderTests.cs
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moto.Core.AI.Builders;
using Xunit;

namespace Moto.Core.Tests.AI.Builders
{
    /// <summary>
    /// ★ AJOUT (28/09, « crée un projet… » depuis le chat) : le chat montre les fichiers AVANT de les écrire (PlannedFiles) — la liste
    /// montrée doit être exactement celle que BuildAsync écrit, sinon la confirmation mentirait.
    /// </summary>
    public class AutoProjectBuilderTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "moto-apb-" + Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("crée un jeu snake", "SnakeRetro")]
        [InlineData("génère moi un jeu de plateforme", "PlatformerGame")]
        [InlineData("fais moi un projet", "MotoProject")]
        public async Task PlannedFiles_SontExactementCeuxQueBuildAsyncEcrit(string demande, string dossier)
        {
            var builder = new AutoProjectBuilder();
            Assert.True(AutoProjectBuilder.ShouldHandle(demande));
            Assert.Equal(Path.Combine(_root, dossier), builder.ComputeProjectDir(demande, _root));

            var prevus = builder.PlannedFiles(demande);
            Assert.False(Directory.Exists(_root)); // rien n'est écrit avant BuildAsync

            var result = await builder.BuildAsync(demande, _root);

            Assert.True(result.Success, result.Explanation);
            var ecrits = Directory.GetFiles(Path.Combine(_root, dossier), "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Path.Combine(_root, dossier), f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal).ToList();
            Assert.Equal(prevus.Select(f => f.Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal), ecrits);
        }

        [Theory]
        [InlineData("explique ce fichier")]
        [InlineData("crée une fonction qui trie une liste")]
        [InlineData("/window git")]
        public void ShouldHandle_NePrendPasUneAutreDemandePourUnProjet(string demande)
            => Assert.False(AutoProjectBuilder.ShouldHandle(demande));

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
