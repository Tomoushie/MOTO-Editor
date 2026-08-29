// Moto.Core.Tests/Collab/CrdtOfflineTests.cs
using System.IO;
using System.Threading.Tasks;
using Moto.Core.Collab;
using Xunit;

namespace Moto.Core.Tests.Collab
{
    public class CrdtOfflineTests : System.IDisposable
    {
        private readonly string _tempDir;

        public CrdtOfflineTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "moto-crdt-offline-" + System.Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public async Task Queue_PersistsBetweenRestarts()
        {
            var queue1 = new CrdtOfflineQueue(_tempDir);
            await queue1.EnqueueAsync(new QueuedOperator { DocumentId = "d1", ActorId = "a1", Lamport = 1, Kind = "insert", Position = 0, Text = "X" });

            // Simule un redémarrage
            var queue2 = new CrdtOfflineQueue(_tempDir);
            Assert.Equal(1, queue2.Size);
        }

        [Fact]
        public async Task DequeueAll_ReturnsInLamportOrder()
        {
            var queue = new CrdtOfflineQueue(_tempDir);
            await queue.EnqueueAsync(new QueuedOperator { DocumentId = "d1", ActorId = "a1", Lamport = 3, Kind = "insert", Position = 0, Text = "C" });
            await queue.EnqueueAsync(new QueuedOperator { DocumentId = "d1", ActorId = "a1", Lamport = 1, Kind = "insert", Position = 0, Text = "A" });
            await queue.EnqueueAsync(new QueuedOperator { DocumentId = "d1", ActorId = "a1", Lamport = 2, Kind = "insert", Position = 0, Text = "B" });

            var ops = await queue.DequeueAllAsync();
            Assert.Equal(3, ops.Count);
            Assert.Equal(1, ops[0].Lamport);
            Assert.Equal(2, ops[1].Lamport);
            Assert.Equal(3, ops[2].Lamport);
            Assert.Equal(0, queue.Size);
        }

        [Fact]
        public async Task RequeueFailed_IncrementsRetryCount()
        {
            var queue = new CrdtOfflineQueue(_tempDir);
            var op = new QueuedOperator { DocumentId = "d1", ActorId = "a1", Lamport = 1, Kind = "insert", Position = 0, Text = "X", RetryCount = 0 };

            await queue.RequeueFailedAsync(op);
            Assert.Equal(1, queue.Size);

            var ops = await queue.DequeueAllAsync();
            Assert.Equal(1, ops[0].RetryCount);
        }

        [Fact]
        public void ResolveConflicts_MergesLocalAndRemote()
        {
            var queue = new CrdtOfflineQueue(_tempDir);
            var local = new[] {
                new QueuedOperator { DocumentId = "d1", ActorId = "local", Lamport = 1, Kind = "insert", Position = 0, Text = "L" }
            };
            var remote = new[] {
                new QueuedOperator { DocumentId = "d1", ActorId = "remote", Lamport = 2, Kind = "insert", Position = 1, Text = "R" }
            };

            var resolution = queue.ResolveConflicts("AB", local, remote);
            Assert.True(resolution.Success);
            Assert.Contains("L", resolution.ResolvedContent);
            Assert.Contains("R", resolution.ResolvedContent);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }
}
