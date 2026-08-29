// Moto.Core/AI/Suggestions/DismissPersistenceEngine.cs
// Persistance des dismiss avec expiration (7 jours) et scoring dynamique.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.AI.Suggestions
{
    public sealed class DismissRecord
    {
        public string SuggestionId { get; init; } = string.Empty;
        public DateTime DismissedUtc { get; init; } = DateTime.UtcNow;
        public int DismissCount { get; set; } = 1;
    }

    /// <summary>
    /// Moteur de persistance des dismiss.
    /// - Stocke dans .moto/proactive-dismissed.json
    /// - Expiration après 7 jours
    /// - Scoring dynamique : plus une suggestion est dismissée, moins elle revient
    /// </summary>
    public sealed class DismissPersistenceEngine
    {
        private readonly string _dismissPath;
        private readonly List<DismissRecord> _dismissed = new();
        private readonly object _lock = new();
        private static readonly TimeSpan Expiration = TimeSpan.FromDays(7);

        public DismissPersistenceEngine(string workspaceRoot)
        {
            ArgumentNullException.ThrowIfNull(workspaceRoot);
            var motoDir = Path.Combine(workspaceRoot, ".moto");
            Directory.CreateDirectory(motoDir);
            _dismissPath = Path.Combine(motoDir, "proactive-dismissed.json");
            Load();
        }

        /// <summary>Marque une suggestion comme dismissée.</summary>
        public void Dismiss(string suggestionId)
        {
            lock (_lock)
            {
                var existing = _dismissed.FirstOrDefault(d => d.SuggestionId == suggestionId);
                if (existing != null)
                {
                    existing.DismissCount++;
                }
                else
                {
                    _dismissed.Add(new DismissRecord { SuggestionId = suggestionId });
                }
                Save();
            }
        }

        /// <summary>Vérifie si une suggestion est dismissée (non expirée).</summary>
        public bool IsDismissed(string suggestionId)
        {
            lock (_lock)
            {
                var record = _dismissed.FirstOrDefault(d => d.SuggestionId == suggestionId);
                if (record == null) return false;

                // Expiration : si > 7 jours, on considère comme non dismissé
                if (DateTime.UtcNow - record.DismissedUtc > Expiration)
                    return false;

                // Scoring dynamique : si dismissé > 3 fois, toujours dismissé
                return record.DismissCount >= 1;
            }
        }

        /// <summary>
        /// Retourne le score de suppression (0 = jamais dismissé, 1 = très dismissé).
        /// Utilisé pour le scoring dynamique des suggestions.
        /// </summary>
        public double GetDismissScore(string suggestionId)
        {
            lock (_lock)
            {
                var record = _dismissed.FirstOrDefault(d => d.SuggestionId == suggestionId);
                if (record == null) return 0.0;

                // Expiration
                if (DateTime.UtcNow - record.DismissedUtc > Expiration)
                    return 0.0;

                // Score : 1 - (1 / (count + 1)) → approche 1 asymptotiquement
                return 1.0 - (1.0 / (record.DismissCount + 1));
            }
        }

        /// <summary>Nettoie les dismiss expirés (> 7 jours).</summary>
        public void CleanupExpired()
        {
            lock (_lock)
            {
                _dismissed.RemoveAll(d => DateTime.UtcNow - d.DismissedUtc > Expiration);
                Save();
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_dismissPath)) return;
                var json = File.ReadAllText(_dismissPath);
                var loaded = JsonSerializer.Deserialize<List<DismissRecord>>(json);
                if (loaded != null)
                {
                    _dismissed.Clear();
                    _dismissed.AddRange(loaded);
                    CleanupExpired();
                }
            }
            catch { /* fichier corrompu : repart vide */ }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_dismissed, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(_dismissPath, json);
            }
            catch { /* best-effort */ }
        }
    }
}
