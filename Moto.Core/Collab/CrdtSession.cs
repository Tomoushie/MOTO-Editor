// Moto.Core/Collab/CrdtSession.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.Collab
{
    public sealed class RemoteCursor
    {
        public string UserId { get; init; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
        public string Color { get; init; } = "#D97757";
    }

    /// <summary>
    /// Session collaborative CRDT : remplace PatchEngine pour du temps réel.
    /// Gère documents partagés + curseurs distants.
    /// </summary>
    public sealed class CrdtSession : IDisposable
    {
        private readonly Dictionary<string, CrdtDocument> _documents = new();
        private readonly List<RemoteCursor> _cursors = new();

        public event Action<string, CrdtOperation>? OperationBroadcast;
        public event Action<RemoteCursor>? CursorMoved;

        public CrdtDocument GetOrCreateDocument(string path, string userId, string initialContent = "")
        {
            if (!_documents.TryGetValue(path, out var doc))
            {
                doc = new CrdtDocument(userId, initialContent);
                _documents[path] = doc;
            }
            return doc;
        }

        public void BroadcastOperation(string path, CrdtOperation op)
            => OperationBroadcast?.Invoke(path, op);

        public void UpdateRemoteCursor(string userId, string path, int line, int column)
        {
            var cursor = _cursors.FirstOrDefault(c => c.UserId == userId);
            if (cursor == null)
            {
                cursor = new RemoteCursor { UserId = userId };
                _cursors.Add(cursor);
            }
            cursor.Path = path;
            cursor.Line = line;
            cursor.Column = column;
            cursor.LastSeenUtc = DateTime.UtcNow;
            CursorMoved?.Invoke(cursor);
        }

        public IReadOnlyList<RemoteCursor> GetActiveCursors(TimeSpan timeout)
            => _cursors.Where(c => DateTime.UtcNow - c.LastSeenUtc < timeout).ToList();

        public void Dispose()
        {
            _documents.Clear();
            _cursors.Clear();
        }
    }
}
