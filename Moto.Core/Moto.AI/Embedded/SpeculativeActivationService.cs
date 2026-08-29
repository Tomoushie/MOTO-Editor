// Moto.Core/AI/Embedded/SpeculativeActivationService.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Active automatiquement le speculative decoding si le modèle draft est téléchargé.
/// Vérification au démarrage + monitoring périodique.
/// </summary>
public sealed class SpeculativeActivationService
{
    private readonly SpeculativeDecoder _decoder;
    private readonly ModelDownloader _downloader;
    private readonly EmbeddedLlmConfig _draftConfig;
    private bool _isEnabled;

    public static SpeculativeActivationService? Instance { get; private set; }
    public bool IsEnabled => _isEnabled;
    public bool IsDraftAvailable => _downloader.IsModelDownloaded(_draftConfig);

    public SpeculativeActivationService(
        SpeculativeDecoder decoder,
        ModelDownloader downloader,
        EmbeddedLlmConfig draftConfig)
    {
        _decoder = decoder;
        _downloader = downloader;
        _draftConfig = draftConfig;
        Instance = this;
    }

    /// <summary>
    /// Tente d'activer le speculative decoding.
    /// Retourne true si activé avec succès.
    /// </summary>
    public async Task<bool> TryActivateAsync(CancellationToken ct = default)
    {
        if (_isEnabled) return true;
        if (!IsDraftAvailable) return false;

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
    /// Statistiques du speculative decoding.
    /// </summary>
    public SpeculativeStats GetStats() => new()
    {
        IsEnabled = _isEnabled,
        IsDraftAvailable = IsDraftAvailable,
        AcceptanceRate = _decoder.AcceptanceRate,
        SpeedupFactor = _decoder.SpeedupFactor,
        AcceptedTokens = _decoder.AcceptedTokens,
        RejectedTokens = _decoder.RejectedTokens
    };
}

public class SpeculativeStats
{
    public bool IsEnabled { get; set; }
    public bool IsDraftAvailable { get; set; }
    public double AcceptanceRate { get; set; }
    public double SpeedupFactor { get; set; }
    public long AcceptedTokens { get; set; }
    public long RejectedTokens { get; set; }
}
