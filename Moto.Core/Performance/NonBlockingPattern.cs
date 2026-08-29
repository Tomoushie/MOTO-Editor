using Microsoft.UI.Dispatching;

namespace Moto.Core.Performance;

/// <summary>
/// Pattern Non-Blocking : l'UI reste toujours responsive.
/// Les opérations lourdes s'exécutent en arrière-plan avec feedback visuel.
/// </summary>
public static class NonBlockingPattern
{
    /// <summary>
    /// Exécute une opération lourde sans bloquer l'UI.
    /// </summary>
    public static async Task<T> RunAsync<T>(
        Func<Task<T>> heavyWork,
        Action<string>? onProgress = null,
        DispatcherQueue? uiDispatcher = null)
    {
        var taskId = Guid.NewGuid().ToString("N")[..8];
        onProgress?.Invoke($"⏳ {taskId}");

        // Exécute hors du thread UI
        var result = await Task.Run(heavyWork).ConfigureAwait(false);

        // Retour sur le thread UI si nécessaire
        if (uiDispatcher != null)
        {
            var tcs = new TaskCompletionSource();
            uiDispatcher.TryEnqueue(() =>
            {
                onProgress?.Invoke($"✅ {taskId}");
                tcs.SetResult();
            });
            await tcs.Task;
        }

        return result;
    }

    /// <summary>
    /// Exécute une opération lourde void sans bloquer l'UI.
    /// </summary>
    public static async Task RunAsync(
        Func<Task> heavyWork,
        Action<string>? onProgress = null)
    {
        await RunAsync(async () => { await heavyWork(); return 0; }, onProgress);
    }
}
