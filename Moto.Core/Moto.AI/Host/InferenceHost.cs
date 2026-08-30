using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Settings;

namespace Moto.Core.AI.Host;

public class InferenceHost
{
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly SettingsEngine _settings;

    public InferenceHost(SettingsEngine settings)
    {
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream("moto.inference.host", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(ct);

            // Délégation à un handler pour ne pas bloquer l'acceptation de nouveaux clients
            _ = Task.Run(() => HandleClientAsync(server, ct), ct);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        while (!ct.IsCancellationRequested && server.IsConnected)
        {
            var command = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(command)) break;

            switch (command)
            {
                case "HEALTH":
                    var health = new
                    {
                        Status = "OK",
                        UptimeSeconds = (DateTime.UtcNow - _startTime).TotalSeconds,
                        WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024),
                        MaxPrefetchSlots = SettingsCatalog.Ai.Advanced.MaxConcurrentPrefetch.Value,
                        CircuitState = "Closed" // À relier à InferenceWatchdog
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(health));
                    break;

                case "COLLECT_LOGS":
                    var archivePath = await CollectAndArchiveLogsAsync(ct);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { ArchivePath = archivePath }));
                    break;

                // ... autres commandes existantes (INFERENCE, LOAD_MODEL, etc.) ...
            }
        }
    }

    private async Task<string> CollectAndArchiveLogsAsync(CancellationToken ct)
    {
        // Logique de zip des fichiers .log de %AppData%/MotoEditor/logs/
        await Task.Delay(100, ct); // Simulation
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MotoEditor", "logs");
        return Path.Combine(logDir, $"moto-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
    }
}
