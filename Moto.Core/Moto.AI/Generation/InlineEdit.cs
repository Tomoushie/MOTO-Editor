// Moto.Core/Moto.AI/Generation/InlineEdit.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : l'édition « en ligne » — l'utilisateur décrit une modification, le modèle
// réécrit la SÉLECTION (ou le fichier), l'humain voit le diff et accepte ou refuse.
// Constat du 24/09 sur l'ancien bandeau IA (MainPage.OnAiBandPrompt) : la réponse REMPLAÇAIT tout le texte de l'éditeur — sans diff, sans
// confirmation, sans vérifier que le bloc de code était complet. Un modèle qui répondait par un simple extrait (ou qui s'arrêtait à
// mi-chemin) effaçait donc le reste du fichier, et l'éditeur n'offrait aucun moyen de revenir en arrière.
// Ce code n'APPLIQUE rien : il prépare un plan (texte final + diff + avertissements) ou explique, en français, pourquoi il refuse.
using System.Text.RegularExpressions;
using Moto.Core.AI.Autonomy.V2;

namespace Moto.Core.AI.Generation;

public enum InlineEditScope
{
    /// <summary>Seul le texte sélectionné est réécrit.</summary>
    Selection,

    /// <summary>Pas de sélection : le fichier entier est réécrit.</summary>
    WholeFile,
}

public sealed class InlineEditRequest
{
    /// <summary>Nom ou chemin du fichier (sert au langage et au contrôle de validité) ; peut être vide pour un onglet sans nom.</summary>
    public string DisplayPath { get; init; } = string.Empty;

    /// <summary>Le texte de l'onglet tel que l'utilisateur le voit — pas celui du disque.</summary>
    public string DocumentText { get; init; } = string.Empty;

    /// <summary>Le texte sélectionné (null ou blanc = pas de sélection).</summary>
    public string? Selection { get; init; }

    public string Instruction { get; init; } = string.Empty;

    public InlineEditScope Scope => string.IsNullOrWhiteSpace(Selection) ? InlineEditScope.WholeFile : InlineEditScope.Selection;
}

public sealed class InlineEditPlan
{
    public InlineEditScope Scope { get; init; }

    /// <summary>Le texte COMPLET du document après application (fins de ligne du document d'origine).</summary>
    public string NewText { get; init; } = string.Empty;

    /// <summary>Ce qui prend la place du texte remplacé (sélection ou fichier entier), fins de ligne normalisées en « \n ».</summary>
    public string Replacement { get; init; } = string.Empty;

    public DiffResult Diff { get; init; } = new(string.Empty, 0, 0);

    /// <summary>Points à vérifier avant d'accepter (montrés au-dessus du diff) — le plan reste applicable.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <param name="Plan">Le plan, ou null si <paramref name="Problem"/> explique pourquoi rien n'a été proposé.</param>
/// <param name="Problem">Message lisible par l'utilisateur (« rien n'a été modifié » y figure toujours).</param>
/// <param name="Model">Le modèle qui a répondu (ou son étiquette pour un fournisseur externe).</param>
/// <param name="Note">Information à montrer sans gravité (ex. « le modèle réglé n'est pas installé : utilisation de … »).</param>
public sealed record InlineEditOutcome(InlineEditPlan? Plan, string? Problem, string Model = "", string? Note = null)
{
    public bool Succeeded => Plan is not null;

    public static InlineEditOutcome Failure(string problem, string model = "") => new(null, problem, model);
}

public static class InlineEditPlanner
{
    /// <summary>Au-delà, on ne demande pas à un modèle local de réécrire d'un bloc (la fiabilité s'effondre, mesuré avec l'agent) : l'agent, lui, modifie par petites touches.</summary>
    public const int MaxTargetChars = 30_000;

