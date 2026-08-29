// Moto.Core/AI/Embedded/KvCacheManager.cs
using System;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// KV Cache compressé avec eviction LRU pour réduire la mémoire
/// pendant les sessions d'inférence longues.
/// </summary>
public sealed class KvCacheManager
{
    private readonly ConcurrentDictionary<string, CacheBlock> _cache = new();
    private readonly long _maxSizeBytes;
    private long _currentSizeBytes;

    public static KvCacheManager? Instance { get; private set; }
    public int EntryCount => _cache.Count;
    public long CurrentSizeMB => _currentSizeBytes / 1024 / 1024;

    public KvCacheManager(long maxSizeMB = 512)
    {
        _maxSizeBytes = maxSizeMB * 1024 * 1024;
        Instance = this;
    }

    /// <summary>
    /// Stocke un bloc KV compressé.
    /// </summary>
    public void Put(string promptPrefix, byte[] kvData)
    {
        var key = ComputeKey(promptPrefix);
        var compressed = Compress(kvData);

        var block = new CacheBlock
        {
            Key = key,
            Data = compressed,
            SizeBytes = compressed.Length,
            LastAccess = DateTime.UtcNow,
            HitCount = 0
        };

        // Eviction LRU si nécessaire
        while (_currentSizeBytes + block.SizeBytes > _maxSizeBytes && _cache.Count > 0)
        {
            EvictLru();
        }

        if (_cache.TryAdd(key, block))
        {
            Interlocked.Add(ref _currentSizeBytes, block.SizeBytes);
        }
    }

    /// <summary>
    /// Récupère un bloc KV (décompressé). Retourne null si absent.
    /// </summary>
    public byte[]? Get(string promptPrefix)
    {
        var key = ComputeKey(promptPrefix);
        if (_cache.TryGetValue(key, out var block))
        {
            block.LastAccess = DateTime.UtcNow;
            block.HitCount++;
            return Decompress(block.Data);
        }
        return null;
    }

    /// <summary>
    /// Vide le cache complètement (appelé lors du unload du modèle).
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _currentSizeBytes, 0);
    }

    private void EvictLru()
    {
        var oldest = _cache.Values
            .OrderBy(b => b.LastAccess)
            .FirstOrDefault();

        if (oldest != null && _cache.TryRemove(oldest.Key, out _))
        {
            Interlocked.Add(ref _currentSizeBytes, -oldest.SizeBytes);
        }
    }

    private static string ComputeKey(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "")[..16];
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new System.IO.MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest))
        {
            brotli.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new System.IO.MemoryStream(data);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new System.IO.MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}

internal class CacheBlock
{
    public string Key { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public long SizeBytes { get; set; }
    public DateTime LastAccess { get; set; }
    public int HitCount { get; set; }
}
