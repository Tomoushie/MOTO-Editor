// Moto.Core/Moto.AI/Autonomy/V2/ToolCallSalvage.cs
// ★ AJOUT (24/09, agent v2) : certains modèles (qwen2.5-coder dans Ollama, mesuré) écrivent l'appel d'outil
// comme du TEXTE JSON dans la réponse au lieu de le passer en appel structuré — souvent entouré de
// ```json ou de <tool_call>. Sans ce filet, l'agent croit à une réponse finale et s'arrête sans rien faire.
// On ne retient un objet JSON que si son nom est celui d'un outil CONNU : du JSON quelconque (un exemple de
// configuration cité dans une explication) n'est jamais exécuté.
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Moto.Core.AI.Llm;

namespace Moto.Core.AI.Autonomy.V2;

internal static class ToolCallSalvage
{
    private static readonly Regex Wrappers = new(@"</?tool_call>|```(?:json)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (IReadOnlyList<LlmToolCall> Calls, string Remaining) Extract(string content, ISet<string> knownTools)
    {
        var calls = new List<LlmToolCall>();
        if (string.IsNullOrWhiteSpace(content)) return (calls, string.Empty);

        var remaining = new StringBuilder();
        var i = 0;
        while (i < content.Length)
        {
            var open = content.IndexOf('{', i);
            if (open < 0) { remaining.Append(content, i, content.Length - i); break; }

            var close = FindMatchingBrace(content, open);
            if (close < 0) { remaining.Append(content, i, content.Length - i); break; }

            var parsed = TryParse(content.Substring(open, close - open + 1), knownTools);
            if (parsed is not null)
            {
                remaining.Append(content, i, open - i);
                calls.Add(parsed);
                i = close + 1;
            }
            else
            {
                remaining.Append(content, i, open + 1 - i);
                i = open + 1;
            }
        }

        var text = calls.Count == 0 ? content : Wrappers.Replace(remaining.ToString(), string.Empty).Trim();
        return (calls, text);
    }

    private static LlmToolCall? TryParse(string json, ISet<string> knownTools)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        if (node is not JsonObject obj) return null;

        // Forme « OpenAI » : { "function": { "name": ..., "arguments": ... } }
        if (obj["function"] is JsonObject fn) obj = fn;

        var name = obj["name"] is JsonValue nv && nv.TryGetValue<string>(out var n) ? n : null;
        if (name is null || !knownTools.Contains(name)) return null;

        JsonObject args;
        var raw = obj["arguments"] ?? obj["parameters"];
        if (raw is JsonObject ao)
            args = (JsonObject)ao.DeepClone();
        else if (raw is JsonValue rv && rv.TryGetValue<string>(out var s) && TryParseObject(s, out var parsed))
            args = parsed;
        else
        {
            // Arguments mis à plat : { "name": "read_file", "path": "x" }
            args = new JsonObject();
            foreach (var kv in obj)
                if (kv.Key is not "name" and not "arguments" and not "parameters")
                    args[kv.Key] = kv.Value?.DeepClone();
        }
        return new LlmToolCall(name, args);
    }

    private static bool TryParseObject(string s, out JsonObject result)
    {
        try
        {
            if (JsonNode.Parse(s) is JsonObject o) { result = o; return true; }
        }
        catch (JsonException) { /* texte non JSON */ }
        result = new JsonObject();
        return false;
    }

    /// <summary>Accolade fermante correspondante, en ignorant celles qui sont dans des chaînes.</summary>
    private static int FindMatchingBrace(string s, int open)
    {
        var depth = 0;
        var inString = false;
        for (var i = open; i < s.Length; i++)
        {
            var c = s[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }
}
