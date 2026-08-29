// Moto.Core/AI/Internal/HybridAiRouter.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Embedded;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Routeur IA hybride : choisit automatiquement entre Ollama et moteur embarqué.
/// Priorité : Ollama (si installé) → Embedded (fallback).
/// </summary>
public sealed class HybridAiRouter
{
    private readonly OllamaClient? _ollama;
    private readonly EmbeddedLlmEngine _embedded;
    private readonly AiRouterConfig _config;

    public static HybridAiRouter? Instance { get; private set; }
    public AiProvider CurrentProvider { get; private set; } = AiProvider.None;

    public HybridAiRouter(OllamaClient? ollama, EmbeddedLlmEngine embedded, AiRouterConfig config)
    {
        _ollama = ollama;
        _embedded = embedded;
        _config = config;
        Instance = this;
    }

    /// <summary>
    /// Détecte le meilleur provider disponible.
    /// </summary>
    public async Task<AiProvider> DetectBestProviderAsync(CancellationToken ct = default)
    {
        // 1. Ollama prioritaire si activé et disponible
        if (_config.PreferOllama && _ollama != null)
        {
            try
            {
                if (await _ollama.IsAvailableAsync(ct))
                {
                    CurrentProvider = AiProvider.Ollama;
                    return AiProvider.Ollama;
                }
            }
            catch { /* Ollama indisponible, fallback */ }
        }

        // 2. Embedded si modèle téléchargé
        if (_config.AllowEmbedded)
        {
            var modelPath = Path.Combine(_embedded.ModelName, ""); // À adapter
            if (File.Exists(modelPath) || await TryLoadEmbeddedAsync(ct))
            {
                CurrentProvider = AiProvider.Embedded;
                return AiProvider.Embedded;
            }
        }

        CurrentProvider = AiProvider.None;
        return AiProvider.None;
    }

    /// <summary>
    /// Génère une réponse via le meilleur provider.
    /// </summary>
    public async Task<string> GenerateAsync(string prompt, AiTaskType taskType, CancellationToken ct = default)
    {
        var provider = await DetectBestProviderAsync(ct);

        return provider switch
        {
            AiProvider.Ollama => await _ollama!.GenerateAsync(prompt, ct),
            AiProvider.Embedded => await _embedded.GenerateAsync(prompt, ct: ct),
            _ => throw new InvalidOperationException("Aucun provider IA disponible. Installez Ollama ou téléchargez le modèle embarqué.")
        };
    }

    /// <summary>
    /// Génère du code via le meilleur provider.
    /// </summary>
    public async Task<string> GenerateCodeAsync(string instruction, string? context, CancellationToken ct = default)
    {
        var provider = await DetectBestProviderAsync(ct);

        return provider switch
        {
            AiProvider.Ollama => await _ollama!.GenerateCodeAsync(instruction, context, ct),
            AiProvider.Embedded => await _embedded.GenerateCodeAsync(instruction, context, ct),
            _ => throw new InvalidOperationException("Aucun provider IA disponible.")
        };
    }

    /// <summary>
    /// Complétion de code (infill).
    /// </summary>
    public async Task<string> CompleteCodeAsync(string prefix, string suffix, CancellationToken ct = default)
    {
        var provider = await DetectBestProviderAsync(ct);

        return provider switch
        {
            AiProvider.Ollama => await _ollama!.CompleteCodeAsync(prefix, suffix, ct),
            AiProvider.Embedded => await _embedded.CompleteCodeAsync(prefix, suffix, ct),
            _ => string.Empty // Silent fallback
        };
    }

    private async Task<bool> TryLoadEmbeddedAsync(CancellationToken ct)
    {
        try
        {
            await _embedded.LoadAsync(ct);
            return _embedded.IsAvailable;
        }
        catch
        {
            return false;
        }
    }
}

public class AiRouterConfig
{
    public bool PreferOllama { get; set; } = true;
    public bool AllowEmbedded { get; set; } = true;
    public bool AutoDownloadModel { get; set; } = true;
}

public enum AiProvider
{
    None,
    Ollama,
    Embedded
}

public enum AiTaskType
{
    Chat,
    CodeGeneration,
    CodeCompletion,
    Refactoring,
    Explanation
}
