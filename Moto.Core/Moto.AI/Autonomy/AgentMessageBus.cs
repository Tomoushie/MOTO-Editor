// Moto.Core/AI/Autonomy/AgentMessageBus.cs
// ★ AJOUT (03/09, jalon 2 — "agents autonomes en tâche de fond"). Pub/sub en
// mémoire, un seul process (aucun IPC nécessaire) : chaque BackgroundAgentLoop
// y poste ses propositions/décisions/résultats (pour la détection de conflit)
// ET les vrais messages qu'un agent adresse à un autre (action SendMessage).
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Moto.Core.AI.Autonomy
{
    public enum AgentMessageKind
    {
        /// <summary>Un vrai message qu'un agent adresse à un autre (ou à tous) —
        /// le seul type que BuildPrompt fait remonter dans le contexte d'UN AUTRE
        /// agent (voir BackgroundAgentLoop).</summary>
        Note,
        /// <summary>Comptabilité interne : posté juste avant de demander une
        /// confirmation, pour que le prochain agent qui toucherait le même
        /// chemin puisse le voir. Jamais montré comme un "message" à un agent.</summary>
        Proposal,
        /// <summary>Comptabilité interne : posté après l'exécution (ou le refus)
        /// d'une action mutante — même rôle que Proposal pour la détection de
        /// conflit, côté "c'est fait" plutôt que "je vais le faire".</summary>
        Outcome
    }

    public sealed class AgentMessage
    {
        public string FromAgentId { get; init; } = string.Empty;

        /// <summary>Null = adressé à tous les agents (broadcast).</summary>
        public string? ToAgentId { get; init; }

        public AgentMessageKind Kind { get; init; } = AgentMessageKind.Note;
        public string Text { get; init; } = string.Empty;

        /// <summary>Chemin de fichier concerné, si ce message représente une
        /// proposition ou un résultat touchant un fichier précis — null pour un
        /// Note "libre" ou une action sans fichier (ex. RunCommand). Permet la
        /// détection de conflit SANS reparser Text.</summary>
        public string? TouchedPath { get; init; }

        public DateTime SentUtc { get; init; } = DateTime.UtcNow;
    }

    public sealed class AgentMessageBus
    {
        // Même plafond que ChatService.Tasks (30) — ni vidé ni persistant entre
        // sessions, juste assez pour la détection de conflit et l'historique
        // affiché pendant qu'un run est en cours.
        private const int MaxHistory = 30;

        public ObservableCollection<AgentMessage> History { get; } = new();

        public event Action<AgentMessage>? MessagePosted;

        public void Post(AgentMessage message)
        {
            History.Add(message);
            while (History.Count > MaxHistory) History.RemoveAt(0);
            MessagePosted?.Invoke(message);
        }

        /// <summary>
        /// Le message le plus récent d'un AUTRE agent (Proposal ou Outcome) ayant
        /// touché CE chemin, avant l'instant donné — utilisé pour avertir
        /// l'humain d'un conflit potentiel dans la demande de confirmation,
        /// plutôt que de compter sur la chance du minutage.
        /// </summary>
        public AgentMessage? FindRecentTouch(string agentId, string path, DateTime beforeUtc)
        {
            for (var i = History.Count - 1; i >= 0; i--)
            {
                var m = History[i];
                if (m.SentUtc >= beforeUtc) continue;
                if (m.FromAgentId == agentId) continue;
                if (m.TouchedPath != null && string.Equals(m.TouchedPath, path, StringComparison.OrdinalIgnoreCase))
                    return m;
            }
            return null;
        }

        /// <summary>Vrais messages (Note) adressés à `agentId` ou à tous, envoyés
        /// par un AUTRE agent après `sinceUtc` — c'est ce que BuildPrompt fait
        /// remonter au prochain tour d'un agent.</summary>
        public System.Collections.Generic.IReadOnlyList<AgentMessage> NotesSince(string agentId, DateTime sinceUtc) =>
            History.Where(m =>
                    m.Kind == AgentMessageKind.Note &&
                    m.SentUtc > sinceUtc &&
                    m.FromAgentId != agentId &&
                    (m.ToAgentId == null || m.ToAgentId == agentId))
                .ToList();
    }
}
