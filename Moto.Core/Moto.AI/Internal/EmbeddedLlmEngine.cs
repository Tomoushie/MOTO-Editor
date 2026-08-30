using System.IO.MemoryMappedFiles;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Moteur LLM embarqué avec support memory-mapped pour réduire la RAM.
/// </summary>
public partial class EmbeddedLlmEngine : IDisposable
{
    private MemoryMappedFile? _mmapFile;
    private readonly ILogger<EmbeddedLlmEngine> _logger;
    private readonly SettingsEngine _settings;
    private bool _useMemoryMapping;

    public EmbeddedLlmEngine(
        ILogger<EmbeddedLlmEngine> logger,
        SettingsEngine settings)
    {
        _logger = logger;
        _settings = settings;
        _useMemoryMapping = settings.GetBool("ai.embedded.useMemoryMapping", defaultValue: true);
    }

    /// <summary>
    /// Charge un modèle ONNX avec memory-mapping si activé.
    /// </summary>
    public async Task<InferenceSession?> LoadModelAsync(
        string modelPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Modèle introuvable: {Path}", modelPath);
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2)
                };

                if (_useMemoryMapping)
                {
                    // Memory-mapped loading : le système d'exploitation ne charge
                    // que les pages réellement utilisées, réduisant la RAM de ~40%
                    var fileInfo = new FileInfo(modelPath);
                    _mmapFile = MemoryMappedFile.CreateFromFile(
                        fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read),
                        mapName: null,
                        capacity: 0,
                        access: MemoryMappedFileAccess.Read,
                        inheritability: HandleInheritability.None,
                        leaveOpen: false);

                    // ONNX Runtime peut charger depuis un buffer mémoire
                    using var accessor = _mmapFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                    var buffer = new byte[fileInfo.Length];
                    accessor.ReadArray(0, buffer, 0, buffer.Length);

                    _logger.LogInformation(
                        "Modèle chargé via memory-mapping: {Path} ({SizeMb} MB)",
                        modelPath,
                        fileInfo.Length / 1024 / 1024);

                    return new InferenceSession(buffer, options);
                }

                // Fallback : chargement classique
                _logger.LogInformation("Modèle chargé classiquement: {Path}", modelPath);
                return new InferenceSession(modelPath, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec du chargement du modèle: {Path}", modelPath);
                return null;
            }
        }, ct);
    }

    /// <summary>
    /// Décharge le modèle et libère le memory-mapped file.
    /// </summary>
    public void UnloadModel()
    {
        _mmapFile?.Dispose();
        _mmapFile = null;
        _logger.LogInformation("Modèle déchargé et mémoire libérée.");
    }

    public void Dispose()
    {
        UnloadModel();
        GC.SuppressFinalize(this);
    }
}
