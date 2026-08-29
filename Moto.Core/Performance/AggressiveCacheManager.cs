using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Moto.Core.Performance;

/// <summary>
/// Cache multi-niveaux agressif : RAM + disque persistant.
/// Remplace AiCacheEngine existant en plus complet.
/// </summary>
public sealed class AggressiveCacheManager
{
    private readonly ConcurrentDictionary<string, CacheEntry> _ram = new();
    private readonly string _diskPath;
    private readonly long _maxRamBytes;
    private long _currentRamBytes;

    public static AggressiveCacheManager Instance { get; private set; } = null!;

    public AggressiveCacheManager(string cacheFolder, long maxRamBytes = 512 * 1024 * 1024) // 512 MB RAM
    {
        _diskPath = cacheFolder;
        _maxRamBytes = maxRamBytes;
        Directory.CreateDirectory(_diskPath);
        Instance = this;

        // Purge périodique
        _ = Task.Run(PurgeLoopAsync);
    }

    public async Task<T?> GetOrComputeAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        ttl ??= TimeSpan.FromDays(7);

        // 1. RAM hit
        if (_ram.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            entry.LastAccess = DateTime.UtcNow;
            return (T)entry.Value;
        }

        // 2. Disk hit
        var diskFile = GetDiskPath(key);
        if (File.Exists(diskFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(diskFile);
                var diskEntry = JsonSerializer.Deserialize<CacheEntry>(json);
                if (diskEntry != null && !diskEntry.IsExpired)
                {
                    AddToRam(key, diskEntry);
                    return (T)diskEntry.Value;
                }
            }
            catch { /* corrupt file, ignore */ }
        }

        // 3. Compute
        var value = await factory();
        var newEntry = new CacheEntry
        {
            Key = key,
            Value = value!,
            ExpiresAt = DateTime.UtcNow + ttl.Value,
            LastAccess = DateTime.UtcNow,
            SizeBytes = EstimateSize(value)
        };

        AddToRam(key, newEntry);
        _ = PersistToDiskAsync(key, newEntry);

        return value;
    }

    public void Invalidate(string keyPattern)
    {
        var keys = _ram.Keys.Where(k => k.Contains(keyPattern)).ToList();
        foreach (var key in keys)
        {
            _ram.TryRemove(key, out _);
            var diskFile = GetDiskPath(key);
            if (File.Exists(diskFile)) File.Delete(diskFile);
        }
    }

    public CacheStats GetStats() => new()
    {
        RamEntries = _ram.Count,
        RamBytes = _currentRamBytes,
        DiskFiles = Directory.Exists(_diskPath) ? Directory.GetFiles(_diskPath).Length : 0
    };

    private void AddToRam(string key, CacheEntry entry)
    {
        _ram[key] = entry;
        Interlocked.Add(ref _currentRamBytes, entry.SizeBytes);
        EvictIfNeeded();
    }

    private void EvictIfNeeded()
    {
        while (_currentRamBytes > _maxRamBytes && _ram.Count > 0)
        {
            var oldest = _ram.Values
                .OrderBy(e => e.LastAccess)
                .FirstOrDefault();
            if (oldest == null) break;

            _ram.TryRemove(oldest.Key, out _);
            Interlocked.Add(ref _currentRamBytes, -oldest.SizeBytes);
        }
    }

    private string GetDiskPath(string key)
    {
        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
        return Path.Combine(_diskPath, $"{hash}.cache");
    }

    private async Task PersistToDiskAsync(string key, CacheEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry);
            await File.WriteAllTextAsync(GetDiskPath(key), json);
        }
        catch { /* disk full, ignore */ }
    }

    private async Task PurgeLoopAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1));
            var now = DateTime.UtcNow;
            var expired = _ram.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
            foreach (var key in expired)
            {
                _ram.TryRemove(key, out _);
                var diskFile = GetDiskPath(key);
                if (File.Exists(diskFile)) File.Delete(diskFile);
            }
        }
    }

    private static long EstimateSize<T>(T value)
    {
        if (value == null) return 0;
        try { return JsonSerializer.SerializeToUtf8Bytes(value).Length; }
        catch { return 1024; }
    }
}

public class CacheEntry
{
    public string Key { get; set; } = "";
    public object Value { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastAccess { get; set; }
    public long SizeBytes { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

public class CacheStats
{
    public int RamEntries { get; set; }
    public long RamBytes { get; set; }
    public int DiskFiles { get; set; }
}
