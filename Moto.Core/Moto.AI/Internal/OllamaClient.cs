// Moto.Core/AI/Internal/OllamaClient.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Client Ollama (existant, préservé).
/// </summary>
public sealed class OllamaClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public OllamaClient(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new { model = "qwen2.5-coder:7b", prompt, stream = false };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{_baseUrl}/api/generate", content, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        // Parser la réponse Ollama
        return json;
    }

    public async Task<string> GenerateCodeAsync(string instruction, string? context, CancellationToken ct = default)
    {
        var fullPrompt = context != null
            ? $"Context:\n{context}\n\nInstruction: {instruction}\n\nCode:"
            : $"Instruction: {instruction}\n\nCode:";
        return await GenerateAsync(fullPrompt, ct);
    }

    public async Task<string> CompleteCodeAsync(string prefix, string suffix, CancellationToken ct = default)
    {
        var prompt = $"<pre>{prefix}<suf>{suffix}<mid>";
        return await GenerateAsync(prompt, ct);
    }
}
