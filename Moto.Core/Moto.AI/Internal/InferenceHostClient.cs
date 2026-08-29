using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Client named-pipe pour communiquer avec Moto.InferenceHost.
/// </summary>
public sealed class InferenceHostClient : IDisposable
{
    private const string PipeName = "moto.inference.host";
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_pipe?.IsConnected == true) return true;

            _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipe.ConnectAsync(3000, ct);

            _reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(bool IsHostAlive, string? Error)> GetStatusAsync(CancellationToken ct)
    {
        if (!await ConnectAsync(ct))
            return (false, "InferenceHost non démarré");

        var response = await SendRequestAsync(new { action = "status" }, ct);
        return response?.success == true
            ? (true, null)
            : (false, response?.error);
    }

    public async Task EnsureModelLoadedAsync(string modelId, string tier, CancellationToken ct)
    {
        if (!await ConnectAsync(ct)) return;
        await SendRequestAsync(new { action = "load", modelId, tier }, ct);
    }

    public async Task<AiResponse?> InferAsync(
        string modelId,
        string prompt,
        int maxTokens,
        CancellationToken ct)
    {
        if (!await ConnectAsync(ct)) return null;

        var response = await SendRequestAsync(
            new { action = "infer", modelId, prompt, maxTokens }, ct);

        if (response?.success != true) return null;

        return new AiResponse
        {
            Content = response.data?.ToString() ?? string.Empty,
            Provider = "embedded",
            LatencyMs = 0 // TODO: extraire du résultat
        };
    }

    private async Task<HostResponse?> SendRequestAsync(object request, CancellationToken ct)
    {
        if (_writer is null || _reader is null) return null;

        var json = JsonSerializer.Serialize(request);
        await _writer.WriteLineAsync(json);

        var line = await _reader.ReadLineAsync(ct);
        return line is null ? null : JsonSerializer.Deserialize<HostResponse>(line);
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _pipe?.Dispose();
        _lock.Dispose();
    }
}

public class HostResponse
{
    public bool success { get; set; }
    public JsonElement? data { get; set; }
    public string? error { get; set; }
}
