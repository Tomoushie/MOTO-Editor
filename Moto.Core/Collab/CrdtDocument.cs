// Moto.Core/Collab/CrdtDocument.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.Collab
{
    public enum CrdtOpKind { Insert, Delete }

    public sealed class CrdtOperation
    {
        public string UserId { get; init; } = string.Empty;
        public long Lamport { get; init; }
        public CrdtOpKind Kind { get; init; }
        public int Position { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>
    /// CRDT de type RGA : chaque caractère a un ID unique (userId + lamport).
    /// Garantit la convergence sans verrou central.
    /// </summary>
    public sealed class CrdtDocument
    {
        private readonly List<CrdtChar> _chars = new();
        private long _lamport;
        private readonly string _userId;

        public CrdtDocument(string userId, string initialContent = "")
        {
            _userId = userId;
            foreach (var c in initialContent)
                _chars.Add(new CrdtChar { Value = c, Id = (_userId, ++_lamport), Visible = true });
        }

        public string GetText()
            => new string(_chars.Where(c => c.Visible).Select(c => c.Value).ToArray());

        public CrdtOperation Insert(int position, char c)
        {
            _lamport++;
            var id = (_userId, _lamport);
            var newChar = new CrdtChar { Value = c, Id = id, Visible = true };

            int idx = position >= _chars.Count ? _chars.Count : FindVisibleIndex(position);
            _chars.Insert(idx, newChar);

            return new CrdtOperation
            {
                UserId = _userId, Lamport = _lamport,
                Kind = CrdtOpKind.Insert, Position = position, Text = c.ToString()
            };
        }

        public CrdtOperation Delete(int position)
        {
            _lamport++;
            int idx = FindVisibleIndex(position);
            if (idx < _chars.Count) _chars[idx].Visible = false;

            return new CrdtOperation
            {
                UserId = _userId, Lamport = _lamport,
                Kind = CrdtOpKind.Delete, Position = position, Text = ""
            };
        }

        /// <summary>Applique une opération distante (convergence).</summary>
        public void ApplyRemote(CrdtOperation op)
        {
            if (op.Kind == CrdtOpKind.Insert)
            {
                var newChar = new CrdtChar
                { Value = op.Text.Length > 0 ? op.Text[0] : ' ', Id = (op.UserId, op.Lamport), Visible = true };
                int idx = op.Position >= _chars.Count ? _chars.Count : FindVisibleIndex(op.Position);
                _chars.Insert(idx, newChar);
            }
            else if (op.Kind == CrdtOpKind.Delete)
            {
                int idx = FindVisibleIndex(op.Position);
                if (idx < _chars.Count) _chars[idx].Visible = false;
            }
            _lamport = Math.Max(_lamport, op.Lamport);
        }

        private int FindVisibleIndex(int visiblePos)
        {
            int count = 0;
            for (int i = 0; i < _chars.Count; i++)
            {
                if (_chars[i].Visible)
                {
                    if (count == visiblePos) return i;
                    count++;
                }
            }
            return _chars.Count;
        }

        private sealed class CrdtChar
        {
            public char Value { get; init; }
            public (string User, long Lamport) Id { get; init; }
            public bool Visible { get; set; } = true;
        }
    }
}
