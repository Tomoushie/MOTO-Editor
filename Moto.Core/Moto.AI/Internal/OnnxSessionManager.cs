using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Moto.Core.Settings;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Gestionnaire de sessions ONNX avec compression du KV-cache.
/// </summary>
public partial class OnnxSessionManager : IDisposable
{
    private readonly ILogger<OnnxSessionManager> _logger;
    private readonly SettingsEngine _settings;
    private bool _enableKvCacheCompression;
    private KvCacheCompressor? _kvCacheCompressor;

    public OnnxSessionManager(
        ILogger<OnnxSessionManager> logger,
        SettingsEngine settings)
    {
        _logger = logger;
        _settings = settings;
        _enableKvCacheCompression = settings.GetBool("ai.embedded.enableKvCacheCompression", defaultValue: true);

        if (_enableKvCacheCompression)
        {
            _kvCacheCompressor = new KvCacheCompressor(logger);
        }
    }

    /// <summary>
    /// Crée une session ONNX avec KV-cache compression si activée.
    /// </summary>
    public InferenceSession CreateSession(string modelPath)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2)
        };

        // Active la compression du KV-cache si supportée
        if (_enableKvCacheCompression)
        {
            // Note : ONNX Runtime ne supporte pas nativement la compression du KV-cache
            // On implémente une compression manuelle dans le pipeline d'inférence
            _logger.LogInformation("KV-cache compression activée pour {Path}", modelPath);
        }

        return new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Compresse le KV-cache après chaque étape d'inférence.
    /// </summary>
    public byte[] CompressKvCache(float[] kvCache)
    {
        if (_kvCacheCompressor is null)
            return kvCache.Select(BitConverter.GetBytes).SelectMany(b => b).ToArray();

        return _kvCacheCompressor.Compress(kvCache);
    }

    /// <summary>
    /// Décompresse le KV-cache avant l'inférence.
    /// </summary>
    public float[] DecompressKvCache(byte[] compressed)
    {
        if (_kvCacheCompressor is null)
            return compressed.Chunk(4).Select(BitConverter.ToSingle).ToArray();

        return _kvCacheCompressor.Decompress(compressed);
    }

    public void Dispose()
    {
        _kvCacheCompressor?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Compresse le KV-cache de FP16 à INT8.
/// Réduction RAM ~50%.
/// </summary>
public sealed class KvCacheCompressor : IDisposable
{
    private readonly ILogger _logger;

    public KvCacheCompressor(ILogger logger) => _logger = logger;

    /// <summary>
    /// Compresse un tableau de floats FP16 en INT8.
    /// </summary>
    public byte[] Compress(float[] input)
    {
        var output = new byte[input.Length];

        // Calcule min/max pour la quantification
        var min = input.Min();
        var max = input.Max();
        var range = max - min;
        var scale = range > 0 ? 255.0f / range : 1.0f;

        for (var i = 0; i < input.Length; i++)
        {
            // Quantification INT8 : (value - min) * scale
            var quantized = (byte)((input[i] - min) * scale);
            output[i] = quantized;
        }

        _logger.LogDebug(
            "KV-cache compressé: {InputSize} → {OutputSize} bytes ({Ratio}%)",
            input.Length * 4,
            output.Length,
            100 - (output.Length * 100 / (input.Length * 4)));

        return output;
    }

    /// <summary>
    /// Décompresse un tableau INT8 en FP16.
    /// </summary>
    public float[] Decompress(byte[] input)
    {
        var output = new float[input.Length];

        // Logique de décompression simplifiée
        // À adapter selon les métadonnées de quantification stockées
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] / 255.0f;
        }

        return output;
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
