// Moto.Core/Moto.AI/Llm/StructuredTools.cs
// ★ AJOUT (24/09, "écriture agentique fonctionnelle") : appels d'outils par SORTIE CONTRAINTE.
//
// Pourquoi : MESURÉ le 24/09 sur le banc d'essai (8 tâches, 1 essai), avec les appels d'outils NATIFS d'Ollama,
// qwen2.5-coder:7b (le modèle par défaut de l'éditeur) ne réussit que 2 tâches sur 8. Ses transcriptions montrent
// toujours la même panne : au lieu d'appeler l'outil il ÉCRIT « je vais utiliser read_file… » ou recopie du code,
// et répète la même phrase après chaque relance. Les modèles sans la capacité « tools » (deepseek-coder-v2) ne
// peuvent pas être agents du tout.
//
// Le principe : on n'envoie pas « tools » ; on décrit les outils dans la consigne et on IMPOSE au serveur un
// schéma JSON (paramètre « format » d'Ollama). Le décodage est alors contraint : le modèle ne PEUT écrire qu'un
// objet {"tool": "<nom>", "arguments": {…}} valide pour l'un des outils — impossible d'annoncer, de s'excuser,
// de recopier un fichier ou d'inventer un paramètre. Le reste de la boucle ne voit aucune différence : elle reçoit
// toujours des LlmToolCall.
using System.Text;
using System.Text.Json.Nodes;

namespace Moto.Core.AI.Llm;

/// <summary>Comment le client obtient les appels d'outils d'un modèle.</summary>
public enum LlmToolMode
{
    /// <summary>Appels d'outils natifs d'Ollama (paramètre « tools »).</summary>
    Native,

    /// <summary>Sortie contrainte par un schéma JSON : une réponse = exactement un appel d'outil valide.</summary>
    Structured,
}

public static class StructuredTools
{
    public const string ToolKey = "tool";
    public const string ArgumentsKey = "arguments";

    /// <summary>Champ facultatif écrit AVANT l'outil : une phrase de raisonnement (le modèle n'a sinon aucun endroit où réfléchir).</summary>
    public const string ThoughtKey = "pensee";
    public const int ThoughtMaxLength = 300;

