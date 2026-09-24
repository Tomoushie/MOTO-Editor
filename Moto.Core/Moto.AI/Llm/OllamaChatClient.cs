// Moto.Core/Moto.AI/Llm/OllamaChatClient.cs
// ★ AJOUT (24/09, "écriture générative et agentique fonctionnelle") : client Ollama
// pour /api/chat — le point d'appel unique des nouveaux chemins d'écriture IA.
// Ce que l'ancien Internal/OllamaClient (/api/generate) ne faisait pas, MESURÉ le 24/09 :
//  - pas de flux : rien à l'écran tant que la réponse entière n'est pas finie ;
//  - pas d'options : la fenêtre de contexte restait au défaut d'Ollama (souvent 4096), qui
//    tronque SILENCIEUSEMENT le début de la conversation — donc la consigne — dès que
//    l'agent a lu un fichier ;
//  - pas d'appels d'outils natifs : l'agent devait parser du texte libre (9 rustines de
//    tolérance déjà ajoutées) alors que qwen2.5-coder, qwen3, gpt-oss, llama3.1 et gemma4
//    savent tous appeler des outils ;
//  - « localhost » : résolu d'abord en IPv6, ~0,2 s perdue par nouvelle connexion.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Moto.Core.AI.Llm;

public sealed class OllamaChatClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, LlmModelInfo?> _infoCache = new(StringComparer.OrdinalIgnoreCase);

    public string Endpoint { get; }

    /// <summary>Délai maximal avant le PREMIER morceau — inclut le chargement du modèle en VRAM (~13 s mesuré pour un 7B).</summary>
    public TimeSpan FirstChunkTimeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Délai maximal sans aucun nouveau morceau une fois la réponse commencée.</summary>
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromSeconds(90);

    public OllamaChatClient(string? endpoint = null, HttpMessageHandler? handler = null)
    {
        Endpoint = NormalizeEndpoint(endpoint);
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        // Les délais sont gérés par jeton d'annulation (premier morceau / silence), pas par HttpClient :
        // une réponse en flux peut légitimement durer des minutes.
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public static string NormalizeEndpoint(string? endpoint)
    {
        var e = string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:11434" : endpoint.Trim().TrimEnd('/');
        return e.Replace("//localhost", "//127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _http.Dispose();

    // ── Découverte ──────────────────────────────────────────────────────────

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await _http.GetAsync(Endpoint + "/api/version", linked.Token).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Noms des modèles installés.</summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var json = await _http.GetStringAsync(Endpoint + "/api/tags", linked.Token).ConfigureAwait(false);
            var names = new List<string>();
            if (JsonNode.Parse(json)?["models"] is JsonArray models)
            {
                foreach (var m in models)
                {
                    var name = AsString(m?["name"]);
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
            }
            return names;
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmException("Ollama ne répond pas (liste des modèles).") { TimedOut = true };
        }
    }

    /// <summary>Capacités, taille et contexte d'un modèle (mis en cache). Null si le serveur ne le connaît pas.</summary>
    public async Task<LlmModelInfo?> ShowAsync(string model, CancellationToken ct = default)
    {
        if (_infoCache.TryGetValue(model, out var cached)) return cached;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(15));
            var body = new JsonObject { ["model"] = model };
            using var resp = await _http.PostAsync(Endpoint + "/api/show",
                new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), linked.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false));
            if (root is null) return null;

            var caps = new List<string>();
            if (root["capabilities"] is JsonArray arr)
                foreach (var c in arr) { var s = AsString(c); if (!string.IsNullOrEmpty(s)) caps.Add(s); }

            var ctx = 0;
            if (root["model_info"] is JsonObject mi)
                foreach (var kv in mi)
                    if (kv.Key.EndsWith(".context_length", StringComparison.Ordinal) && kv.Value is JsonValue v && v.TryGetValue<int>(out var n))
                        ctx = n;

            var info = new LlmModelInfo(model,
                AsString(root["details"]?["parameter_size"]) ?? string.Empty,
                AsString(root["details"]?["quantization_level"]) ?? string.Empty,
                ctx, caps);
            _infoCache[model] = info;
            return info;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    // ── Chat en flux ────────────────────────────────────────────────────────

    /// <summary>
    /// Envoie la conversation et lit la réponse EN FLUX : <paramref name="onContent"/> reçoit chaque
    /// morceau de texte dès qu'il arrive. Retourne la réponse complète (texte + appels d'outils + mesures).
    /// </summary>
    public async Task<LlmReply> ChatAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolSpec>? tools = null,
        LlmOptions? options = null,
        Action<string>? onContent = null,
        Action<string>? onThinking = null,
        CancellationToken ct = default)
    {
        options ??= new LlmOptions();

        var info = await ShowAsync(model, ct).ConfigureAwait(false);
        if (tools is { Count: > 0 } && info is not null && !info.SupportsTools)
        {
            throw new LlmException(
                $"Le modèle « {model} » ne sait pas appeler d'outils : il ne peut pas piloter un agent. " +
                "Choisis un modèle avec la capacité « tools » (ex. qwen2.5-coder, qwen3, gpt-oss, llama3.1).")
            { ToolsNotSupported = true };
        }

        // « think » n'est envoyé qu'aux modèles qui savent réfléchir (sinon Ollama répond par une erreur).
        var think = options.Think;
        if (think is not null && (info is null || !info.SupportsThinking)) think = null;

        var body = BuildBody(model, messages, tools, options, think);
        var clock = Stopwatch.StartNew();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(FirstChunkTimeout);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint + "/api/chat")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
                throw MapError((int)resp.StatusCode, text, model);
            }

            using var stream = await resp.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var content = new StringBuilder();
            var thinking = new StringBuilder();
            var calls = new List<LlmToolCall>();
            JsonNode? last = null;
            var firstChunk = -1.0;

            while (true)
            {
                var line = await reader.ReadLineAsync(linked.Token).ConfigureAwait(false);
                if (line is null) break;
                if (line.Length == 0) continue;

                if (firstChunk < 0) firstChunk = clock.Elapsed.TotalSeconds;
                linked.CancelAfter(StallTimeout);

                JsonNode? node;
                try { node = JsonNode.Parse(line); }
                catch (JsonException) { continue; }
                if (node is null) continue;

                if (node["error"] is JsonNode err) throw MapError(0, err.ToString(), model);

                if (node["message"] is JsonNode msg)
                {
                    var c = AsString(msg["content"]);
                    if (!string.IsNullOrEmpty(c)) { content.Append(c); onContent?.Invoke(c); }

                    var t = AsString(msg["thinking"]);
                    if (!string.IsNullOrEmpty(t)) { thinking.Append(t); onThinking?.Invoke(t); }

                    if (msg["tool_calls"] is JsonArray arr)
                    {
                        foreach (var tc in arr)
                        {
                            var fn = tc?["function"];
                            var name = AsString(fn?["name"]);
                            if (string.IsNullOrEmpty(name)) continue;
                            calls.Add(new LlmToolCall(name, ParseArguments(fn?["arguments"])));
                        }
                    }
                }

                if (node["done"] is JsonValue d && d.TryGetValue<bool>(out var done) && done) last = node;
            }

            return new LlmReply
            {
                Content = content.ToString(),
                Thinking = thinking.ToString(),
                ToolCalls = calls,
                DoneReason = AsString(last?["done_reason"]) ?? string.Empty,
                PromptTokens = AsInt(last?["prompt_eval_count"]),
                CompletionTokens = AsInt(last?["eval_count"]),
                TotalSeconds = AsLong(last?["total_duration"]) / 1e9,
                LoadSeconds = AsLong(last?["load_duration"]) / 1e9,
                TokensPerSecond = AsLong(last?["eval_duration"]) > 0
                    ? AsInt(last?["eval_count"]) / (AsLong(last?["eval_duration"]) / 1e9)
                    : 0,
                FirstChunkSeconds = firstChunk < 0 ? clock.Elapsed.TotalSeconds : firstChunk,
            };
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmException(
                $"Le modèle « {model} » ne répond plus (délai dépassé). La machine est peut-être saturée, ou le modèle trop gros pour la carte graphique.")
            { TimedOut = true };
        }
    }

    // ── Construction / lecture JSON ─────────────────────────────────────────

    private static JsonObject BuildBody(string model, IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolSpec>? tools, LlmOptions o, string? think)
    {
        var msgs = new JsonArray();
        foreach (var m in messages)
        {
            var jm = new JsonObject { ["role"] = m.Role, ["content"] = m.Content };
            if (m.ToolCalls is { Count: > 0 })
            {
                var arr = new JsonArray();
                foreach (var c in m.ToolCalls)
                    arr.Add(new JsonObject
                    {
                        ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments.DeepClone() }
                    });
                jm["tool_calls"] = arr;
            }
            if (m.Role == "tool" && !string.IsNullOrEmpty(m.ToolName)) jm["tool_name"] = m.ToolName;
            msgs.Add(jm);
        }

        var opts = new JsonObject { ["num_ctx"] = o.NumCtx, ["temperature"] = o.Temperature };
        if (o.NumPredict is int np) opts["num_predict"] = np;

        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = msgs,
            ["stream"] = true,
            ["keep_alive"] = o.KeepAlive,
            ["options"] = opts,
        };

        if (tools is { Count: > 0 })
        {
            var arr = new JsonArray();
            foreach (var t in tools)
                arr.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = t.Parameters.DeepClone(),
                    }
                });
            body["tools"] = arr;
        }

        if (think is not null)
            body["think"] = think switch
            {
                "true" => JsonValue.Create(true),
                "false" => JsonValue.Create(false),
                _ => JsonValue.Create(think),
            };

        return body;
    }

    /// <summary>Ollama renvoie les arguments comme un objet ; certains modèles/serveurs compatibles OpenAI les renvoient comme une chaîne JSON.</summary>
    private static JsonObject ParseArguments(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                return (JsonObject)o.DeepClone();
            case JsonValue v when v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s):
                try { if (JsonNode.Parse(s) is JsonObject parsed) return parsed; }
                catch (JsonException) { /* arguments illisibles : objet vide, l'outil signalera les champs manquants */ }
                return new JsonObject();
            default:
                return new JsonObject();
        }
    }

    private static string? AsString(JsonNode? n)
        => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int AsInt(JsonNode? n)
        => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    private static long AsLong(JsonNode? n)
        => n is JsonValue v && v.TryGetValue<long>(out var l) ? l : 0;

    private LlmException Unreachable(Exception inner)
        => new($"Ollama est injoignable à {Endpoint}. Est-il lancé ?", inner);

    private static LlmException MapError(int status, string body, string model)
    {
        var message = body;
        try
        {
            if (JsonNode.Parse(body)?["error"] is JsonNode e) message = e.ToString();
        }
        catch (JsonException) { /* corps non JSON : on garde le texte brut */ }

        if (message.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
            return new LlmException($"Le modèle « {model} » ne sait pas appeler d'outils.") { ToolsNotSupported = true };

        if (status == 404 || message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return new LlmException($"Modèle « {model} » introuvable dans Ollama (ollama pull {model}).");

        return new LlmException($"Ollama a refusé la requête ({(status == 0 ? "erreur" : status.ToString())}) : {message}");
    }
}
