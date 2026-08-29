// Moto.Core/AI/Providers/AiProviderManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;

namespace Moto.Core.AI.Providers
{
    /// <summary>
    /// Gestionnaire central des providers IA.
    /// Gère le fallback automatique : local → Ollama → cloud.
    /// </summary>
    public class AiProviderManager
    {
        private readonly List<IAiProvider> _providers = new List<IAiProvider>();
        private readonly List<AiProviderConfig> _configs = new List<AiProviderConfig>();

        /// <summary>Déclenché quand un fallback se produit.</summary>
        public event Action<string, string> FallbackOccurred;

        /// <summary>Déclenché quand un provider tombe en panne.</summary>
        public event Action<string, string> ProviderFailed;

        public AiProviderManager()
        {
            // Enregistrer les providers par défaut.
            RegisterProvider(new OllamaProvider());
            RegisterProvider(new OpenAiProvider());
            RegisterProvider(new AnthropicProvider());
            RegisterProvider(new MistralProvider());
        }

        /// <summary>
        /// Enregistre un provider.
        /// </summary>
        public void RegisterProvider(IAiProvider provider)
        {
            if (!_providers.Any(p => p.Type == provider.Type))
            {
                _providers.Add(provider);
            }
        }

        /// <summary>
        /// Configure un provider.
        /// </summary>
        public void ConfigureProvider(AiProviderConfig config)
        {
            var existing = _configs.FirstOrDefault(c => c.Type == config.Type);

            if (existing != null)
            {
                _configs.Remove(existing);
            }

            _configs.Add(config);

            var provider = _providers.FirstOrDefault(p => p.Type == config.Type);
            provider?.Configure(config);
        }

        /// <summary>
        /// Récupère toutes les configurations.
        /// </summary>
        public List<AiProviderConfig> GetAllConfigs()
        {
            return _configs.ToList();
        }

        /// <summary>
        /// Récupère la configuration d'un type de provider.
        /// </summary>
        public AiProviderConfig GetConfig(AiProviderType type)
        {
            return _configs.FirstOrDefault(c => c.Type == type);
        }

        /// <summary>
        /// Vérifie la disponibilité de tous les providers.
        /// </summary>
        public async Task<Dictionary<AiProviderType, bool>> CheckAllProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<AiProviderType, bool>();

            foreach (var provider in _providers)
            {
                var config = _configs.FirstOrDefault(c => c.Type == provider.Type);

                if (config != null && config.IsEnabled)
                {
                    results[provider.Type] = await provider.HealthCheckAsync(cancellationToken);
                }
                else
                {
                    results[provider.Type] = false;
                }
            }

            return results;
        }

        /// <summary>
        /// Exécute une complétion avec fallback automatique.
        /// Essaie les providers dans l'ordre de priorité.
        /// </summary>
        public async Task<AiCompletionResult> CompleteWithFallbackAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            // Trier par priorité.
            var orderedConfigs = _configs
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.Priority)
                .ToList();

            // Toujours essayer le moteur interne en premier.
            var internalResult = await TryInternalEngineAsync(request, cancellationToken);

            if (internalResult.Success)
            {
                return internalResult;
            }

            // Essayer chaque provider externe dans l'ordre.
            foreach (var config in orderedConfigs)
            {
                var provider = _providers.FirstOrDefault(p => p.Type == config.Type);

                if (provider == null)
                {
                    continue;
                }

                try
                {
                    var result = await provider.CompleteAsync(request, cancellationToken);

                    if (result.Success)
                    {
                        return result;
                    }

                    ProviderFailed?.Invoke(provider.Name, result.Error);
                    FallbackOccurred?.Invoke(provider.Name, "Passage au provider suivant.");
                }
                catch (Exception ex)
                {
                    ProviderFailed?.Invoke(provider.Name, ex.Message);
                    FallbackOccurred?.Invoke(provider.Name, $"Exception: {ex.Message}");
                }
            }

            // Tous les providers ont échoué.
            return new AiCompletionResult
            {
                Success = false,
                ProviderName = "None",
                Error = "Tous les providers IA ont échoué. Vérifiez vos configurations."
            };
        }

        /// <summary>
        /// Génère du code avec fallback.
        /// </summary>
        public async Task<AiCompletionResult> GenerateCodeWithFallbackAsync(
            string prompt,
            string language,
            string existingCode,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un générateur de code {language}. Réponds uniquement avec le code.",
                Prompt = prompt,
                Context = existingCode,
                Temperature = 0.2
            };

            return await CompleteWithFallbackAsync(request, cancellationToken);
        }

        /// <summary>
        /// Corrige du code avec fallback.
        /// </summary>
        public async Task<AiCompletionResult> FixCodeWithFallbackAsync(
            string code,
            string errors,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                SystemPrompt = "Tu es un expert en correction de code.",
                Prompt = $"Corrige ce code :\n\n{code}\n\nErreurs :\n{errors}",
                Temperature = 0.1
            };

            return await CompleteWithFallbackAsync(request, cancellationToken);
        }

        /// <summary>
        /// Explique du code avec fallback.
        /// </summary>
        public async Task<AiCompletionResult> ExplainCodeWithFallbackAsync(
            string code,
            string language,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un professeur de programmation {language}.",
                Prompt = $"Explique ce code simplement :\n\n{code}",
                Temperature = 0.5
            };

            return await CompleteWithFallbackAsync(request, cancellationToken);
        }

        /// <summary>
        /// Essaie le moteur interne MOTO AI.
        /// </summary>
        private async Task<AiCompletionResult> TryInternalEngineAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Le moteur interne est toujours disponible.
                // Il utilise les règles et patterns locaux.
                var startTime = DateTime.UtcNow;

                // Simulation du moteur interne.
                // Dans la vraie implémentation, on appelle MotoAiKernel.
                await Task.Delay(1, cancellationToken);

                return new AiCompletionResult
                {
                    Success = false, // Le moteur interne ne gère pas tout.
                    ProviderName = "MOTO AI Internal",
                    Content = string.Empty,
                    Latency = DateTime.UtcNow - startTime
                };
            }
            catch
            {
                return new AiCompletionResult
                {
                    Success = false,
                    ProviderName = "MOTO AI Internal",
                    Error = "Moteur interne indisponible."
                };
            }
        }
    }
}
