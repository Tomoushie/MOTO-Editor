using System.Text.Json;
using Moto.Core.Performance;

namespace Moto.EngineHost;

/// <summary>
/// Hôte léger pour les moteurs lourds isolés.
/// Reçoit des requêtes JSON-RPC sur stdin, répond sur stdout.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var engineName = args.FirstOrDefault(a => a.StartsWith("--engine="))?[9..] ?? "unknown";

        Console.Error.WriteLine($"[EngineHost] Starting engine: {engineName}");

        using var reader = Console.In;
        using var writer = Console.Out;
        writer.AutoFlush = true;

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
                if (request == null) continue;

                var response = await HandleRequestAsync(engineName, request);
                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                var errorResponse = new JsonRpcResponse
                {
                    Id = "error",
                    Error = new JsonRpcError { Code = -32000, Message = ex.Message }
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
            }
        }
    }

    private static Task<JsonRpcResponse> HandleRequestAsync(string engineName, JsonRpcRequest request)
    {
        var response = new JsonRpcResponse { Id = request.Id };

        switch (request.Method)
        {
            case "echo":
                var echoParam = JsonSerializer.Deserialize<EchoParams>(
                    JsonSerializer.Serialize(request.Params));
                response.Result = new { echo = echoParam?.Message ?? "" };
                break;

            case "analyze":
                response.Result = new { engine = engineName, analyzed = true };
                break;

            default:
                response.Error = new JsonRpcError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}"
                };
                break;
        }

        return Task.FromResult(response);
    }
}

public class EchoParams
{
    public string Message { get; set; } = "";
}
