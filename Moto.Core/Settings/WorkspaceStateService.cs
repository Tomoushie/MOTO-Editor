// Moto.Core/Settings/WorkspaceStateService.cs — MÉTHODES À AJOUTER
// Ajoute la persistance de l'ordre des sessions dans chaque section.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Core.Settings
{
    // ── À ajouter dans la classe WorkspaceStateService existante ──
    public partial class WorkspaceStateService
    {
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

        // ── Champ privé à ajouter ──
        private readonly Dictionary<SessionSection, List<string>> _sessionOrder = new();

        // ── Modification de Load() pour charger l'ordre ──
        // Dans la méthode Load(), après le chargement des sessions :
        // if (loaded.Order != null) _sessionOrder = loaded.Order;

        // ── Modification de Save() pour sauvegarder l'ordre ──
        // Dans la méthode Save(), ajouter Order = _sessionOrder dans le JSON
    }
}
