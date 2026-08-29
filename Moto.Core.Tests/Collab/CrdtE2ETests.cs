// Moto.Core.Tests/Collab/CrdtE2ETests.cs
using System.Linq;
using Moto.Core.Collab;
using Xunit;

namespace Moto.Core.Tests.Collab
{
    public class CrdtE2ETests
    {
        [Fact]
        public void E2E_TwoUsers_ConvergeAfterCrossOperations()
        {
            var alice = new CrdtDocument("alice", "Hello");
            var bob = new CrdtDocument("bob", "Hello");

            // Alice ajoute " World" caractère par caractère
            foreach (var c in " World")
            {
                var op = alice.Insert(alice.GetText().Length, c);
                bob.ApplyRemote(op);
            }

            // Bob ajoute "!" à la fin
            var op2 = bob.Insert(bob.GetText().Length, '!');
            alice.ApplyRemote(op2);

            // Convergence : les deux documents sont identiques
            Assert.Equal(alice.GetText(), bob.GetText());
            Assert.Equal("Hello World!", alice.GetText());
        }

        [Fact]
        public void E2E_ConcurrentInserts_NoDataLoss()
        {
            var alice = new CrdtDocument("alice", "AB");
            var bob = new CrdtDocument("bob", "AB");

            // Insertions concurrentes à la même position
            var opA = alice.Insert(1, 'X');
            var opB = bob.Insert(1, 'Y');

            alice.ApplyRemote(opB);
            bob.ApplyRemote(opA);

            // Même texte (pas forcément même ordre mais converge)
            Assert.Equal(alice.GetText(), bob.GetText());
            Assert.Equal(3, alice.GetText().Length); // A, X/Y, B
        }

        [Fact]
        public void E2E_Delete_Operation_Propagates()
        {
            var alice = new CrdtDocument("alice", "Hello");
            var bob = new CrdtDocument("bob", "Hello");

            var op = alice.Delete(0); // supprime 'H'
            bob.ApplyRemote(op);

            Assert.Equal("ello", alice.GetText());
            Assert.Equal("ello", bob.GetText());
        }

        [Fact]
        public void E2E_Session_MultipleDocuments_CursorsTracked()
        {
            using var session = new CrdtSession();
            var aliceDoc = session.GetOrCreateDocument("/a.cs", "alice", "class A {}");
            var bobDoc = session.GetOrCreateDocument("/a.cs", "bob", "class A {}");

            session.UpdateRemoteCursor("alice", "/a.cs", 0, 5);
            session.UpdateRemoteCursor("bob", "/a.cs", 0, 8);

            var cursors = session.GetActiveCursors(System.TimeSpan.FromMinutes(5));
            Assert.Equal(2, cursors.Count);
            Assert.Contains(cursors, c => c.UserId == "alice" && c.Column == 5);
            Assert.Contains(cursors, c => c.UserId == "bob" && c.Column == 8);
        }
    }
}
