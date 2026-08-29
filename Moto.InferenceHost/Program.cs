using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moto.InferenceHost.Services;

namespace Moto.InferenceHost;

/// <summary>
/// Point d'entrée du processus d'inférence isolé.
/// Communique avec Moto.Editor via named pipes.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var logger = provider.GetRequiredService<ILogger<InferenceHostService>>();
        logger.LogInformation("Moto.InferenceHost démarré (PID: {Pid})", Environment.ProcessId);

        var host = provider.GetRequiredService<InferenceHostService>();
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await host.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Arrêt propre demandé.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Crash fatal de l'InferenceHost.");
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<InferenceHostService>();
        services.AddSingleton<OnnxModelLoader>();
        services.AddSingleton<InferenceEngine>();
        services.AddSingleton<ModelRegistry>();
    }
}
