// Moto.Core/Collab/CrdtCursorRenderer.cs
// Rendu des curseurs distants pour l'intégration UI.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.Collab
{
    /// <summary>
    /// Représente un curseur distant à rendre dans l'éditeur.
    /// </summary>
    public sealed class RemoteCursorView
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Color { get; init; } = "#D97757";
        public string DocumentPath { get; init; } = string.Empty;
        public int Line { get; init; }
        public int Column { get; init; }
        public DateTime LastSeenUtc { get; init; }
        public string? SelectedText { get; init; }

        /// <summary>Retourne true si le curseur est actif (vu dans les 30 dernières secondes).</summary>
        public bool IsActive => DateTime.UtcNow - LastSeenUtc < TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Service de rendu des curseurs distants.
    /// Filtre les curseurs par document et gère l'expiration.
    /// </summary>
    public sealed class CrdtCursorRenderer
    {
        private readonly Dictionary<string, RemoteCursorView> _cursors = new();
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        /// <summary>Déclenché quand la liste des curseurs change.</summary>
        public event Action<IReadOnlyList<RemoteCursorView>>? CursorsChanged;

        /// <summary>Met à jour ou ajoute un curseur distant.</summary>
        public void UpdateCursor(RemoteCursorView cursor)
        {
            _cursors[cursor.UserId] = cursor;
            CleanupExpired();
            CursorsChanged?.Invoke(GetCursorsForDocument(cursor.DocumentPath));
        }

        /// <summary>Supprime un curseur (utilisateur déconnecté).</summary>
        public void RemoveCursor(string userId)
        {
            if (_cursors.Remove(userId))
                CursorsChanged?.Invoke(_cursors.Values.ToList());
        }

        /// <summary>Retourne les curseurs pour un document donné.</summary>
        public IReadOnlyList<RemoteCursorView> GetCursorsForDocument(string documentPath)
        {
            CleanupExpired();
            return _cursors.Values
                .Where(c => c.DocumentPath == documentPath && c.IsActive)
                .ToList();
        }

        /// <summary>Retourne tous les curseurs actifs.</summary>
        public IReadOnlyList<RemoteCursorView> GetAllActiveCursors()
        {
            CleanupExpired();
            return _cursors.Values.Where(c => c.IsActive).ToList();
        }

        private void CleanupExpired()
        {
            var expired = _cursors.Values
                .Where(c => DateTime.UtcNow - c.LastSeenUtc > _timeout)
                .Select(c => c.UserId)
                .ToList();

            foreach (var userId in expired)
                _cursors.Remove(userId);
        }
    }
}
