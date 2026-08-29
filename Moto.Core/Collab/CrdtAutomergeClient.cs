// Moto.Core/Collab/CrdtAutomergeClient.cs
// Wrapper Automerge pour la collaboration temps réel.
// Automerge est un CRDT qui garantit la convergence sans verrou central.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Collab
{
    /// <summary>Opération CRDT générée par Automerge.</summary>
    public sealed class CrdtPatch
    {
        public byte[] Data { get; init; } = Array.Empty<byte>();
        public long ActorId { get; init; }
        public long Seq { get; init; }
    }

    /// <summary>
    /// Client Automerge natif via P/Invoke.
    /// En production : utiliser le package NuGet Automerge.NET ou
    /// compiler la lib Rust et exposer via FFI.
    /// </summary>
    public sealed class CrdtAutomergeClient : IDisposable
    {
        // ── P/Invoke Automerge (lib automerge-c) ──
        // En production, ces méthodes appellent la lib native.
        // Ici : implémentation pure C# pour la portabilité.

        private readonly Dictionary<long, List<string>> _opsByActor = new();
        private readonly List<(string content, long actorId, long seq)> _history = new();
        private long _localActorId;
        private long _localSeq;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public event Action<CrdtPatch>? PatchGenerated;

        public CrdtAutomergeClient(long actorId)
        {
            _localActorId = actorId;
            _localSeq = 0;
        }

        /// <summary>
        /// Applique une insertion locale et génère un patch diffusable.
        /// </summary>
        public async Task<CrdtPatch> InsertAsync(int position, string text, string documentId)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _localSeq++;
                var op = $"ins:{position}:{text}:{_localActorId}:{_localSeq}";

                if (!_opsByActor.ContainsKey(_localActorId))
                    _opsByActor[_localActorId] = new List<string>();
                _opsByActor[_localActorId].Add(op);

                _history.Add((op, _localActorId, _localSeq));

                var patch = new CrdtPatch
                {
                    Data = Encoding.UTF8.GetBytes(op),
                    ActorId = _localActorId,
                    Seq = _localSeq
                };

                PatchGenerated?.Invoke(patch);
                return patch;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Applique une suppression locale et génère un patch.
        /// </summary>
        public async Task<CrdtPatch> DeleteAsync(int position, int length, string documentId)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _localSeq++;
                var op = $"del:{position}:{length}:{_localActorId}:{_localSeq}";

                if (!_opsByActor.ContainsKey(_localActorId))
                    _opsByActor[_localActorId] = new List<string>();
                _opsByActor[_localActorId].Add(op);

                _history.Add((op, _localActorId, _localSeq));

                var patch = new CrdtPatch
                {
                    Data = Encoding.UTF8.GetBytes(op),
                    ActorId = _localActorId,
                    Seq = _localSeq
                };

                PatchGenerated?.Invoke(patch);
                return patch;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Fusionne un patch distant (convergence CRDT).
        /// Automerge garantit que l'ordre d'application n'affecte pas le résultat final.
        /// </summary>
        public async Task MergeAsync(CrdtPatch remotePatch)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var op = Encoding.UTF8.GetString(remotePatch.Data);

                if (!_opsByActor.ContainsKey(remotePatch.ActorId))
                    _opsByActor[remotePatch.ActorId] = new List<string>();
                _opsByActor[remotePatch.ActorId].Add(op);

                _history.Add((op, remotePatch.ActorId, remotePatch.Seq));
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Reconstruit le document depuis l'historique des opérations.
        /// C'est la fonction de convergence CRDT.
        /// </summary>
        public async Task<string> RebuildAsync(string initialContent)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Trier par (actorId, seq) pour la convergence déterministe
                var sortedOps = new List<(string op, long actorId, long seq)>(_history);
                sortedOps.Sort((a, b) =>
                {
                    var actorCmp = a.actorId.CompareTo(b.actorId);
                    return actorCmp != 0 ? actorCmp : a.seq.CompareTo(b.seq);
                });

                var chars = new List<char>(initialContent);
                foreach (var (op, _, _) in sortedOps)
                {
                    var parts = op.Split(':');
                    if (parts.Length < 2) continue;

                    if (parts[0] == "ins" && parts.Length >= 3)
                    {
                        if (int.TryParse(parts[1], out var pos) && pos >= 0 && pos <= chars.Count)
                        {
                            var text = parts[2];
                            for (int i = 0; i < text.Length && pos + i <= chars.Count; i++)
                                chars.Insert(pos + i, text[i]);
                        }
                    }
                    else if (parts[0] == "del" && parts.Length >= 3)
                    {
                        if (int.TryParse(parts[1], out var pos) &&
                            int.TryParse(parts[2], out var len) &&
                            pos >= 0 && pos + len <= chars.Count)
                        {
                            chars.RemoveRange(pos, len);
                        }
                    }
                }

                return new string(chars.ToArray());
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Exporte l'historique complet pour la synchronisation.
        /// </summary>
        public byte[] ExportState()
        {
            var sb = new StringBuilder();
            foreach (var (op, actorId, seq) in _history)
                sb.AppendLine($"{actorId}:{seq}:{op}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Importe un état distant pour la synchronisation initiale.
        /// </summary>
        public async Task ImportStateAsync(byte[] state)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var lines = Encoding.UTF8.GetString(state).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(':', 3);
                    if (parts.Length < 3) continue;
                    if (!long.TryParse(parts[0], out var actorId)) continue;
                    if (!long.TryParse(parts[1], out var seq)) continue;

                    if (!_opsByActor.ContainsKey(actorId))
                        _opsByActor[actorId] = new List<string>();
                    _opsByActor[actorId].Add(parts[2]);
                    _history.Add((parts[2], actorId, seq));
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose() => _gate.Dispose();
    }
}
