using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Moto.Core.Performance;

/// <summary>
/// Lance des moteurs lourds dans des processus séparés.
/// Communication via JSON-RPC sur stdin/stdout.
/// </summary>
public sealed class HeavyProcessLauncher : IDisposable
{
    private readonly Dictionary<string, HeavyProcess> _processes = new();
    private readonly string _executablePath;

    public static HeavyProcessLauncher Instance { get; private set; } = null!;

    public HeavyProcessLauncher(string executablePath)
    {
        _executablePath = executablePath;
        Instance = this;
    }

    /// <summary>
    /// Lance un moteur lourd dans un processus séparé.
    /// </summary>
    public async Task<HeavyProcess> LaunchAsync(string engineName, string[] args)
    {
        if (_processes.TryGetValue(engineName, out var existing))
        {
            return existing;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = $"--engine={engineName} {string.Join(" ", args)}",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var heavyProcess = new HeavyProcess(engineName, process);
        _processes[engineName] = heavyProcess;

        // Démarre la lecture asynchrone des réponses
        _ = heavyProcess.StartReadingResponsesAsync();

        return heavyProcess;
    }

    /// <summary>
    /// Envoie une requête JSON-RPC au processus.
    /// </summary>
    public async Task<TResponse> SendRequestAsync<TResponse>(string engineName, JsonRpcRequest request)
    {
        if (!_processes.TryGetValue(engineName, out var process))
        {
            throw new InvalidOperationException($"Engine '{engineName}' not launched");
        }

        return await process.SendRequestAsync<TResponse>(request);
    }

    /// <summary>
    /// Arrête un processus lourd.
    /// </summary>
    public void Stop(string engineName)
    {
        if (_processes.TryGetValue(engineName, out var process))
        {
            process.Dispose();
            _processes.Remove(engineName);
        }
    }

    /// <summary>
    /// Arrête tous les processus lourds.
    /// </summary>
    public void StopAll()
    {
        foreach (var process in _processes.Values)
        {
            process.Dispose();
        }
        _processes.Clear();
    }

    public void Dispose()
    {
        StopAll();
    }
}

/// <summary>
/// Représente un processus lourd avec communication JSON-RPC.
/// </summary>
public sealed class HeavyProcess : IDisposable
{
    private readonly Process _process;
    private readonly Channel<JsonRpcResponse> _responseChannel;
    private readonly Dictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();

    public string EngineName { get; }
    public bool IsRunning => !_process.HasExited;

    public HeavyProcess(string engineName, Process process)
    {
        EngineName = engineName;
        _process = process;
        _responseChannel = Channel.CreateUnbounded<JsonRpcResponse>();
    }

    /// <summary>
    /// Démarre la lecture asynchrone des réponses du processus.
    /// </summary>
    public async Task StartReadingResponsesAsync()
    {
        using var reader = _process.StandardOutput;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var response = JsonSerializer.Deserialize<JsonRpcResponse>(line);
                if (response != null)
                {
                    await _responseChannel.Writer.WriteAsync(response);

                    // Notifie les requêtes en attente
                    if (_pendingRequests.TryGetValue(response.Id, out var tcs))
                    {
                        tcs.SetResult(response);
                        _pendingRequests.Remove(response.Id);
                    }
                }
            }
            catch
            {
                // Ignore les lignes malformées
            }
        }
    }

    /// <summary>
    /// Envoie une requête JSON-RPC et attend la réponse.
    /// </summary>
    public async Task<TResponse> SendRequestAsync<TResponse>(JsonRpcRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        await _process.StandardInput.WriteLineAsync(json);
        await _process.StandardInput.FlushAsync();

        var tcs = new TaskCompletionSource<JsonRpcResponse>();
        _pendingRequests[request.Id] = tcs;

        var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));

        if (response.Error != null)
        {
            throw new InvalidOperationException($"RPC Error: {response.Error.Message}");
        }

        return JsonSerializer.Deserialize<TResponse>(response.Result?.ToString() ?? "{}")!;
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(5000);
            }
        }
        catch { }
        _process.Dispose();
    }
}

/// <summary>
/// Requête JSON-RPC 2.0.
/// </summary>
public class JsonRpcRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Method { get; set; } = "";
    public object? Params { get; set; }
}

/// <summary>
/// Réponse JSON-RPC 2.0.
/// </summary>
public class JsonRpcResponse
{
    public string JsonRpc { get; set; } = "2.0";
    public string Id { get; set; } = "";
    public object? Result { get; set; }
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
}