    /// <summary>Schéma imposé à la génération : l'un des outils, avec ses paramètres exacts.</summary>
    public static JsonObject BuildSchema(IReadOnlyList<LlmToolSpec> tools, bool withThought = false)
    {
        var branches = new JsonArray();
        foreach (var t in tools)
        {
            var args = (JsonObject)t.Parameters.DeepClone();
            args["additionalProperties"] = false;

            var properties = new JsonObject();
            var required = new JsonArray();
            if (withThought)
            {
                properties[ThoughtKey] = new JsonObject { ["type"] = "string", ["maxLength"] = ThoughtMaxLength };
                required.Add(ThoughtKey);
            }
            properties[ToolKey] = new JsonObject { ["const"] = t.Name };
            properties[ArgumentsKey] = args;
            required.Add(ToolKey);
            required.Add(ArgumentsKey);

            branches.Add(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false,
            });
        }
        return new JsonObject { ["anyOf"] = branches };
    }

    /// <summary>Le paragraphe ajouté à la consigne système : format de réponse et liste des outils.</summary>
    public static string BuildInstructions(IReadOnlyList<LlmToolSpec> tools, bool withThought = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FORMAT DE RÉPONSE (obligatoire)");
        sb.AppendLine(withThought
            ? "Tu réponds TOUJOURS par UN SEUL objet JSON, sans aucun autre texte : {\"pensee\": \"<une phrase>\", \"tool\": \"<nom de l'outil>\", \"arguments\": { … }}"
            : "Tu réponds TOUJOURS par UN SEUL objet JSON, sans aucun autre texte : {\"tool\": \"<nom de l'outil>\", \"arguments\": { … }}");
        if (withThought)
            sb.AppendLine("« pensee » : en UNE phrase courte, ce que tu viens d'apprendre et ce que tu fais maintenant (dis, par exemple, quelle modification tu prépares).");
        sb.AppendLine("Un seul appel d'outil par réponse. Le résultat te revient ensuite dans un message « Résultat de … », puis tu appelles l'outil suivant.");
        sb.AppendLine();
        sb.AppendLine("OUTILS");
        foreach (var t in tools)
        {
            sb.AppendLine($"- {t.Name} : {t.Description}");
            if (t.Parameters["properties"] is not JsonObject props) continue;

            var required = (t.Parameters["required"] as JsonArray)?.Select(n => n?.GetValue<string>()).ToHashSet() ?? new HashSet<string?>();
            foreach (var (name, spec) in props)
            {
                var type = FrenchType(spec?["type"]?.GetValue<string>());
                var description = spec?["description"]?.GetValue<string>();
                sb.AppendLine($"    {name} ({type}{(required.Contains(name) ? ", obligatoire" : string.Empty)}){(string.IsNullOrEmpty(description) ? string.Empty : " : " + description)}");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string FrenchType(string? type) => type switch
    {
        "string" => "texte",
        "integer" => "entier",
        "number" => "nombre",
        "boolean" => "vrai/faux",
        "array" => "liste",
        "object" => "objet",
        _ => "texte",
    };

    /// <summary>
    /// La conversation telle que le serveur la reçoit en mode structuré : les appels d'outils deviennent le JSON que le modèle
    /// est censé écrire, les résultats d'outils des messages « Résultat de … », et les instructions rejoignent la consigne système.
    /// </summary>
    public static List<LlmMessage> ToPlainMessages(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolSpec> tools, bool withThought = false)
    {
        var instructions = BuildInstructions(tools, withThought);
        var plain = new List<LlmMessage>(messages.Count + 1);
        var placed = false;

        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case "system" when !placed:
                    plain.Add(LlmMessage.System(m.Content.TrimEnd() + "\n\n" + instructions));
                    placed = true;
                    break;

                case "assistant" when m.ToolCalls is { Count: > 0 }:
                    plain.Add(LlmMessage.Assistant(string.Join("\n", m.ToolCalls.Select(c => Render(c, withThought ? m.Content : null)))));
                    break;

                case "tool":
                    plain.Add(LlmMessage.User($"Résultat de « {m.ToolName ?? "outil"} » :\n{m.Content}"));
                    break;

                default:
                    plain.Add(m);
                    break;
            }
        }

        if (!placed) plain.Insert(0, LlmMessage.System(instructions));
        return plain;
    }

    /// <summary>Le JSON compact d'un appel d'outil, tel que le modèle l'écrit en mode structuré.</summary>
    public static string Render(LlmToolCall call, string? thought = null)
    {
        var json = new JsonObject();
        if (thought is not null) json[ThoughtKey] = thought;
        json[ToolKey] = call.Name;
        json[ArgumentsKey] = call.Arguments.DeepClone();
        return json.ToJsonString();
    }

    /// <summary>Lit la réponse contrainte. Null si ce n'est pas un appel d'outil connu (réponse tronquée, texte libre…).</summary>
    public static LlmToolCall? TryParseCall(string content, IReadOnlyList<LlmToolSpec> tools)
        => TryParse(content, tools, out _);

    /// <summary>Comme <see cref="TryParseCall"/>, et renvoie aussi la phrase de raisonnement (« pensee ») si le modèle en a écrit une.</summary>
    public static LlmToolCall? TryParse(string content, IReadOnlyList<LlmToolSpec> tools, out string thought)
    {
        thought = string.Empty;
        if (string.IsNullOrWhiteSpace(content)) return null;

        var text = content.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('\n');
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start) text = text[(start + 1)..end].Trim();
        }

        JsonNode? node;
        try { node = JsonNode.Parse(text); }
        catch (System.Text.Json.JsonException) { return null; }

        if (node is not JsonObject obj) return null;

        var name = (obj[ToolKey] ?? obj["name"]) is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (string.IsNullOrEmpty(name) || !tools.Any(t => t.Name == name)) return null;

        if (obj[ThoughtKey] is JsonValue tv && tv.TryGetValue<string>(out var t)) thought = t.Trim();

        var args = (obj[ArgumentsKey] ?? obj["parameters"]) as JsonObject;
        return new LlmToolCall(name, args is null ? new JsonObject() : (JsonObject)args.DeepClone());
    }
}
