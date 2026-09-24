// Moto.Core/Moto.AI/Autonomy/V2/AgentToolV2.cs
// ★ AJOUT (24/09, agent v2) : contrat des outils de l'agent v2 (appels d'outils NATIFS d'Ollama).
// Même règle de sécurité que la v1 : un outil qui MODIFIE quelque chose ne s'exécute jamais
// lui-même — il PRÉPARE un changement (PrepareAsync : aucun effet sur le disque) et c'est la
// boucle (AgentLoopV2) qui demande la confirmation humaine, PUIS applique. Un outil bogué ne
// peut donc pas court-circuiter le verrou.
using System.Text.Json.Nodes;
using Moto.Core.AI.Llm;

namespace Moto.Core.AI.Autonomy.V2;

public enum ApprovalKind { FileChange, Command }

/// <summary>Résultat d'un outil, tel que renvoyé au modèle. Une erreur est un résultat comme un autre
/// (le modèle la lit et corrige), jamais une exception.</summary>
public sealed record ToolResult(bool IsError, string Text, ChangedFile? Change = null)
{
    public static ToolResult Ok(string text, ChangedFile? change = null) => new(false, text, change);
    public static ToolResult Error(string text) => new(true, text);
}

/// <summary>Un fichier modifié pendant le run (pour le compte rendu et l'annulation).</summary>
public sealed record ChangedFile(string RelativePath, int Added, int Removed, bool Created);

/// <summary>Ce qu'un outil mutant a préparé : rien n'est encore écrit.</summary>
public sealed class PendingChange
{
    public required string Title { get; init; }
    public required string Summary { get; init; }

    /// <summary>Texte montré à l'humain (diff unifié pour un fichier, commande + dossier pour une commande).</summary>
    public required string Details { get; init; }

    public ApprovalKind Kind { get; init; }
    public bool IsDestructive { get; init; }
    public string? Path { get; init; }
    public DiffResult? Diff { get; init; }

    /// <summary>Exécuté UNIQUEMENT après accord humain.</summary>
    public required Func<CancellationToken, Task<ToolResult>> ApplyAsync { get; init; }
}

public sealed class ToolPreparation
{
    /// <summary>Non nul : l'appel est invalide, rien à faire valider (le modèle reçoit ce message d'erreur).</summary>
    public ToolResult? Rejected { get; init; }
    public PendingChange? Change { get; init; }

    public static ToolPreparation Reject(string message) => new() { Rejected = ToolResult.Error(message) };
    public static ToolPreparation Propose(PendingChange change) => new() { Change = change };
}

public abstract class AgentToolV2
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonObject Parameters { get; }

    /// <summary>Vrai : passe par PrepareAsync + confirmation humaine. Faux : ExecuteAsync direct (lecture seule).</summary>
    public virtual bool IsMutating => false;

    /// <summary>Vrai pour les outils qui écrivent dans un fichier du projet (sert à détecter un run qui « termine » sans avoir rien modifié).</summary>
    public virtual bool WritesFiles => false;

    public LlmToolSpec Spec => new(Name, Description, Parameters);

    public virtual Task<ToolResult> ExecuteAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
        => throw new NotSupportedException($"{Name} est un outil mutant : utiliser PrepareAsync.");

    public virtual Task<ToolPreparation> PrepareAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
        => throw new NotSupportedException($"{Name} est un outil en lecture seule : utiliser ExecuteAsync.");

    // ── Aides de schéma JSON ────────────────────────────────────────────────

    protected static JsonObject Schema(JsonObject properties, params string[] required)
    {
        var req = new JsonArray();
        foreach (var r in required) req.Add(r);
        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = req };
    }

    protected static JsonObject Prop(string type, string description)
        => new() { ["type"] = type, ["description"] = description };
}

/// <summary>Lecture tolérante des arguments : un petit modèle envoie parfois « 10 » (texte) pour un entier.</summary>
internal static class ToolArgs
{
    public static string? Str(JsonObject a, string name)
    {
        if (!a.TryGetPropertyValue(name, out var n) || n is null) return null;
        return n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n.ToJsonString().Trim('"');
    }

    public static int? Int(JsonObject a, string name)
    {
        if (!a.TryGetPropertyValue(name, out var n) || n is not JsonValue v) return null;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l)) return (int)Math.Clamp(l, int.MinValue, int.MaxValue);
        if (v.TryGetValue<double>(out var d)) return (int)d;
        return v.TryGetValue<string>(out var s) && int.TryParse(s.Trim(), out var p) ? p : null;
    }

    public static bool Bool(JsonObject a, string name, bool fallback = false)
    {
        if (!a.TryGetPropertyValue(name, out var n) || n is not JsonValue v) return fallback;
        if (v.TryGetValue<bool>(out var b)) return b;
        return v.TryGetValue<string>(out var s) && bool.TryParse(s.Trim(), out var p) ? p : fallback;
    }
}
