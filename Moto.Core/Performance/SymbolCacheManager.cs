// Moto.Core/Performance/SymbolCacheManager.cs
using System.IO.Compression;
using System.Text.Json;

namespace Moto.Core.Performance;

/// <summary>
/// Stocke les symboles/AST compressés sur disque pour réouverture rapide.
/// </summary>
public sealed class SymbolCacheManager
{
    private readonly string _cacheDir;

    public static SymbolCacheManager Instance { get; private set; } = null!;

    public SymbolCacheManager(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(_cacheDir);
        Instance = this;
    }

    /// <summary>
    /// Sauvegarde les symboles d'un fichier (compressé).
    /// </summary>
    public async Task SaveAsync(string filePath, List<SymbolInfo> symbols)
    {
        var cacheFile = GetCachePath(filePath);
        var json = JsonSerializer.Serialize(symbols);
        var compressed = Compress(Encoding.UTF8.GetBytes(json));
        await File.WriteAllBytesAsync(cacheFile, compressed);
    }

    /// <summary>
    /// Charge les symboles depuis le cache (si existant).
    /// </summary>
    public async Task<List<SymbolInfo>?> LoadAsync(string filePath)
    {
        var cacheFile = GetCachePath(filePath);
        if (!File.Exists(cacheFile)) return null;

        try
        {
            var compressed = await File.ReadAllBytesAsync(cacheFile);
            var json = Encoding.UTF8.GetString(Decompress(compressed));
            return JsonSerializer.Deserialize<List<SymbolInfo>>(json);
        }
        catch
        {
            return null;
        }
    }

    private string GetCachePath(string filePath)
    {
        var hash = filePath.GetHashCode().ToString("X");
        return Path.Combine(_cacheDir, $"{hash}.symcache");
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
