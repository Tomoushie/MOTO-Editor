using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace Moto.InferenceHost.Services;

/// <summary>
/// Registre des modèles ONNX chargés dans le processus isolé.
/// Supporte le déchargement à chaud (mode éco).
/// </summary>
public sealed class ModelRegistry : IDisposable
{
    private readonly ILogger<ModelRegistry> _logger;
    private readonly Dictionary<string, InferenceSession> _sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ModelRegistry(ILogger<ModelRegistry> logger) => _logger = logger;

    public async Task<object> LoadModelAsync(string modelId, string? tier, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_sessions.ContainsKey(modelId))
                return new { status = "already_loaded", model_id = modelId };

            var path = GetModelPath(modelId, tier);
            if (!File.Exists(path))
                return new { error = $"Fichier modèle introuvable: {path}" };

            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2)
            };

            var session = new InferenceSession(path, options);
            _sessions[modelId] = session;

            _logger.LogInformation("Modèle {ModelId} chargé (tier: {Tier})", modelId, tier ?? "default");
            return new { status = "loaded", model_id = modelId, tier };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<object> UnloadModelAsync(string modelId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_sessions.TryGetValue(modelId, out var session))
            {
                session.Dispose();
                _sessions.Remove(modelId);
                _logger.LogInformation("Modèle {ModelId} déchargé.", modelId);
                return new { status = "unloaded", model_id = modelId };
            }
            return new { status = "not_loaded", model_id = modelId };
        }
        finally
        {
            _lock.Release();
        }
    }

    public InferenceSession? GetSession(string modelId)
        => _sessions.GetValueOrDefault(modelId);

    public object GetStatus()
        => new
        {
            loaded_models = _sessions.Keys.ToList(),
            process_id = Environment.ProcessId,
            working_set_mb = Environment.WorkingSet / 1024 / 1024
        };

    private static string GetModelPath(string modelId, string? tier)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "Models");

        var fileName = tier is null
            ? $"{modelId}.onnx"
            : $"{modelId}-{tier.ToLowerInvariant()}.onnx";

        return Path.Combine(baseDir, fileName);
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
            session.Dispose();
        _sessions.Clear();
        _lock.Dispose();
    }
}
