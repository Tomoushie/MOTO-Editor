// Moto.Core/AI/Providers/IAiProvider.cs
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;

namespace Moto.Core.AI.Providers
{
    /// <summary>
    /// Contrat pour tous les providers IA.
    /// Chaque provider (Ollama, OpenAI, Anthropic, etc.) implémente cette interface.
    /// </summary>
    public interface IAiProvider
    {
        /// <summary>Nom du provider.</summary>
        string Name { get; }

        /// <summary>Type de provider.</summary>
        AiProviderType Type { get; }

        /// <summary>Le provider est-il disponible ?</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Initialise le provider avec sa configuration.
        /// </summary>
        void Configure(AiProviderConfig config);

        /// <summary>
        /// Vérifie si le provider est accessible.
        /// </summary>
        Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Génère une complétion.
        /// </summary>
        Task<AiCompletionResult> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Génère du code spécifiquement.
        /// </summary>
        Task<AiCompletionResult> GenerateCodeAsync(
            string prompt,
            string language,
            string existingCode,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Explique du code.
        /// </summary>
        Task<AiCompletionResult> ExplainCodeAsync(
            string code,
            string language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrige du code.
        /// </summary>
        Task<AiCompletionResult> FixCodeAsync(
            string code,
            string errors,
            CancellationToken cancellationToken = default);
    }
}
