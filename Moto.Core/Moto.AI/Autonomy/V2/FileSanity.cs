// Moto.Core/Moto.AI/Autonomy/V2/FileSanity.cs
// ★ AJOUT (24/09, agent v2) : « ne casse pas un fichier valide ». Constat du banc d'essai en mode structuré : le modèle
// écrit du C# dans une chaîne JSON, oublie d'échapper un guillemet (`$"` dans `Console.WriteLine($"…")`), la chaîne JSON
// se ferme au mauvais endroit et le fichier est écrit TRONQUÉ à mi-instruction — l'humain a validé un diff, puis le build casse.
// Un fichier tronqué est presque toujours détectable sans compiler : accolades qui ne s'équilibrent plus (C#, Java),
// XML ou JSON qui ne se lit plus. On refuse donc AVANT la confirmation, en disant quoi recopier.
// Règle de prudence : on ne refuse que si le fichier ÉTAIT valide avant (ou est neuf) — jamais pour un fichier déjà cassé.
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace Moto.Core.AI.Autonomy.V2;

internal static class FileSanity
{
    private static readonly string[] BraceLanguages = { ".cs", ".java" };
    private static readonly string[] XmlLike = { ".xml", ".csproj", ".xaml", ".props", ".targets", ".config", ".resx", ".svg", ".slnx" };

    private const string Tip = " Si tu écris du code dans un argument JSON : chaque guillemet du code s'écrit \\\" et chaque retour à la ligne \\n.";

    /// <summary>Un message d'erreur à renvoyer au modèle si <paramref name="after"/> n'est plus valide alors que <paramref name="before"/> l'était (null = fichier neuf).</summary>
    public static string? Check(string displayPath, string? before, string after)
    {
        var ext = Path.GetExtension(displayPath).ToLowerInvariant();

        if (BraceLanguages.Contains(ext)) return CheckBraces(displayPath, before, after);
        if (ext == ".json") return CheckJson(displayPath, before, after);
        if (XmlLike.Contains(ext)) return CheckXml(displayPath, before, after);
        return null;
    }

    // ── C# / Java : accolades ───────────────────────────────────────────────

    private static string? CheckBraces(string display, string? before, string after)
    {
        if (IsUnbalanced(after) is not { } net) return null;
        if (before is not null && IsUnbalanced(before) is not null) return null; // déjà déséquilibré avant : pas notre affaire

        return $"Refusé : après cette écriture {display} aurait {(net > 0 ? $"{net} accolade(s) « {{ » sans « }} » correspondante" : $"{-net} accolade(s) « }} » en trop")}. " +
               "Le contenu semble tronqué ou incomplet : recopie le passage ou le fichier COMPLET, avec toutes ses accolades." + Tip;
    }

    /// <summary>Le déséquilibre d'accolades, si DEUX analyses indépendantes le confirment ; sinon null (équilibré ou analyse peu fiable).</summary>
    private static int? IsUnbalanced(string text)
    {
        if (CodeStructure.NetBraces(text) is not { } net || net == 0) return null;
        return CodeStructure.Scan(text.Replace("\r\n", "\n").Split('\n')) is null ? net : null;
    }

    // ── JSON ────────────────────────────────────────────────────────────────

    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static string? CheckJson(string display, string? before, string after)
    {
        if (string.IsNullOrWhiteSpace(after)) return null;
        var error = JsonError(after);
        if (error is null) return null;
        if (before is not null && !string.IsNullOrWhiteSpace(before) && JsonError(before) is not null) return null;
        return $"Refusé : {display} ne serait plus du JSON valide ({error}). Recopie le passage ou le fichier COMPLET." + Tip;
    }

    private static string? JsonError(string text)
    {
        try { JsonNode.Parse(text, documentOptions: Lenient); return null; }
        catch (JsonException ex) { return ex.Message.Length > 140 ? ex.Message[..140] + "…" : ex.Message; }
    }

    // ── XML (csproj, XAML…) ─────────────────────────────────────────────────

    private static string? CheckXml(string display, string? before, string after)
    {
        if (string.IsNullOrWhiteSpace(after)) return null;
        var error = XmlError(after);
        if (error is null) return null;
        if (before is not null && !string.IsNullOrWhiteSpace(before) && XmlError(before) is not null) return null;
        return $"Refusé : {display} ne serait plus du XML valide ({error}). " +
               "Ferme chaque balise et ne mets jamais « -- » dans un commentaire. Recopie le passage ou le fichier COMPLET." + Tip;
    }

    private static string? XmlError(string text)
    {
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            XDocument.Load(reader);
            return null;
        }
        catch (XmlException ex) { return ex.Message.Length > 160 ? ex.Message[..160] + "…" : ex.Message; }
    }
}
