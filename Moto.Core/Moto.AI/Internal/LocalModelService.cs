// Moto.Core/AI/Internal/LocalModelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;
using Moto.Core.AI.Providers;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Service de pilotage des modèles locaux.
    /// Gère le contexte, les tokens et le fallback automatique.
    /// </summary>
    public sealed class LocalModelService : IDisposable
    {
        private readonly AiProviderManager _providerManager;
        private readonly List<ChatMessage> _contextWindow = new List<ChatMessage>();
        private readonly int _maxContextTokens;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public LocalModelService(AiProviderManager? providerManager = null)
        {
            _providerManager = providerManager ?? new AiProviderManager();
            _maxContextTokens = SettingsEngine.Shared.GetInt("ai_local_max_context_tokens", 8192);
        }

        /// <summary>
        /// Représente un message dans l'historique de la session.
        /// </summary>
        public class ChatMessage
        {
            public string Role { get; set; } = "user";
            public string Content { get; set; } = string.Empty;
            public int EstimatedTokens { get; set; }
        }

        /// <summary>
        /// Génère une réponse en utilisant le meilleur provider local disponible.
        /// </summary>
        public async Task<AiCompletionResult> GenerateAsync(
            string prompt,
            string? systemPrompt = null,
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                // 1. Mise à jour du contexte
                AddToContext("user", prompt);

                // 2. Construction de la requête avec le contexte glissant
                var request = new AiCompletionRequest
                {
                    Prompt = BuildContextualPrompt(),
                    SystemPrompt = systemPrompt ?? "Tu es MOTO AI, l'intelligence interne de MOTO Editor.",
                    Temperature = SettingsEngine.Shared.GetDouble("ai_local_temperature", 0.3),
                    MaxTokens = SettingsEngine.Shared.GetInt("ai_local_max_tokens", 4096)
                };

                // 3. Exécution avec fallback automatique
                var result = await _providerManager.CompleteWithFallbackAsync(request, ct);

                // 4. Ajout de la réponse au contexte si succès
                if (result.Success)
                {
                    AddToContext("assistant", result.Content);
                }

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Ajoute un message au contexte et nettoie les plus anciens si la limite est atteinte.
        /// </summary>
        private void AddToContext(string role, string content)
        {
            var tokens = EstimateTokens(content);
            _contextWindow.Add(new ChatMessage { Role = role, Content = content, EstimatedTokens = tokens });

            // Nettoyage du contexte (fenêtre glissante)
            int currentTotal = _contextWindow.Sum(m => m.EstimatedTokens);
            while (currentTotal > _maxContextTokens && _contextWindow.Count > 1)
            {
                currentTotal -= _contextWindow[0].EstimatedTokens;
                _contextWindow.RemoveAt(0);
            }
        }

        private string BuildContextualPrompt()
        {
            if (_contextWindow.Count <= 1)
                return _contextWindow.LastOrDefault()?.Content ?? string.Empty;

            var history = string.Join("\n", _contextWindow.Select(m => $"[{m.Role}]: {m.Content}"));
            return $"{history}\n[assistant]:";
        }

        /// <summary>
        /// Estimation simple des tokens (1 token ≈ 4 caractères en anglais, 3 en FR).
        /// </summary>
        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length / 3;
        }

        public void ClearContext()
        {
            _contextWindow.Clear();
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
