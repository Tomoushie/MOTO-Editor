// Moto.Core/AI/Models/AiProviderConfig.cs
using System;
using System.Collections.Generic;

namespace Moto.Core.AI.Models
{
    /// <summary>
    /// Type de provider IA supporté.
    /// </summary>
    public enum AiProviderType
    {
        LocalInternal,  // Moteur interne MOTO AI (toujours disponible)
        Ollama,         // Modèles locaux via Ollama
        OpenAI,         // API OpenAI (GPT-4, etc.)
        Anthropic,      // API Anthropic (Claude, etc.)
        Mistral,        // API Mistral
        DeepSeek,       // API DeepSeek
        Custom          // Provider personnalisé
    }

    /// <summary>
    /// Priorité de fallback.
    /// Plus le numéro est bas, plus le provider est prioritaire.
    /// </summary>
    public enum FallbackPriority
    {
        Primary = 0,
        Secondary = 1,
        Tertiary = 2,
        LastResort = 3
    }

    /// <summary>
    /// Configuration d'un provider IA.
    /// </summary>
    public class AiProviderConfig
    {
        /// <summary>Identifiant unique du provider.</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Type de provider.</summary>
        public AiProviderType Type { get; set; } = AiProviderType.LocalInternal;

        /// <summary>Nom affiché dans l'UI.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Clé API (chiffrée en stockage).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>URL de l'endpoint API.</summary>
        public string EndpointUrl { get; set; } = string.Empty;

        /// <summary>Nom du modèle à utiliser.</summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>Priorité de fallback.</summary>
        public FallbackPriority Priority { get; set; } = FallbackPriority.LastResort;

        /// <summary>Le provider est-il activé ?</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Timeout en millisecondes.</summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>Température pour la génération (0.0 à 2.0).</summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>Nombre max de tokens en sortie.</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>Headers HTTP supplémentaires.</summary>
        public Dictionary<string, string> CustomHeaders { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Crée une configuration par défaut pour Ollama.
        /// </summary>
        public static AiProviderConfig DefaultOllama()
        {
            return new AiProviderConfig
            {
                Type = AiProviderType.Ollama,
                DisplayName = "Ollama (local)",
                EndpointUrl = "http://127.0.0.1:11434",
                ModelName = "qwen2.5-coder:7b",
                Priority = FallbackPriority.Primary,
                TimeoutMs = 60000,
                Temperature = 0.3,
                MaxTokens = 8192
            };
        }

        /// <summary>
        /// Crée une configuration par défaut pour OpenAI.
        /// </summary>
        public static AiProviderConfig DefaultOpenAI()
        {
            return new AiProviderConfig
            {
                Type = AiProviderType.OpenAI,
                DisplayName = "OpenAI",
                EndpointUrl = "https://api.openai.com/v1",
                ModelName = "gpt-4o",
                Priority = FallbackPriority.Secondary,
                TimeoutMs = 30000,
                Temperature = 0.7,
                MaxTokens = 4096
            };
        }

        /// <summary>
        /// Crée une configuration par défaut pour Anthropic.
        /// </summary>
        public static AiProviderConfig DefaultAnthropic()
        {
            return new AiProviderConfig
            {
                Type = AiProviderType.Anthropic,
                DisplayName = "Anthropic (Claude)",
                EndpointUrl = "https://api.anthropic.com/v1",
                ModelName = "claude-sonnet-4-20250514",
                Priority = FallbackPriority.Secondary,
                TimeoutMs = 30000,
                Temperature = 0.7,
                MaxTokens = 8192
            };
        }

        /// <summary>
        /// Crée une configuration par défaut pour Mistral.
        /// </summary>
        public static AiProviderConfig DefaultMistral()
        {
            return new AiProviderConfig
            {
                Type = AiProviderType.Mistral,
                DisplayName = "Mistral AI",
                EndpointUrl = "https://api.mistral.ai/v1",
                ModelName = "mistral-large-latest",
                Priority = FallbackPriority.Tertiary,
                TimeoutMs = 30000,
                Temperature = 0.7,
                MaxTokens = 4096
            };
        }
    }

    /// <summary>
    /// Résultat d'une requête IA.
    /// </summary>
    public class AiCompletionResult
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public TimeSpan Latency { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Requête envoyée à un provider IA.
    /// </summary>
    public class AiCompletionRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 4096;
        public bool Stream { get; set; } = false;
    }
}
