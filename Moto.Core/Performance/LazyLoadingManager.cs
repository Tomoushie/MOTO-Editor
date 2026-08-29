using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Moto.Core.Performance;

/// <summary>
/// Gère le lazy-loading de tous les services lourds.
/// Aucun moteur lourd n'est instancié au démarrage.
/// </summary>
public sealed class LazyLoadingManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, Lazy<object>> _cache = new();
    private readonly ConcurrentDictionary<string, LoadStats> _stats = new();

    public static LazyLoadingManager Instance { get; private set; } = null!;

    public LazyLoadingManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Instance = this;
    }

    /// <summary>
    /// Récupère un service lourd à la demande (thread-safe, singleton par clé).
    /// </summary>
    public T Get<T>(string key, Func<T> factory) where T : class
    {
        var lazy = _cache.GetOrAdd(key, _ => new Lazy<object>(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var instance = factory();
            sw.Stop();

            _stats.AddOrUpdate(key,
                new LoadStats { LoadTimeMs = sw.ElapsedMilliseconds, LoadedAt = DateTime.UtcNow },
                (_, existing) => existing);

            return instance!;
        }));

        return (T)lazy.Value;
    }

    /// <summary>
    /// Décharge un service lourd (libère la mémoire).
    /// </summary>
    public bool Unload(string key)
    {
        if (_cache.TryRemove(key, out var lazy))
        {
            if (lazy.IsValueCreated && lazy.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Décharge tous les services non utilisés depuis X minutes.
    /// </summary>
    public int GarbageCollect(TimeSpan idleThreshold)
    {
        var cutoff = DateTime.UtcNow - idleThreshold;
        var toUnload = _stats
            .Where(kvp => kvp.Value.LoadedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toUnload)
        {
            Unload(key);
        }

        return toUnload.Count;
    }

    public bool IsLoaded(string key) => _cache.TryGetValue(key, out var lazy) && lazy.IsValueCreated;

    public IReadOnlyDictionary<string, LoadStats> GetStats() => _stats;

    public int LoadedCount => _cache.Count(kvp => kvp.Value.IsValueCreated);
}

public class LoadStats
{
    public long LoadTimeMs { get; set; }
    public DateTime LoadedAt { get; set; }
}
