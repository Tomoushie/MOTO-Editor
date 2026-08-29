// Moto.Core/Performance/PerFileOnDemandService.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Performance;

/// <summary>
/// Item 103 — Service on-demand per-file.
/// Crée/résout les services par fichier uniquement quand demandé (lazy).
/// Compatible avec LazyFileLoader (LRU) existant.
/// </summary>
public sealed class PerFileOnDemandService
{
    private readonly StructuredLogCollector _log;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
    private readonly ConcurrentDictionary<string, object?> _cache = new();

    public PerFileOnDemandService(StructuredLogCollector log) => _log = log;

    /// <summary>Charge/produit une ressource pour un fichier, une seule fois, à la demande.</summary>
    public async Task<T?> GetOrCreateAsync<T>(string filePath, Func<string, CancellationToken, Task<T?>> factory, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(filePath, out var cached) && cached is T typed)
            return typed;

        var gate = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Double-check après acquisition du verrou
            if (_cache.TryGetValue(filePath, out cached) && cached is T typed2)
                return typed2;

            var value = await factory(filePath, ct);
            _cache[filePath] = value;
            _log.Debug("PerFileOnDemand", "Ressource créée à la demande", new { filePath });
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Invalide un fichier (ex: fermeture d'onglet) pour libérer la mémoire.</summary>
    public void Invalidate(string filePath)
    {
        _cache.TryRemove(filePath, out _);
        _log.Debug("PerFileOnDemand", "Cache invalidé", new { filePath });
    }

    public void ClearAll() => _cache.Clear();
}
