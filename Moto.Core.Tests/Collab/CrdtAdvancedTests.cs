// Moto.Core.Tests/Collab/CrdtAdvancedTests.cs
using System;
using System.Threading.Tasks;
using Moto.Core.Collab;
using Xunit;

namespace Moto.Core.Tests.Collab
{
    public class CrdtAdvancedTests
    {
        [Fact]
        public async Task Reconnection_PreservesDocumentState()
        {
            // GIVEN : un utilisateur avec un document modifié
            var alice = new CrdtAutomergeClient(actorId: 1);
            var initial = "Hello World";

            await alice.InsertAsync(5, " Beautiful", "doc1");
            var state1 = alice.ExportState();

            // WHEN : déconnexion + reconnexion
            var alice2 = new CrdtAutomergeClient(actorId: 1);
            await alice2.ImportStateAsync(state1);

            // THEN : l'état est restauré
            var content = await alice2.RebuildAsync(initial);
            Assert.Contains("Beautiful", content);
        }

        [Fact]
        public async Task ConflictResolution_DeterministicOrdering()
        {
            // GIVEN : 3 utilisateurs font des modifications concurrentes
            var alice = new CrdtAutomergeClient(actorId: 1);
            var bob = new CrdtAutomergeClient(actorId: 2);
            var charlie = new CrdtAutomergeClient(actorId: 3);
            var initial = "AB";

            // WHEN : insertions concurrentes à la même position
            var patchA = await alice.InsertAsync(1, "X", "doc1");
            var patchB = await bob.InsertAsync(1, "Y", "doc1");
            var patchC = await charlie.InsertAsync(1, "Z", "doc1");

            // Appliquer tous les patches dans différents ordres
            await alice.MergeAsync(patchB);
            await alice.MergeAsync(patchC);

            await bob.MergeAsync(patchA);
            await bob.MergeAsync(patchC);

            await charlie.MergeAsync(patchA);
            await charlie.MergeAsync(patchB);

            // THEN : convergence déterministe
            var contentA = await alice.RebuildAsync(initial);
            var contentB = await bob.RebuildAsync(initial);
            var contentC = await charlie.RebuildAsync(initial);

            Assert.Equal(contentA, contentB);
            Assert.Equal(contentB, contentC);
            Assert.Equal(5, contentA.Length); // A + X/Y/Z + B
        }

        [Fact]
        public async Task LargeDocument_HandlesMultipleOperations()
        {
            var client = new CrdtAutomergeClient(actorId: 1);
            var initial = "";

            // 100 insertions
            for (int i = 0; i < 100; i++)
            {
                await client.InsertAsync(i, (char)('A' + (i % 26)), "doc1");
            }

            var content = await client.RebuildAsync(initial);
            Assert.Equal(100, content.Length);
        }

        [Fact]
        public async Task UndoRedo_PartialHistory()
        {
            var client = new CrdtAutomergeClient(actorId: 1);
            var initial = "Hello";

            var patch1 = await client.InsertAsync(5, " World", "doc1");
            var patch2 = await client.InsertAsync(11, "!", "doc1");

            // Export à mi-parcours
            var midState = client.ExportState();

            // Continuer
            await client.InsertAsync(12, " More", "doc1");

            // Restaurer l'état intermédiaire
            var client2 = new CrdtAutomergeClient(actorId: 1);
            await client2.ImportStateAsync(midState);

            var content = await client2.RebuildAsync(initial);
            Assert.Equal("Hello World!", content);
        }

        [Fact]
        public void CursorRenderer_FiltersExpiredCursors()
        {
            var renderer = new CrdtCursorRenderer();

            // Curseur récent
            renderer.UpdateCursor(new RemoteCursorView
            {
                UserId = "alice",
                DisplayName = "Alice",
                DocumentPath = "/test.cs",
                Line = 10,
                Column = 5,
                LastSeenUtc = DateTime.UtcNow
            });

            // Curseur expiré (il y a 2 minutes)
            renderer.UpdateCursor(new RemoteCursorView
            {
                UserId = "bob",
                DisplayName = "Bob",
                DocumentPath = "/test.cs",
                Line = 20,
                Column = 10,
                LastSeenUtc = DateTime.UtcNow.AddMinutes(-2)
            });

            var cursors = renderer.GetAllActiveCursors();
            Assert.Single(cursors);
            Assert.Equal("alice", cursors[0].UserId);
        }
    }
}
