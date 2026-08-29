// Moto.Core.Tests/Collab/CrdtAutomergeTests.cs
using System;
using System.Threading.Tasks;
using Moto.Core.Collab;
using Xunit;

namespace Moto.Core.Tests.Collab
{
    public class CrdtAutomergeTests
    {
        [Fact]
        public async Task TwoActors_ConvergeAfterConcurrentInserts()
        {
            // GIVEN : deux acteurs avec le même document initial
            var alice = new CrdtAutomergeClient(actorId: 1);
            var bob = new CrdtAutomergeClient(actorId: 2);
            var initial = "Hello";

            // WHEN : insertions concurrentes à la même position
            var patchA = await alice.InsertAsync(5, " World", "doc1");
            var patchB = await bob.InsertAsync(5, "!", "doc1");

            // Merge croisé
            await alice.MergeAsync(patchB);
            await bob.MergeAsync(patchA);

            // THEN : convergence (même contenu)
            var contentA = await alice.RebuildAsync(initial);
            var contentB = await bob.RebuildAsync(initial);

            Assert.Equal(contentA, contentB);
        }

        [Fact]
        public async Task InsertAndDelete_Converge()
        {
            var alice = new CrdtAutomergeClient(actorId: 1);
            var bob = new CrdtAutomergeClient(actorId: 2);
            var initial = "Hello World";

            // Alice supprime "World"
            var patchDel = await alice.DeleteAsync(5, 6, "doc1");
            // Bob insère "!" à la fin
            var patchIns = await bob.InsertAsync(11, "!", "doc1");

            await alice.MergeAsync(patchIns);
            await bob.MergeAsync(patchDel);

            var contentA = await alice.RebuildAsync(initial);
            var contentB = await bob.RebuildAsync(initial);

            Assert.Equal(contentA, contentB);
        }

        [Fact]
        public async Task ExportImport_PreservesHistory()
        {
            var alice = new CrdtAutomergeClient(actorId: 1);
            await alice.InsertAsync(0, "Test", "doc1");

            var state = alice.ExportState();

            var bob = new CrdtAutomergeClient(actorId: 2);
            await bob.ImportStateAsync(state);

            var content = await bob.RebuildAsync(string.Empty);
            Assert.Equal("Test", content);
        }
    }
}
