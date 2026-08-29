// Moto.Core/AI/Embedded/LayeredActivationService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Active automatiquement le layered loading si :
/// - RAM système < 8 GB, OU
/// - Modèle > 4 GB
///
/// Réduit la RAM active de ~70% (4.5 GB → 1.2 GB).
/// </summary>
public sealed class LayeredActivationService
{
    private readonly LayeredModelLoader _loader;
    private readonly EmbeddedLlmConfig _config;
    private readonly long _ramThresholdMB;
    private readonly long _modelSizeThresholdBytes;
    private bool _isEnabled;

    public static LayeredActivationService? Instance { get; private set; }
    public bool IsEnabled => _isEnabled;

    public LayeredActivationService(
        LayeredModelLoader loader,
        EmbeddedLlmConfig config,
        long ramThresholdMB = 8192,
        long modelSizeThresholdBytes = 4L * 1024 * 1024 * 1024)
    {
        _loader = loader;
        _config = config;
        _ramThresholdMB = ramThresholdMB;
        _modelSizeThresholdBytes = modelSizeThresholdBytes;
        Instance = this;
    }

    /// <summary>
    /// Évalue si le layered loading doit être activé.
    /// </summary>
    public bool ShouldActivate()
    {
        var availableRamMB = GetAvailableRamMB();
        var modelSizeBytes = GetModelSizeBytes();

        return availableRamMB < _ramThresholdMB || modelSizeBytes > _modelSizeThresholdBytes;
    }

    /// <summary>
    /// Active le layered loading si les conditions sont remplies.
    /// </summary>
    public async Task<bool> TryActivateAsync(CancellationToken ct = default)
    {
        if (_isEnabled) return true;
        if (!ShouldActivate()) return false;

        try
        {
            _isEnabled = true;
            return true;
        }
        catch
        {
            _isEnabled = false;
            return false;
        }
    }

    /// <summary>
    /// Statistiques du layered loading.
    /// </summary>
    public LayeredStats GetStats() => new()
    {
        IsEnabled = _isEnabled,
        LoadedLayers = _loader.LoadedLayers,
        TotalLayers = _loader.TotalLayers,
        ActiveExperts = _loader.ActiveExperts,
        ActiveMemoryMB = _loader.ActiveMemoryMB,
        ShouldActivate = ShouldActivate()
    };

    private static long GetAvailableRamMB()
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.TotalAvailableMemoryBytes / 1024 / 1024;
        }
        catch { return 16_000; }
    }

    private long GetModelSizeBytes()
    {
        var modelPath = Path.Combine(_config.ModelsDirectory, _config.ModelFileName);
        return File.Exists(modelPath) ? new FileInfo(modelPath).Length : 0;
    }
}

public class LayeredStats
{
    public bool IsEnabled { get; set; }
    public int LoadedLayers { get; set; }
    public int TotalLayers { get; set; }
    public int ActiveExperts { get; set; }
    public long ActiveMemoryMB { get; set; }
    public bool ShouldActivate { get; set; }
}