    /// <summary>
    /// Transforme la réponse du modèle en plan applicable, ou en refus expliqué. Ne touche à rien.
    /// Refuse : pas de code, bloc coupé, code abrégé (« ... »), sélection introuvable ou ambiguë, fichier qui ne serait plus valide,
    /// réponse identique à l'existant. Avertit : gros rétrécissement, lignes voisines recopiées, plusieurs blocs.
    /// </summary>
    public static InlineEditOutcome Plan(InlineEditRequest request, string? modelReply, string model = "")
    {
        var blocks = CodeBlocks.Extract(modelReply);
        if (blocks.Count == 0)
            return InlineEditOutcome.Failure(NoCodeMessage(modelReply), model);

        var block = blocks[0];
        if (!block.IsComplete)
            return InlineEditOutcome.Failure(
                "La réponse du modèle a été coupée en plein milieu (elle était trop longue pour lui). Rien n'a été modifié. "
                + "Sélectionne un passage plus court, ou essaie avec un modèle plus grand.", model);

        var warnings = new List<string>();
        if (blocks.Count > 1)
            warnings.Add($"Le modèle a renvoyé {blocks.Count} blocs de code : seul le premier est utilisé.");

        var doc = Normalize(request.DocumentText);
        var crlf = request.DocumentText.Contains("\r\n", StringComparison.Ordinal);
        var replacement = Normalize(block.Code);

        string original; // le texte remplacé : la sélection ou le fichier entier
        string newDoc;
        var index = 0;

        if (request.Scope == InlineEditScope.WholeFile)
        {
            if (replacement.Length == 0)
                return InlineEditOutcome.Failure("Le modèle a renvoyé un bloc de code vide. Rien n'a été modifié.", model);

            original = doc;
            replacement = WithTrailingNewlinesOf(replacement, doc);
            newDoc = replacement;
        }
        else
        {
            var selection = Normalize(request.Selection!);
            var (found, problem) = Locate(doc, selection);
            if (problem is not null) return InlineEditOutcome.Failure(problem, model);

            index = found;
            original = selection;
            replacement = WithTrailingNewlinesOf(replacement, selection);
            newDoc = doc[..index] + replacement + doc[(index + selection.Length)..];

            if (replacement.Trim().Length == 0)
                warnings.Add("Le nouveau code est vide : le passage sélectionné sera SUPPRIMÉ.");
        }

        if (Elisions.Count(replacement) > Elisions.Count(original))
            return InlineEditOutcome.Failure(
                "Le modèle a abrégé son code (des « ... » ou un « reste inchangé » à la place de vrai code) : appliquer cette réponse effacerait ces passages. "
                + "Rien n'a été modifié. Demande-lui le code complet, ou sélectionne un passage plus court.", model);

        if (newDoc == doc)
            return InlineEditOutcome.Failure("Le modèle n'a rien changé : le code qu'il propose est identique à l'actuel. Rien n'a été modifié. "
                + "(Si ta demande était une question, pose-la dans le chat ou utilise « Expliquer ».)", model);

        if (FileSanity.Check(request.DisplayPath, doc, newDoc) is { } broken)
            return InlineEditOutcome.Failure(HumanSanityMessage(request.DisplayPath, broken), model);

        var diff = LineDiff.Compute(doc, newDoc);
        if (!diff.HasChanges)
            return InlineEditOutcome.Failure("Le modèle n'a rien changé d'important : seules des fins de ligne diffèrent. Rien n'a été modifié.", model);

        if (original.Length >= 400 && replacement.Length < original.Length * 0.5)
            warnings.Add($"Le nouveau code ne fait que {100 * replacement.Length / original.Length} % de la taille de l'ancien : vérifie que rien d'important ne disparaît.");

        if (request.Scope == InlineEditScope.Selection && EchoesContext(doc, index, original.Length, replacement))
            warnings.Add("Le modèle semble avoir recopié des lignes situées AUTOUR de la sélection : elles risquent d'apparaître en double (regarde le diff).");

        // Mesuré (24/09) : posée dans le bandeau d'édition, une QUESTION (« quelle est la capitale de la France ? ») ressort d'un modèle 7B
        // sous forme du fichier + un commentaire qui répond. Le diff montre ce commentaire ; cet avertissement dit pourquoi il est là.
        if (diff.Removed == 0 && OnlyCommentLinesAdded(diff.Unified) && !MentionsComments(request.Instruction))
            warnings.Add("Le modèle n'a fait qu'AJOUTER des commentaires alors que ta demande n'en parlait pas : c'était peut-être une question "
                       + "(pour interroger le code, utilise « Expliquer » ou le chat).");

        return new InlineEditOutcome(new InlineEditPlan
        {
            Scope = request.Scope,
            NewText = crlf ? newDoc.Replace("\n", "\r\n") : newDoc,
            Replacement = replacement,
            Diff = diff,
            Warnings = warnings,
        }, null, model);
    }

    /// <summary>
    /// Où se trouve la sélection dans le document. L'éditeur ne donne que le TEXTE sélectionné, pas sa position : s'il apparaît deux fois,
    /// on ne devine pas lequel modifier.
    /// </summary>
    public static (int Index, string? Problem) Locate(string normalizedDocument, string normalizedSelection)
    {
        var first = normalizedDocument.IndexOf(normalizedSelection, StringComparison.Ordinal);
        if (first < 0)
            return (-1, "La sélection ne correspond plus au texte du fichier (il a changé depuis) : sélectionne à nouveau le passage. Rien n'a été modifié.");

        var second = normalizedDocument.IndexOf(normalizedSelection, first + 1, StringComparison.Ordinal);
        if (second >= 0)
            return (-1, "Cette sélection apparaît plusieurs fois dans le fichier : sélectionne un passage un peu plus long pour que je sache lequel modifier. Rien n'a été modifié.");

        return (first, null);
    }

    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    // ── Aides ───────────────────────────────────────────────────────────────

