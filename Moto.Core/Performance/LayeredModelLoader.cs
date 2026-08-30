using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Settings;
using Moto.Core.AI.Internal; // Pour l'interface ILogger existante

namespace Moto.Core.Performance;

public class LayeredModelLoader
{
    private readonly SemaphoreSlim _prefetchSemaphore;
    private readonly SettingsEngine _settings;
    private readonly ILogger _logger;

    public LayeredModelLoader(SettingsEngine settings, ILogger logger)
    {
        _settings = settings;
        _logger = logger;

        // Initialisation du sémaphore avec la valeur des settings (Backpressure)
        var maxConcurrent = SettingsCatalog.Ai.Advanced.MaxConcurrentPrefetch.Value;
        _prefetchSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        // Écoute des changements pour ajustement dynamique (si l'utilisateur modifie le setting à chaud)
        SettingsCatalog.Ai.Advanced.MaxConcurrentPrefetch.Changed += OnMaxConcurrentChanged;
    }

    private void OnMaxConcurrentChanged(object? sender, SettingChangedEventArgs<int> e)
    {
        _logger.Info($"[LayeredModelLoader] Ajustement backpressure demandé : {e.OldValue} -> {e.NewValue}");
        // Note : L'ajustement dynamique d'un SemaphoreSlim existant est complexe.
        // En production, on privilégiera un Channel<T> ou un redémarrage du pool de prefetch.
    }

    public async Task LoadLayerAsync(string layerName, CancellationToken ct = default)
    {
        _logger.Debug($"[LayeredModelLoader] Demande de chargement pour {layerName}. En attente de slot...");

        // BACKPRESSURE : Bloque si le nombre max de prefetch est atteint
        await _prefetchSemaphore.WaitAsync(ct);

        try
        {
            _logger.Debug($"[LayeredModelLoader] Slot acquis pour {layerName}.");
            await ExecuteLoadAsync(layerName, ct);
        }
        finally
        {
            _prefetchSemaphore.Release();
            _logger.Debug($"[LayeredModelLoader] Slot libéré pour {layerName}.");
        }
    }

    private async Task ExecuteLoadAsync(string layerName, CancellationToken ct)
    {
        // Logique existante de chargement (I/O, décompression, mapping mémoire)
        await Task.Delay(500, ct);
    }
}
