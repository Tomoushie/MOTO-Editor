// Moto.Core/Collab/CrdtOfflineQueue.cs
// Queue persistante d'opérations CRDT en attente (offline-first).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Collab
{
    public sealed class QueuedOperation
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string DocumentId { get; init; } = string.Empty;
        public string ActorId { get; init; } = string.Empty;
        public long Lamport { get; init; }
        public string Kind { get; init; } = string.Empty; // insert, delete
        public int Position { get; init; }
        public string Text { get; init; } = string.Empty;
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public int RetryCount { get; set; }
    }

    public sealed class ConflictResolution
    {
        public bool Success { get; init; }
        public string ResolvedContent { get; init; } = string.Empty;
        public int ConflictsResolved { get; init; }
        public string Strategy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Queue offline-first pour CRDT.
    /// Les opérations sont stockées localement et rejouées à la reconnexion.
    /// </summary>
    public sealed class CrdtOfflineQueue
    {
        private readonly string _queuePath;
        private readonly List<QueuedOperator> _queue = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private const int MaxRetries = 5;

        public event Action<int>? QueueSizeChanged;

        public CrdtOfflineQueue(string workspaceRoot)
        {
            var dir = Path.Combine(workspaceRoot, ".moto", "crdt");
            Directory.CreateDirectory(dir);
            _queuePath = Path.Combine(dir, "offline-queue.json");
            Load();
        }

        public int Size
        {
            get
            {
                lock (_queue) return _queue.Count;
            }
        }

        public async Task EnqueueAsync(QueuedOperator op)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _queue.Add(op);
                Save();
                QueueSizeChanged?.Invoke(_queue.Count);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<QueuedOperator>> DequeueAllAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var ordered = _queue
                    .OrderBy(q => q.Lamport)
                    .ThenBy(q => q.CreatedUtc)
                    .ToList();
                _queue.Clear();
                Save();
                QueueSizeChanged?.Invoke(0);
                return ordered;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RequeueFailedAsync(QueuedOperator op)
        {
            if (op.RetryCount >= MaxRetries) return; // abandon

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                op.RetryCount++;
                _queue.Add(op);
                Save();
                QueueSizeChanged?.Invoke(_queue.Count);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Résout les conflits entre opérations locales et distantes.
        /// Stratégie : Last-Writer-Wins avec tie-break sur ActorId.
        /// </summary>
        public ConflictResolution ResolveConflicts(
            string localContent,
            IReadOnlyList<QueuedOperator> localOps,
            IReadOnlyList<QueuedOperator> remoteOps)
        {
            var all = localOps
                .Concat(remoteOps)
                .OrderBy(o => o.Lamport)
                .ThenBy(o => string.Compare(o.ActorId, "local", StringComparison.Ordinal))
                .ToList();

            var chars = new List<char>(localContent);
            int conflicts = 0;

            foreach (var op in all)
            {
                try
                {
                    if (op.Kind == "insert" && op.Position >= 0 && op.Position <= chars.Count)
                    {
                        for (int i = 0; i < op.Text.Length; i++)
                            chars.Insert(op.Position + i, op.Text[i]);
                    }
                    else if (op.Kind == "delete" && op.Position >= 0 && op.Position + 1 <= chars.Count)
                    {
                        chars.RemoveAt(op.Position);
                    }
                }
                catch
                {
                    conflicts++;
                }
            }

            return new ConflictResolution
            {
                Success = true,
                ResolvedContent = new string(chars.ToArray()),
                ConflictsResolved = conflicts,
                Strategy = "Lamport-order + LWW"
            };
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_queuePath))
                {
                    var json = File.ReadAllText(_queuePath);
                    var loaded = JsonSerializer.Deserialize<List<QueuedOperator>>(json);
                    if (loaded != null) _queue.AddRange(loaded);
                }
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_queue, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_queuePath, json);
            }
            catch { }
        }
    }
}