    private static readonly string[] CommentMarkers = { "//", "/*", "*", "#", "--", "<!--" };

    private static readonly string[] CommentWords =
        { "comment", "document", "annot", "explique", "todo", "en-tête", "entete", "header", "licence", "license", "docstring", "summary" };

    /// <summary>Toutes les lignes AJOUTÉES du diff sont des commentaires ou des lignes vides (et il y en a au moins une non vide).</summary>
    private static bool OnlyCommentLinesAdded(string unifiedDiff)
    {
        var any = false;
        foreach (var line in unifiedDiff.Split('\n'))
        {
            if (line.Length == 0 || line[0] != '+' || line.StartsWith("+++", StringComparison.Ordinal)) continue;

            var text = line[1..].Trim();
            if (text.Length == 0) continue;
            if (!CommentMarkers.Any(m => text.StartsWith(m, StringComparison.Ordinal))) return false;
            any = true;
        }
        return any;
    }

    private static bool MentionsComments(string instruction)
        => CommentWords.Any(w => instruction.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>Le bloc du modèle n'a pas de saut de ligne final (nettoyé à l'extraction) : on remet autant de sauts que le texte remplacé en avait.</summary>
    internal static string WithTrailingNewlinesOf(string text, string reference)
    {
        if (text.Trim().Length == 0) return string.Empty; // suppression du passage : sa ligne disparaît avec lui, pas de ligne vide laissée

        var count = 0;
        for (var i = reference.Length - 1; i >= 0 && reference[i] == '\n'; i--) count++;
        return text.TrimEnd('\n') + new string('\n', count);
    }

    private static string NoCodeMessage(string? reply)
    {
        var text = (reply ?? string.Empty).Trim();
        if (text.Length == 0) return "Le modèle n'a rien répondu. Rien n'a été modifié.";

        var excerpt = (text.Length > 160 ? text[..160] + "…" : text).Replace('\n', ' ');
        return $"Le modèle n'a pas renvoyé de code, seulement du texte. Rien n'a été modifié. Sa réponse : « {excerpt} »";
    }

    /// <summary>Les messages de FileSanity s'adressent au modèle (« recopie le passage… ») : on n'en garde que le constat.</summary>
    internal static string HumanSanityMessage(string displayPath, string sanity)
    {
        var text = sanity.StartsWith("Refusé : ", StringComparison.Ordinal) ? sanity["Refusé : ".Length..] : sanity;
        foreach (var marker in new[] { " Le contenu semble", " Recopie", " Ferme chaque balise", " Si tu écris" })
        {
            var cut = text.IndexOf(marker, StringComparison.Ordinal);
            if (cut > 0) text = text[..cut];
        }

        var name = string.IsNullOrWhiteSpace(displayPath) ? "le fichier" : $"« {Path.GetFileName(displayPath)} »";
        return $"Le code proposé casserait {name} : {text.TrimEnd()} Rien n'a été modifié.";
    }

    /// <summary>Le code recopie la ligne juste avant ou juste après la sélection : elle se retrouverait en double.</summary>
    internal static bool EchoesContext(string doc, int index, int length, string replacement)
    {
        var lines = replacement.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count < 2) return false;

        var before = doc[..index].Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0);
        var after = doc[(index + length)..].Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

        return (before is { Length: >= 12 } && lines[0] == before)
            || (after is { Length: >= 12 } && lines[^1] == after);
    }
}

/// <summary>
/// Repère les lignes « abrégées » que les modèles écrivent à la place du vrai code : « // ... », « # ... », « // reste du code inchangé »,
/// « // ... existing code ... ». Les appliquer effacerait ce qu'elles remplacent.
/// </summary>
internal static class Elisions
{
    private static readonly Regex Bare = new(
        @"^\s*(?:(?://|#|--|;|/\*|<!--)\s*)?(?:\.{3}|…)\s*(?:\*/|-->)?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Phrase = new(
        @"^\s*(?://|#|--|/\*|<!--)[^\r\n]{0,90}?(?:rest of (?:the )?(?:code|file|class|method|function)|remaining code|existing code|unchanged|reste du (?:code|fichier)|le reste (?:du|de)|code existant|inchang[ée]e?s?|same as before|comme avant|(?:autres|other) (?:m[ée]thodes|methods|membres|members|fonctions|functions|cas|cases))[^\r\n]{0,60}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static int Count(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
            if (Bare.IsMatch(line) || Phrase.IsMatch(line)) count++;
        return count;
    }
}
