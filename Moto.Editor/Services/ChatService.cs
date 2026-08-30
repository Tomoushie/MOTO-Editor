// Moto.Editor/Services/ChatService.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;
using Moto.Core.AI;
using Moto.Editor.Models;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Service de chat IA côté éditeur : gère les threads de conversation affichés
    /// dans le panneau chat, et route les questions vers Ollama (via MotoAiKernel)
    /// ou le FallbackEngine (providers externes configurés).
    /// </summary>
    public class ChatService
    {
        private readonly FallbackEngine _fallback;
        private readonly MotoAiKernel _kernel;

        /// <summary>Fournit le texte actuellement sélectionné dans l'éditeur (pour le contexte).</summary>
        public Func<string>? SelectionProvider { get; set; }

        /// <summary>Mode de l'IA (Beginner/Expert) pour le routage interne.</summary>
        public AiMode Mode { get; set; } = AiMode.Beginner;

        /// <summary>Racine du workspace courant (mise à jour à l'ouverture d'un dossier).</summary>
        public string WorkspaceRoot { get; set; }

        /// <summary>Threads de conversation (le plus récent en premier).</summary>
        public ObservableCollection<ChatThread> Threads { get; } = new();

        public ChatThread? CurrentThread => Threads.FirstOrDefault();

        public ChatService(string workspaceRoot, FallbackEngine? fallback, MotoAiKernel? kernel, ILogger<ChatService>? logger = null)
        {
            WorkspaceRoot = workspaceRoot ?? string.Empty;
            _fallback = fallback ?? new FallbackEngine();
            _kernel = kernel ?? new MotoAiKernel(workspaceRoot);
        }

        private ChatThread EnsureThread()
        {
            var thread = Threads.FirstOrDefault();
            if (thread is null)
            {
                thread = new ChatThread();
                Threads.Insert(0, thread);
            }
            return thread;
        }

        /// <summary>Envoie un message texte simple dans le thread courant (sans code attaché).</summary>
        public async Task SendAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var thread = EnsureThread();
            thread.Messages.Add(new ChatMessage { Role = "user", Content = text });
            thread.LastActivityUtc = DateTime.UtcNow;

            var selection = SelectionProvider?.Invoke() ?? string.Empty;
            var prompt = string.IsNullOrWhiteSpace(selection) ? text : $"{text}\n\nSélection :\n{selection}";

            var response = await RouteAsync(prompt);
            thread.Messages.Add(new ChatMessage { Role = "ai", Content = response });
            thread.LastActivityUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Envoie un prompt avec le code courant au modèle choisi,
        /// pour modification en direct depuis le bandeau IA.
        /// </summary>
        public async Task<string> AskWithCodeAsync(string model, string prompt, string code)
        {
            var fullPrompt =
                "Tu es MOTO AI, un assistant de développement.\n" +
                $"Demande : {prompt}\n\n" +
                "Code actuel :\n" + code + "\n\n" +
                "Réponds avec le code COMPLET modifié dans un bloc ``` , sans explication.";

            return await RouteAsync(fullPrompt);
        }

        /// <summary>Route un prompt vers Ollama (MotoAiKernel) puis, en repli, vers le FallbackEngine.</summary>
        private async Task<string> RouteAsync(string prompt)
        {
            var kernelResponse = await _kernel.RouteAsync(prompt, ct: default);
            if (kernelResponse is { Success: true } && !string.IsNullOrWhiteSpace(kernelResponse.Content))
                return kernelResponse.Content;

            var fallbackResult = await _fallback.GenerateAsync(prompt, WorkspaceRoot);
            return fallbackResult.Success
                ? fallbackResult.Content
                : "Aucun moteur IA disponible (Ollama et fallback injoignables). Vérifie tes paramètres IA.";
        }
    }
}
