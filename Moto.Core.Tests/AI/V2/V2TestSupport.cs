// Moto.Core.Tests/AI/V2/V2TestSupport.cs
// Outils de test de l'agent v2 : un dossier de travail jetable et un faux serveur Ollama scénarisé
// (aucun vrai modèle, aucun réseau — les tests sont déterministes et instantanés).
using System.Text;
using System.Text.Json.Nodes;

namespace Moto.Core.Tests.AI.V2;

public sealed class TempWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "moto-agentv2-" + Guid.NewGuid().ToString("N")[..10]);
    public string Backups => Path.Combine(Root + "-backups");

    public TempWorkspace() => Directory.CreateDirectory(Root);

    public string Write(string relative, string content, bool crlf = false)
    {
        var full = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, crlf ? content.Replace("\n", "\r\n") : content, new UTF8Encoding(false));
        return full;
    }

    public string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));
    public bool Exists(string relative) => File.Exists(Path.Combine(Root, relative));

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
        try { if (Directory.Exists(Backups)) Directory.Delete(Backups, recursive: true); } catch (IOException) { }
    }
}

/// <summary>Faux serveur Ollama : /api/show + /api/chat (flux NDJSON) rejoués depuis un scénario.</summary>
public sealed class FakeOllamaHandler : HttpMessageHandler
{
    private readonly Queue<string> _chatReplies = new();

    public bool SupportsTools { get; set; } = true;
    public List<JsonObject> ChatRequests { get; } = new();

    public FakeOllamaHandler Enqueue(string ndjson) { _chatReplies.Enqueue(ndjson); return this; }

    public FakeOllamaHandler Calls(params (string Name, JsonObject Args)[] calls)
        => Enqueue(Reply(string.Empty, calls));

    public FakeOllamaHandler Text(string text) => Enqueue(Reply(text));

    public static string Reply(string content, params (string Name, JsonObject Args)[] calls)
    {
        var msg = new JsonObject { ["role"] = "assistant", ["content"] = content };
        if (calls.Length > 0)
        {
            var arr = new JsonArray();
            foreach (var (name, args) in calls)
                arr.Add(new JsonObject { ["function"] = new JsonObject { ["name"] = name, ["arguments"] = args.DeepClone() } });
            msg["tool_calls"] = arr;
        }

        var first = new JsonObject { ["model"] = "fake", ["message"] = msg, ["done"] = false };
        var last = new JsonObject
        {
            ["model"] = "fake",
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = string.Empty },
            ["done"] = true,
            ["done_reason"] = "stop",
            ["prompt_eval_count"] = 120,
            ["eval_count"] = 30,
            ["total_duration"] = 1_000_000_000L,
            ["load_duration"] = 0L,
            ["eval_duration"] = 500_000_000L,
        };
        return first.ToJsonString() + "\n" + last.ToJsonString() + "\n";
    }

    public static JsonObject Args(params (string Key, object Value)[] pairs)
    {
        var o = new JsonObject();
        foreach (var (k, v) in pairs) o[k] = JsonValue.Create(v);
        return o;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path == "/api/version")
            return Json("{\"version\":\"0.0.0-test\"}");

        if (path == "/api/show")
        {
            var caps = SupportsTools ? "[\"completion\",\"tools\"]" : "[\"completion\"]";
            return Json("{\"capabilities\":" + caps + ",\"details\":{\"parameter_size\":\"7B\",\"quantization_level\":\"Q4\"},\"model_info\":{\"qwen2.context_length\":32768}}");
        }

        if (path == "/api/chat")
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)) as JsonObject ?? new JsonObject();
            ChatRequests.Add(body);
            var reply = _chatReplies.Count > 0 ? _chatReplies.Dequeue() : Reply("(plus rien de prévu dans le scénario)");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(reply, Encoding.UTF8, "application/x-ndjson"),
            };
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"not found\"}") };
    }

    private static HttpResponseMessage Json(string json)
        => new(System.Net.HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
