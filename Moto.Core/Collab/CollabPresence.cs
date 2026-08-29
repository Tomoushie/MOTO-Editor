// Moto.Core/Collab/CollabPresence.cs
using System;
using System.Collections.Generic;

namespace Moto.Core.Collab
{
    /// <summary>5. Présence temps réel : qui travaille sur le projet et où.</summary>
    public class CollabPeer
    {
        public Guid PeerId { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Anonyme";
        public string Color { get; set; } = "#4da3ff";
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Fichier + position curseur actuels.</summary>
        public string ActiveFile { get; set; } = string.Empty;
        public int CursorLine { get; set; }
    }

    public class CollabPresence
    {
        private readonly Dictionary<Guid, CollabPeer> _peers = new();

        /// <summary>Pairs en ligne (vus dans les 30 dernières secondes).</summary>
        public IReadOnlyList<CollabPeer> Online(TimeSpan? window = null)
        {
            var cutoff = DateTime.UtcNow - (window ?? TimeSpan.FromSeconds(30));
            var online = new List<CollabPeer>();

            foreach (var kv in _peers)
            {
                if (kv.Value.LastSeenUtc >= cutoff)
                {
                    online.Add(kv.Value);
                }
            }

            return online;
        }

        public void Upsert(CollabPeer peer)
        {
            peer.LastSeenUtc = DateTime.UtcNow;
            _peers[peer.PeerId] = peer;
        }

        public void Remove(Guid peerId) => _peers.Remove(peerId);

        public void ClearStale(TimeSpan? window = null)
        {
            var cutoff = DateTime.UtcNow - (window ?? TimeSpan.FromSeconds(30));
            var stale = new List<Guid>();

            foreach (var kv in _peers)
            {
                if (kv.Value.LastSeenUtc < cutoff) stale.Add(kv.Key);
            }

            foreach (var id in stale) _peers.Remove(id);
        }
    }
}
