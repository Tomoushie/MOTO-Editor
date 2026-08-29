using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Moto.InferenceHost.Services;

/// <summary>
/// Service principal de l'hôte d'inférence.
/// Écoute sur un named pipe et route les requêtes vers InferenceEngine.
/// </summary>
public sealed class InferenceHostService
{
    private const string PipeName = "moto.inference.host";
    private readonly ILogger<InferenceHostService> _logger;
    private readonly InferenceEngine _engine;
    private readonly ModelRegistry _registry;

    public InferenceHostService(
        ILogger<InferenceHostService> logger,
        InferenceEngine engine,
        ModelRegistry registry)
    {
        _logger = logger;
        _engine = engine;
        _registry = registry;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 4,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            _logger.LogDebug("En attente de connexion...");
            await server.WaitForConnectionAsync(ct);
            _logger.LogInformation("Client connecté au pipe.");

            _ = Task.Run(() => HandleClientAsync(server, ct), ct);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        try
        {
            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break;

                var response = await ProcessRequestAsync(line, ct);
                await writer.WriteLineAsync(response);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement client.");
        }
        finally
        {
            pipe.Disconnect();
        }
    }

    private async Task<string> ProcessRequestAsync(string request, CancellationToken ct)
    {
        try
        {
            var req = JsonSerializer.Deserialize<InferenceRequest>(request);
            if (req is null) return JsonError("Requête invalide");

            var result = req.Action switch
            {
                "load" => await _registry.LoadModelAsync(req.ModelId!, req.Tier, ct),
                "unload" => await _registry.UnloadModelAsync(req.ModelId!, ct),
                "infer" => await _engine.InferAsync(req.ModelId!, req.Prompt!, req.MaxTokens, ct),
                "status" => _registry.GetStatus(),
                _ => JsonError($"Action inconnue: {req.Action}")
            };

            return JsonSerializer.Serialize(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec du traitement de la requête.");
            return JsonError(ex.Message);
        }
    }

    private static string JsonError(string message)
        => JsonSerializer.Serialize(new { success = false, error = message });
}

public record InferenceRequest
{
    public string? Action { get; init; }
    public string? ModelId { get; init; }
    public string? Prompt { get; init; }
    public int MaxTokens { get; init; } = 256;
    public string? Tier { get; init; }
}
