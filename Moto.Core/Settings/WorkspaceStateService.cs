// Moto.Core/Settings/WorkspaceStateService.cs
// Persistance de l'état des sessions (section pinned/projects/recent + ordre).
// Le coeur de la classe manquait (seules des "méthodes à ajouter" existaient) : reconstruit ici.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Settings
{
    /// <summary>Section d'affichage d'une session sur la page d'accueil.</summary>
    public enum SessionSection
    {
        Recent,
        Pinned,
        Projects
    }

    public partial class WorkspaceStateService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _statePath;
        private Dictionary<string, SessionSection> _sectionAssignments = new();
        private Dictionary<SessionSection, List<string>> _sessionOrder = new();

        public WorkspaceStateService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "workspace-state.json"))
        {
        }

        public WorkspaceStateService(string statePath)
        {
            _statePath = statePath;
            Load();
        }

        /// <summary>Surcharge DI : dérive le fichier d'état depuis la racine du workspace ouvert.</summary>
        public WorkspaceStateService(string workspaceRoot, ILogger<WorkspaceStateService>? logger)
            : this(string.IsNullOrWhiteSpace(workspaceRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MotoEditor", "workspace-state.json")
                : Path.Combine(workspaceRoot, ".moto", "workspace-state.json"))
        {
        }

        /// <summary>Assigne une session à une section (pinned/projects/recent) et persiste.</summary>
        public async Task SetSessionSectionAsync(string sessionId, SessionSection section)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _sectionAssignments[sessionId] = section;
                Save();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Retourne la section d'une session (Recent par défaut si jamais assignée).</summary>
        public SessionSection GetSessionSection(string sessionId)
            => _sectionAssignments.TryGetValue(sessionId, out var section) ? section : SessionSection.Recent;

        /// <summary>
        /// Sauvegarde l'ordre des sessions dans une section donnée.
        /// </summary>
        public async Task SaveSessionOrderAsync(SessionSection section, IReadOnlyList<string> sessionIds)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _sessionOrder[section] = new List<string>(sessionIds);
                Save();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Charge l'ordre des sessions pour une section donnée.
        /// Retourne une liste vide si aucun ordre n'est sauvegardé.
        /// </summary>
        public IReadOnlyList<string> LoadSessionOrder(SessionSection section)
        {
            if (_sessionOrder.TryGetValue(section, out var order))
                return order.AsReadOnly();
            return Array.Empty<string>();
        }

        private sealed class PersistedState
        {
            public Dictionary<string, SessionSection> Sections { get; set; } = new();
            public Dictionary<SessionSection, List<string>> Order { get; set; } = new();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    var json = File.ReadAllText(_statePath);
                    var loaded = JsonSerializer.Deserialize<PersistedState>(json);
                    if (loaded != null)
                    {
                        _sectionAssignments = loaded.Sections ?? new();
                        _sessionOrder = loaded.Order ?? new();
                    }
                }
            }
            catch
            {
                // Fichier corrompu ou absent : on repart d'un état vide.
                _sectionAssignments = new();
                _sessionOrder = new();
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_statePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var state = new PersistedState { Sections = _sectionAssignments, Order = _sessionOrder };
                File.WriteAllText(_statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Sauvegarde impossible : ignoré, ne doit jamais empêcher l'application de fonctionner.
            }
        }
    }
}
