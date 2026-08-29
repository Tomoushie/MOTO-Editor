// Moto.Core/Collab/CrdtSelectionSync.cs
// CRDT multi-curseurs avancé : sélection partagée entre utilisateurs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.Collab
{
    public sealed class SelectionRange
    {
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }

        public bool IsEmpty => StartLine == EndLine && StartColumn == EndColumn;
    }

    public sealed class UserSelection
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Color { get; init; } = "#D97757";
        public List<SelectionRange> Selections { get; set; } = new();
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Synchronisation CRDT des sélections multi-utilisateurs.
    /// Gère : multi-curseurs, sélections partagées, couleurs par utilisateur.
    /// </summary>
    public sealed class CrdtSelectionSync
    {
        private readonly Dictionary<string, UserSelection> _selections = new();
        private readonly object _lock = new();
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        public event Action<IReadOnlyList<UserSelection>>? SelectionsChanged;

        /// <summary>
        /// Met à jour la sélection d'un utilisateur.
        /// </summary>
        public void UpdateSelection(string userId, string displayName, List<SelectionRange> selections)
        {
            lock (_lock)
            {
                _selections[userId] = new UserSelection
                {
                    UserId = userId,
                    DisplayName = displayName,
                    Color = GenerateColorForUser(userId),
                    Selections = selections,
                    LastUpdateUtc = DateTime.UtcNow
                };

                CleanupExpired();
                SelectionsChanged?.Invoke(GetActiveSelections());
            }
        }

        /// <summary>
        /// Supprime la sélection d'un utilisateur (déconnexion).
        /// </summary>
        public void RemoveSelection(string userId)
        {
            lock (_lock)
            {
                _selections.Remove(userId);
                SelectionsChanged?.Invoke(GetActiveSelections());
            }
        }

        /// <summary>
        /// Retourne toutes les sélections actives.
        /// </summary>
        public IReadOnlyList<UserSelection> GetActiveSelections()
        {
            lock (_lock)
            {
                CleanupExpired();
                return _selections.Values.ToList();
            }
        }

        /// <summary>
        /// Vérifie si une position est dans la sélection d'un autre utilisateur.
        /// </summary>
        public bool IsPositionInRemoteSelection(int line, int column, string currentUserId)
        {
            lock (_lock)
            {
                foreach (var selection in _selections.Values)
                {
                    if (selection.UserId == currentUserId) continue;

                    foreach (var range in selection.Selections)
                    {
                        if (IsPositionInRange(line, column, range))
                            return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Sérialise l'état pour la synchronisation réseau.
        /// </summary>
        public string SerializeState()
        {
            lock (_lock)
            {
                return JsonSerializer.Serialize(_selections.Values.ToList());
            }
        }

        /// <summary>
        /// Désérialise l'état reçu du réseau.
        /// </summary>
        public void DeserializeState(string json)
        {
            lock (_lock)
            {
                var selections = JsonSerializer.Deserialize<List<UserSelection>>(json);
                if (selections != null)
                {
                    foreach (var selection in selections)
                    {
                        _selections[selection.UserId] = selection;
                    }
                    SelectionsChanged?.Invoke(GetActiveSelections());
                }
            }
        }

        private static bool IsPositionInRange(int line, int column, SelectionRange range)
        {
            if (line < range.StartLine || line > range.EndLine)
                return false;

            if (line == range.StartLine && column < range.StartColumn)
                return false;

            if (line == range.EndLine && column > range.EndColumn)
                return false;

            return true;
        }

        private static string GenerateColorForUser(string userId)
        {
            // Génère une couleur déterministe basée sur l'ID utilisateur
            var hash = userId.GetHashCode();
            var hue = Math.Abs(hash) % 360;
            return $"hsl({hue}, 70%, 60%)";
        }

        private void CleanupExpired()
        {
            var expired = _selections.Values
                .Where(s => DateTime.UtcNow - s.LastUpdateUtc > Timeout)
                .Select(s => s.UserId)
                .ToList();

            foreach (var userId in expired)
                _selections.Remove(userId);
        }
    }
}
