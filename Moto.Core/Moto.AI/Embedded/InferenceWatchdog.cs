// Mise à jour de Moto.Core/AI/Embedded/InferenceWatchdog.cs

public enum WatchdogState { Healthy, Degrading, CircuitOpen }

public sealed class InferenceWatchdog : IDisposable
{
    // ... (code existant)
    private WatchdogState _state = WatchdogState.Healthy;
    private const int MaxCrashesBeforeCircuitBreaker = 3;

    public WatchdogState State => _state;
    public event Action<WatchdogState, string>? OnStateChanged;

    private void HealthCheck(object? state)
    {
        try
        {
            if (!_host.IsRunning)
            {
                _crashCount++;
                _consecutiveFailures++;

                if (_crashCount >= MaxCrashesBeforeCircuitBreaker)
                {
                    _state = WatchdogState.CircuitOpen;
                    OnStateChanged?.Invoke(_state, $"Le moteur IA a planté {_crashCount} fois. Mode dégradé activé. Veuillez redémarrer l'éditeur ou envoyer les logs.");
                    // Ne pas redémarrer automatiquement pour éviter la boucle
                    return;
                }

                LogEvent(WatchdogEventType.CrashDetected, $"Crash #{_crashCount}");
                if (_state != WatchdogState.CircuitOpen) _ = RestartHostAsync();
                return;
            }

            // ★ Safe Fallback : Si la RAM de l'hôte dépasse 90% du seuil, forcer le déchargement et signaler
            var ramMB = _host.ProcessMemoryMB;
            if (ramMB > _maxMemoryMB * 0.9)
            {
                if (_state == WatchdogState.Healthy)
                {
                    _state = WatchdogState.Degrading;
                    OnStateChanged?.Invoke(_state, "Pression mémoire détectée. Bascule vers le modèle léger recommandée.");
                }
                _ = ForceCleanupAsync();
            }
            // ... (reste du code existant)
        }
        catch { }
    }

    public void Dispose()
    {
        // ... (libération des ressources existantes, ex. timer / hôte)
        OnStateChanged = null;
    }
}
