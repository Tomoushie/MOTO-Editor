// Moto.Core/AI/Embedded/SmartModelManager.cs
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Gestionnaire intelligent du modèle LLM.
/// Combine : sélection de tier, compression, isolation, déchargement auto.
/// </summary>
public sealed class SmartModelManager : IDisposable
{
    private readonly ModelCompressionService _compression;
    private readonly IsolatedInferenceHost _host;
    private readonly ModelDownloader _downloader;
    private ModelTier _currentTier;
    private bool _initialized;

    public static SmartModelManager? Instance { get; private set; }
    public ModelTier CurrentTier => _currentTier;
    public bool IsReady => _initialized && _host.IsModelLoaded;
    public long HostMemoryMB => _host.ProcessMemoryMB;

    public SmartModelManager(
        ModelCompressionService compression,
        IsolatedInferenceHost host,
        ModelDownloader downloader)
    {
        _compression = compression;
        _host = host;
        _downloader = downloader;
        Instance = this;
    }

    /// <summary>
    /// Initialise le système : détecte la RAM, choisit le tier, télécharge si nécessaire.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        // 1. Détecte la RAM disponible
        var availableRamMB = GetAvailableRamMB();
        _currentTier = _compression.SelectTier(availableRamMB);

        Console.WriteLine($"[SmartModelManager] RAM disponible: {availableRamMB} MB → Tier: {_currentTier}");

        // 2. Vérifie si le modèle est téléchargé
        var spec = ModelTierConfig.Specs[_currentTier];
        if (!_downloader.IsModelDownloaded(new EmbeddedLlmConfig
            { ModelFileName = spec.FileName, ModelsDirectory = GetModelsDir() }))
        {
            // Téléchargement auto (optionnel, peut être désactivé)
            // await _downloader.DownloadAsync(...);
        }

        // 3. Démarre le processus hôte isolé
        await _host.StartHostAsync(_currentTier, ct);

        _initialized = true;
    }

    /// <summary>
    /// Génère une réponse via le modèle isolé.
    /// </summary>
    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        if (!_initialized) await InitializeAsync(ct);
        return await _host.GenerateAsync(prompt, ct: ct);
    }

    /// <summary>
    /// Change de tier (par exemple, si l'utilisateur change les settings).
    /// </summary>
    public async Task SwitchTierAsync(ModelTier newTier, CancellationToken ct = default)
    {
        if (newTier == _currentTier) return;

        await _host.UnloadModelAsync();
        await _host.StopHostAsync();

        _currentTier = newTier;
        await _host.StartHostAsync(newTier, ct);
    }

    /// <summary>
    /// Libère la mémoire sans arrêter l'éditeur.
    /// </summary>
    public async Task ReleaseMemoryAsync()
    {
        await _host.UnloadModelAsync();
    }

    private static long GetAvailableRamMB()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
            return totalRam;
        }
        catch
        {
            return 8_000; // Fallback conservateur
        }
    }

    private static string GetModelsDir()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "models");
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
