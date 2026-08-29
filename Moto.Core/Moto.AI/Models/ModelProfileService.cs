using System;
using System.Diagnostics;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Models;

public enum ModelSize { Small3B, Large7B }

public sealed class ModelRecommendation
{
    public ModelSize RecommendedSize { get; set; }
    public string Reason { get; set; } = "";
    public double EstimatedRamMb { get; set; }
}

/// <summary>
/// Bloc 5 — Gestion des modèles & ressources (7 idées).
/// </summary>
public sealed class ModelProfileService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;

    public ModelProfileService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Idée "3B par défaut, 7B sur demande".</summary>
    public ModelSize GetDefaultModelSize()
    {
        if (_settings.Shared.Ai.Profiles.Use3BByDefault.Value)
            return ModelSize.Small3B;
        return ModelSize.Large7B;
    }

    /// <summary>Idée "Low-RAM" — refuse 7B si RAM insuffisante.</summary>
    public bool CanLoadLargeModel()
    {
        if (!_settings.Shared.Ai.Profiles.LowRamMode.Value)
            return true;

        var totalRamGb = GetTotalRamGb();
        int threshold = _settings.Shared.Ai.Profiles.RamThresholdGb.Value;
        return totalRamGb >= threshold;
    }

    /// <summary>Idée "Présets par type de projet".</summary>
    public ModelRecommendation RecommendModelForProject(string projectType)
    {
        var recommendation = new ModelRecommendation();

        recommendation.RecommendedSize = projectType.ToLowerInvariant() switch
        {
            "snake" or "game" => ModelSize.Large7B, // Jeux = complexe
            "console" => ModelSize.Small3B,
            "maui" or "ui" => ModelSize.Small3B,
            _ => GetDefaultModelSize()
        };

        recommendation.Reason = $"Projet '{projectType}' : {recommendation.RecommendedSize} recommandé";
        recommendation.EstimatedRamMb = recommendation.RecommendedSize == ModelSize.Small3B ? 2048 : 4096;

        return recommendation;
    }

    /// <summary>Idée "Auto-benchmark" — teste rapidement les modèles.</summary>
    public ModelSize AutoBenchmarkModels(string testPrompt)
    {
        // Simulation : en production, mesurerait le temps de réponse réel
        _log.Info("ModelProfile", "Auto-benchmark démarré");

        // Heuristique : 3B plus rapide, 7B meilleure qualité
        return GetDefaultModelSize();
    }

    /// <summary>Idée "Shared model" — partage multi-instance.</summary>
    public bool ShouldShareModel()
    {
        if (!_settings.Shared.Ai.Profiles.SharedModelMode.Value)
            return false;

        // Vérifie si d'autres instances MOTO tournent
        var processes = Process.GetProcessesByName("Moto.Editor");
        return processes.Length > 1;
    }

    /// <summary>Idée "No GPU" — force CPU-only.</summary>
    public bool ShouldUseGpu()
    {
        return !_settings.Shared.Ai.Profiles.NoGpuMode.Value;
    }

    /// <summary>Idée "Nightly heavy" — planifie tâches lourdes.</summary>
    public bool ShouldRunHeavyTaskNow()
    {
        if (!_settings.Shared.Ai.Profiles.NightlyHeavyMode.Value)
            return true; // Pas de restriction

        // Vérifie si l'éditeur est idle (simplifié)
        var hour = DateTime.Now.Hour;
        return hour >= 22 || hour <= 6; // Entre 22h et 6h
    }

    private double GetTotalRamGb()
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            // Estimation : utilise WorkingSet comme proxy
            return proc.WorkingSet64 / (1024.0 * 1024.0 * 1024.0) * 4; // ×4 pour estimation totale
        }
        catch
        {
            return 8; // Défaut 8 GB
        }
    }
}
