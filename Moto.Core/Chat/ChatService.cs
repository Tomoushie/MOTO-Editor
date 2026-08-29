// Moto.Core/Chat/ChatService.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Moto.Core.Chat
{
    /// <summary>
    /// Représente un message individuel dans un thread de conversation.
    /// </summary>
    public sealed record ChatMessage(
        string Id,
        string Role,      // "user", "assistant", "system"
        string Content,
        DateTime TimestampUtc
    );

    /// <summary>
    /// Représente un thread de conversation complet.
    /// </summary>
    public sealed class ChatThread
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
        public IReadOnlyList<ChatMessage> Messages { get; init; } = Array.Empty<ChatMessage>();
    }

    /// <summary>
    /// Service central de gestion des conversations et de l'historique des threads.
    /// </summary>
    public partial class ChatService
    {
        private readonly ObservableCollection<ChatThread> _threads = new();
        private readonly object _lock = new();

        /// <summary>
        /// Threads accessibles en lecture seule.
        /// Utilisé par RefreshHomeStats() et par la Sidebar pour lister l'historique.
        /// </summary>
        public IReadOnlyList<ChatThread> Threads
        {
            get
            {
                lock (_lock)
                {
                    return _threads.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>Événement notifié à chaque ajout/modification/suppression de thread.</summary>
        public event EventHandler? ThreadsChanged;

        /// <summary>Ajoute un nouveau thread à la collection.</summary>
        public void AddThread(ChatThread thread)
        {
            lock (_lock)
            {
                _threads.Add(thread);
            }
            ThreadsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Met à jour un thread existant (ex: après l'ajout d'un message).</summary>
        public void UpdateThread(ChatThread updatedThread)
        {
            lock (_lock)
            {
                var index = _threads.FindIndex(t => t.Id == updatedThread.Id);
                if (index >= 0)
                {
                    _threads[index] = updatedThread;
                }
            }
            ThreadsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Supprime un thread par son identifiant.</summary>
        public void RemoveThread(string threadId)
        {
            lock (_lock)
            {
                var thread = _threads.FirstOrDefault(t => t.Id == threadId);
                if (thread != null)
                {
                    _threads.Remove(thread);
                }
            }
            ThreadsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Récupère un thread spécifique par son ID.</summary>
        public ChatThread? GetThread(string threadId)
        {
            lock (_lock)
            {
                return _threads.FirstOrDefault(t => t.Id == threadId);
            }
        }
    }
}
