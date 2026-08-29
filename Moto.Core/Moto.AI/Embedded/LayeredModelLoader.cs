// Mise à jour de Moto.Core/AI/Embedded/LayeredModelLoader.cs

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

public class LayeredModelLoader
{
    /// <summary>
    /// Charge une couche spécifique en RAM de manière asynchrone.
    /// </summary>
    public async Task LoadLayerAsync(int layerIndex, CancellationToken ct = default)
{
    if (layerIndex < 0 || layerIndex >= _config.TotalLayers)
        throw new ArgumentOutOfRangeException(nameof(layerIndex));

    lock (_lock) { if (_layers[layerIndex].IsLoaded) return; }

    if (LoadedLayers >= _config.MaxLayersInMemory)
        await EvictLeastRecentlyUsedAsync(ct);

    // Lecture asynchrone pour éviter le blocage I/O
    var offset = layerIndex * _config.LayerSizeBytes;
    var buffer = await ReadLayerFromDiskAsync(offset, _config.LayerSizeBytes, ct);

    lock (_lock)
    {
        _layers[layerIndex] = new LayerState
        {
            Index = layerIndex,
            Data = buffer,
            IsLoaded = true,
            LastAccess = DateTime.UtcNow,
            AccessCount = 0
        };
    }

    OnLayerEvent?.Invoke(new LayerEvent { Type = LayerEventType.Loaded, LayerIndex = layerIndex, ActiveMemoryMB = ActiveMemoryMB });

    // ★ Préfetch heuristique : charger la couche suivante en arrière-plan si elle n'est pas chargée
    _ = PrefetchNextLayerAsync(layerIndex + 1, ct);
}

private async Task<byte[]> ReadLayerFromDiskAsync(long offset, int length, CancellationToken ct)
{
    var path = ModelPaths.GetModelPath(_config.ModelFileName); // Utilisation du chemin centralisé
    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
    fs.Seek(offset, SeekOrigin.Begin);
    var buffer = new byte[length];
    var read = await fs.ReadAsync(buffer, 0, length, ct);
    if (read < length) Array.Resize(ref buffer, read);
    return buffer;
}

private async Task PrefetchNextLayerAsync(int nextLayerIndex, CancellationToken ct)
{
    if (nextLayerIndex >= 0 && nextLayerIndex < _config.TotalLayers)
    {
        lock (_lock) { if (_layers[nextLayerIndex].IsLoaded) return; }
        // On ne bloque pas, on laisse le Task.Run gérer le prefetch en arrière-plan
        await Task.Run(async () => {
            try { await LoadLayerAsync(nextLayerIndex, ct); } catch { /* Ignorer les erreurs de prefetch */ }
        }, ct);
    }
}
}
