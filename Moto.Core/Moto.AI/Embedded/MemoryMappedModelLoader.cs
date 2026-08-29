// Moto.Core/AI/Embedded/MemoryMappedModelLoader.cs
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Charge les modèles via memory-mapped files (mmap).
/// Réduit l'empreinte RAM : l'OS page le modèle à la demande.
/// Le modèle n'est jamais entièrement en RAM.
/// </summary>
public sealed class MemoryMappedModelLoader : IDisposable
{
    private MemoryMappedFile? _mmap;
    private MemoryMappedViewAccessor? _accessor;
    private string? _loadedPath;

    public static MemoryMappedModelLoader? Instance { get; private set; }
    public bool IsLoaded => _mmap != null;
    public string? LoadedPath => _loadedPath;

    public MemoryMappedModelLoader()
    {
        Instance = this;
    }

    /// <summary>
    /// Mappe un modèle en mémoire via mmap.
    /// L'OS charge uniquement les pages accédées.
    /// </summary>
    public async Task MapModelAsync(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Modèle introuvable: {modelPath}");

        await Task.Run(() =>
        {
            var fileInfo = new FileInfo(modelPath);
            _mmap = MemoryMappedFile.CreateFromFile(
                modelPath,
                FileMode.Open,
                mapName: null,          // Pas de nom = pas de partage inter-processus
                capacity: 0,            // 0 = taille du fichier
                MemoryMappedFileAccess.Read);

            _accessor = _mmap.CreateViewAccessor(0, fileInfo.Length, MemoryMappedFileAccess.Read);
            _loadedPath = modelPath;
        });
    }

    /// <summary>
    /// Lit un segment du modèle (page à la demande).
    /// </summary>
    public byte[] ReadSegment(long offset, int length)
    {
        if (_accessor == null) throw new InvalidOperationException("Aucun modèle mappé");

        var buffer = new byte[length];
        _accessor.ReadArray(offset, buffer, 0, length);
        return buffer;
    }

    /// <summary>
    /// Libère le mapping (l'OS dépage automatiquement).
    /// </summary>
    public void Unmap()
    {
        _accessor?.Dispose();
        _accessor = null;
        _mmap?.Dispose();
        _mmap = null;
        _loadedPath = null;
    }

    public void Dispose() => Unmap();
}
