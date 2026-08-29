// Moto.Core/AI/FallbackEngine.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;
using Moto.Core.AI.Providers;
using Moto.Core.Security;

namespace Moto.Core.AI
{
    /// <summary>
    /// Moteur de fallback central.
    /// Connecte le pipeline XENO-SSS∞ aux providers IA externes.
    /// </summary>
    public class FallbackEngine
    {
        private readonly AiProviderManager _providerManager;
        private readonly SecureTokenStore _tokenStore;

        /// <summary>Déclenché quand un fallback se produit.</summary>
        public event Action<string> FallbackTriggered;

        public FallbackEngine()
        {
            _providerManager = new AiProviderManager();
            _tokenStore = new SecureTokenStore();

            // Charger les tokens sauvegardés.
            LoadSavedTokens();
        }

        /// <summary>
        /// Gestionnaire de providers.
        /// </summary>
        public AiProviderManager ProviderManager => _providerManager;

        /// <summary>
        /// Stockage sécurisé.
        /// </summary>
        public SecureTokenStore TokenStore => _tokenStore;

        /// <summary>
        /// Sauvegarde la clé API d'un provider.
        /// </summary>
        public void SaveApiKey(AiProviderType providerType, string apiKey)
        {
            var providerName = providerType.ToString();
            _tokenStore.SaveToken(providerName, apiKey);

            // Mettre à jour la config du provider.
            var config = _providerManager.GetConfig(providerType);

            if (config != null)
            {
                config.ApiKey = apiKey;
                _providerManager.ConfigureProvider(config);
            }
        }

        /// <summary>
        /// Récupère la clé API d'un provider.
        /// </summary>
        public string GetApiKey(AiProviderType providerType)
        {
            var providerName = providerType.ToString();
            return _tokenStore.GetToken(providerName);
        }

        /// <summary>
        /// Vérifie si un provider a une clé API configurée.
        /// </summary>
        public bool HasApiKey(AiProviderType providerType)
        {
            return _tokenStore.HasToken(providerType.ToString());
        }

        /// <summary>
        /// Exécute une génération avec fallback complet.
        /// C'est la méthode principale appelée par le pipeline XENO.
        /// </summary>
        public async Task<AiCompletionResult> GenerateAsync(
            string prompt,
            string context = null,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                Prompt = prompt,
                Context = context,
                SystemPrompt = "Tu es MOTO AI, un assistant de développement intégré à MOTO Editor.",
                Temperature = 0.3,
                MaxTokens = 8192
            };

            var result = await _providerManager.CompleteWithFallbackAsync(request, cancellationToken);

            if (!result.Success)
            {
                FallbackTriggered?.Invoke(result.Error);
            }

            return result;
        }

        /// <summary>
        /// Génère du code avec fallback.
        /// </summary>
        public async Task<AiCompletionResult> GenerateCodeAsync(
            string prompt,
            string language,
            string existingCode,
            CancellationToken cancellationToken = default)
        {
            return await _providerManager.GenerateCodeWithFallbackAsync(
                prompt, language, existingCode, cancellationToken);
        }

        /// <summary>
        /// Corrige du code avec fallback.
        /// </summary>
        public async Task<AiCompletionResult> FixCodeAsync(
            string code,
            string errors,
            CancellationToken cancellationToken = default)
        {
            return await _providerManager.FixCodeWithFallbackAsync(code, errors, cancellationToken);
        }

        /// <summary>
        /// Explique du code avec fallback.
        /// </summary>
        public async Task<AiCompletionResult> ExplainCodeAsync(
            string code,
            string language,
            CancellationToken cancellationToken = default)
        {
            return await _providerManager.ExplainCodeWithFallbackAsync(code, language, cancellationToken);
        }

        /// <summary>
        /// Charge les tokens sauvegardés dans les providers.
        /// </summary>
        private void LoadSavedTokens()
        {
            foreach (AiProviderType type in Enum.GetValues(typeof(AiProviderType)))
            {
                if (type == AiProviderType.LocalInternal || type == AiProviderType.Custom)
                {
                    continue;
                }

                var token = _tokenStore.GetToken(type.ToString());

                if (!string.IsNullOrWhiteSpace(token))
                {
                    var config = _providerManager.GetConfig(type);

                    if (config != null)
                    {
                        config.ApiKey = token;
                        _providerManager.ConfigureProvider(config);
                    }
                }
            }
        }
    }
}
