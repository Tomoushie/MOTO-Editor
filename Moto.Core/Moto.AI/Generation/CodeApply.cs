// Moto.Core/Moto.AI/Generation/CodeApply.cs
// ★ AJOUT (25/09, « Appliquer » dans le chat — brique 3 de l'écriture générative) : OÙ poser, dans le fichier affiché, un bloc de code écrit
// par le chat. Jusqu'ici le seul geste possible était « Copier » puis coller à la main — et pour quelqu'un qui ne code pas, trouver le bon
// endroit (sans laisser l'ancienne version en double) est justement la partie difficile.
// Règles, dans l'ordre :
//  1. une SÉLECTION dans l'éditeur est remplacée : c'est l'utilisateur qui a désigné l'endroit ;
//  2. un bloc qui reprend l'essentiel du fichier le REMPLACE EN ENTIER ;
//  3. en C#/Java, une méthode, propriété ou classe qui existe déjà (même en-tête, ou même nom sans ambiguïté) est remplacée À SA PLACE ;
//  4. sinon le code est AJOUTÉ : au curseur — ou, pour une méthode que le curseur placerait mal (dans une autre méthode, hors de la classe),
//     à la fin de la classe. Des « using » en tête du bloc vont en haut du fichier.
// En cas de doute, on ne devine pas : on dit quoi faire (« clique à l'endroit voulu… »). Ce code n'APPLIQUE rien : il prépare un plan (texte
// final, diff, phrase qui dit où, avertissements) ou explique pourquoi il refuse — l'humain voit le diff et décide.
using System.Text;
using System.Text.RegularExpressions;
using Moto.Core.AI.Autonomy.V2;
using StructureBlock = Moto.Core.AI.Autonomy.V2.CodeBlock;

namespace Moto.Core.AI.Generation;

public enum CodeApplyKind
{
    /// <summary>Le passage sélectionné dans l'éditeur est remplacé.</summary>
    ReplaceSelection,

    /// <summary>Le fichier entier est remplacé (le bloc en reprend l'essentiel, ou le fichier était vide).</summary>
    ReplaceFile,

    /// <summary>Une méthode, propriété, champ ou classe existant (C#/Java) est remplacé à sa place.</summary>
    ReplaceMember,

    /// <summary>Le code est ajouté à la fin d'une classe (C#/Java).</summary>
    AppendToType,

    /// <summary>Le code est ajouté là où est le curseur.</summary>
    InsertAtCursor,

    /// <summary>Le bloc ne contient que des directives « using » : elles sont ajoutées en haut du fichier.</summary>
    AddUsings,
}

public sealed class CodeApplyRequest
{
    /// <summary>Chemin (ou nom) du fichier affiché : sert au langage et aux contrôles de validité.</summary>
    public string DisplayPath { get; init; } = string.Empty;

    /// <summary>Le texte de l'onglet tel que l'utilisateur le voit.</summary>
    public string DocumentText { get; init; } = string.Empty;

    /// <summary>Le texte sélectionné dans l'éditeur (null ou blanc = pas de sélection).</summary>
    public string? Selection { get; init; }

    /// <summary>Position de la sélection dans le texte aux sauts de ligne « \n », si l'éditeur la connaît de façon sûre.</summary>
    public int? SelectionStart { get; init; }

    /// <summary>Position du curseur dans le texte aux sauts de ligne « \n », ou null si elle est inconnue (éditeur jamais cliqué…).</summary>
    public int? CaretIndex { get; init; }

    /// <summary>Le code du bloc.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Nom de fichier annoncé par le chat pour ce bloc, sinon null.</summary>
    public string? PathHint { get; init; }

    /// <summary>Faux si la réponse s'est arrêtée au milieu du bloc.</summary>
    public bool IsComplete { get; init; } = true;
}

public sealed class CodeApplyPlan
{
    public CodeApplyKind Kind { get; init; }

    /// <summary>Le texte COMPLET du fichier après application (fins de ligne du fichier d'origine).</summary>
    public string NewText { get; init; } = string.Empty;

    public DiffResult Diff { get; init; } = new(string.Empty, 0, 0);

    /// <summary>Où va le code, en clair : « Remplacer la méthode « Foo » (lignes 12 à 30) ».</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Points à vérifier avant d'accepter (le plan reste applicable).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>Première ligne modifiée (base 1).</summary>
    public int FirstLine { get; init; }
}

/// <param name="Plan">Le plan, ou null si <paramref name="Problem"/> explique pourquoi rien n'est proposé.</param>
/// <param name="Problem">Message lisible par l'utilisateur.</param>
public sealed record CodeApplyOutcome(CodeApplyPlan? Plan, string? Problem)
{
    public bool Succeeded => Plan is not null;

    public static CodeApplyOutcome Failure(string problem) => new(null, problem);
}

public static class CodeApplyPlanner
{
    /// <summary>Part des lignes du fichier (celles qui portent du texte) qu'un bloc doit reprendre pour être pris pour le fichier entier.</summary>
    internal const double WholeFileShare = 0.6;

    /// <summary>Lignes au plus entre l'en-tête d'une déclaration et son « { » (paramètres sur plusieurs lignes, contraintes where…).</summary>
    private const int MaxSignatureLines = 20;

    public static CodeApplyOutcome Plan(CodeApplyRequest request)
    {
        if (!request.IsComplete)
            return CodeApplyOutcome.Failure("Ce bloc de code est incomplet : la réponse du chat a été coupée avant sa fin. Rien n'a été modifié. "
                + "Demande-lui « continue », ou une version plus courte.");

        var codeLines = CleanLines(request.Code);
        if (codeLines.Count == 0)
            return CodeApplyOutcome.Failure("Ce bloc de code est vide. Rien n'a été modifié.");

        var c = new Context(request, codeLines);

        if (HintedOtherFile(request.PathHint, request.DisplayPath) is { } other)
            c.Warnings.Add($"Le chat a écrit ce code pour « {other} », mais le fichier affiché est « {c.Name} ».");

        var (placement, problem) =
            !string.IsNullOrWhiteSpace(request.Selection) ? PlaceOnSelection(c)
            : c.Doc.Trim().Length == 0 ? (FillEmpty(c), null)
            : PlaceInFile(c);

        return placement is null
            ? CodeApplyOutcome.Failure(problem ?? "Je ne sais pas où poser ce code. Rien n'a été modifié.")
            : Finish(c, placement);
    }

    // ── 1. Sélection ────────────────────────────────────────────────────────

    private static (Placement?, string?) PlaceOnSelection(Context c)
    {
        // Des « using » en tête du bloc vont en haut du fichier, pas à la place de la sélection.
        var codeLines = c.CodeLines;
        var usings = new List<string>();
        if (c.IsCSharp) (usings, codeLines) = SplitLeadingUsings(codeLines);
        if (codeLines.Count == 0) return AddUsingsOnly(c, usings);

        var sel = InlineEditPlanner.Normalize(c.Request.Selection!);
        int index;
        if (c.Request.SelectionStart is int s && s >= 0 && s + sel.Length <= c.Doc.Length && string.CompareOrdinal(c.Doc, s, sel, 0, sel.Length) == 0)
        {
            index = s; // position donnée par l'éditeur : pas d'ambiguïté même si le même texte apparaît ailleurs
        }
        else
        {
            var (found, problem) = InlineEditPlanner.Locate(c.Doc, sel);
            if (problem is not null) return (null, problem);
            index = found;
        }

        var line = c.LineOf(index);
        var before = c.Doc[c.Offset(line)..index]; // ce qui précède la sélection sur sa première ligne
        var lines = Reindent(codeLines, LeadingWhitespace(c.Lines[line]));
        lines[0] = before.Trim().Length == 0
            ? lines[0][Math.Min(before.Length, LeadingWhitespace(lines[0]).Length)..] // l'indentation déjà présente avant la sélection reste
            : lines[0].TrimStart();                                                   // sélection en milieu de ligne
        var replacement = InlineEditPlanner.WithTrailingNewlinesOf(string.Join("\n", lines), sel);

        if (InlineEditPlanner.EchoesContext(c.Doc, index, sel.Length, replacement))
            c.Warnings.Add("Ce code semble reprendre des lignes situées AUTOUR de la sélection : elles risquent d'apparaître en double (regarde le diff).");

        var last = c.LineOf(index + Math.Max(0, sel.TrimEnd('\n').Length - 1));
        var placement = new Placement(CodeApplyKind.ReplaceSelection, new[] { new Edit(index, sel.Length, replacement) },
            $"Remplacer la sélection ({LineRange(line, last)})", sel, replacement);
        return (WithUsings(c, placement, usings), null);
    }

    // ── 2. Fichier entier ───────────────────────────────────────────────────

    private static Placement FillEmpty(Context c)
    {
        var text = c.Code + "\n";
        return new Placement(CodeApplyKind.ReplaceFile, new[] { new Edit(0, c.Doc.Length, text) },
            $"Écrire ce code dans « {c.Name} », qui est vide", c.Doc, text);
    }

    /// <summary>Le bloc reprend au moins <see cref="WholeFileShare"/> des lignes porteuses de texte du fichier (indentation ignorée).</summary>
    internal static bool LooksLikeWholeFile(string doc, string code)
    {
        var docLines = SignificantLines(doc);
        return docLines.Count > 0 && CommonCount(docLines, SignificantLines(code)) >= WholeFileShare * docLines.Count;
    }

    private static Placement WholeFile(Context c)
    {
        var docLines = SignificantLines(c.Doc);
        var codeLines = SignificantLines(c.Code);
        var codeSet = new HashSet<string>(codeLines, StringComparer.Ordinal);

        // Le risque : un bloc qui reprend presque tout le fichier… sauf son début (using, namespace) ou sa fin.
        if (!codeSet.Contains(docLines[0]))
            c.Warnings.Add($"Ce code ne reprend pas le début du fichier (« {Shorten(docLines[0])} ») : le remplacer en entier supprimera ce début. Regarde le diff.");
        if (docLines.Count > 1 && !codeSet.Contains(docLines[^1]))
            c.Warnings.Add($"Ce code ne reprend pas la fin du fichier (« {Shorten(docLines[^1])} ») : le remplacer en entier supprimera cette fin. Regarde le diff.");

        // C#/Java seulement : dans un texte ordinaire, « Voici la fonction (exemple) » ressemblerait à une méthode « fonction ».
        if (c.IsBraceLanguage && DroppedDeclarations(c.Lines, c.CodeLines) is { Count: > 0 } dropped)
            c.Warnings.Add(DroppedWarning(dropped));

        var missing = docLines.Count - CommonCount(docLines, codeLines);
        if (missing >= Math.Max(3, docLines.Count / 10))
            c.Warnings.Add($"{missing} ligne(s) du fichier actuel ne se retrouvent pas à l'identique dans ce code : elles seront supprimées ou modifiées. Regarde le diff avant d'accepter.");

        var text = c.Code + (c.Doc.EndsWith('\n') ? "\n" : string.Empty);
        return new Placement(CodeApplyKind.ReplaceFile, new[] { new Edit(0, c.Doc.Length, text) },
            $"Remplacer tout le fichier « {c.Name} »", c.Doc, text);
    }

    // ── 3 et 4. Membre existant, ou ajout ───────────────────────────────────

    private static (Placement?, string?) PlaceInFile(Context c)
    {
        var codeLines = c.CodeLines;
        var usings = new List<string>();
        if (c.IsCSharp) (usings, codeLines) = SplitLeadingUsings(codeLines);

        if (codeLines.Count == 0) return AddUsingsOnly(c, usings);

        // Une déclaration qui existe déjà passe AVANT le « fichier entier » : une classe réécrite sans ses using ni son namespace remplace la
        // classe, pas le fichier (sinon ce début disparaîtrait).
        var main = c.IsBraceLanguage ? ReplaceMember(c, codeLines) : null;
        if (main is null && LooksLikeWholeFile(c.Doc, c.Code)) return (WholeFile(c), null);
        if (main is null)
        {
            var (inserted, problem) = Insert(c, codeLines);
            if (inserted is null) return (null, problem);
            main = inserted;
        }

        return (WithUsings(c, main, usings), null);
    }

    /// <summary>En C#/Java : le bloc est UNE déclaration qui existe déjà dans le fichier → elle est remplacée à sa place, sinon null.</summary>
    private static Placement? ReplaceMember(Context c, List<string> codeLines)
    {
        if (c.Blocks is null) return null; // fichier que l'analyse ne comprend pas (accolades déséquilibrées…) : on ne remplace rien
        var head = HeadIndex(codeLines);
        if (head < 0 || !IsSingleDeclaration(codeLines, head)) return null;

        var declaration = Declaration.Parse(codeLines[head].Trim());
        var anchor = FindAnchor(c, codeLines[head], declaration);
        if (anchor < 0) return null;

        if (MemberRegion(c.Blocks, c.Lines, anchor) is not { } region) return null;
        var (start, end) = region;

        // Le bloc apporte ses propres commentaires /// (ou attributs [..]) : ceux du fichier, juste au-dessus, sont remplacés (sinon doublon) —
        // mais seulement la même sorte : un attribut du fichier n'est jamais emporté par un bloc qui n'a que des ///, il disparaîtrait.
        // Un autre commentaire du fichier (« // », « /* ») n'est emporté que si le bloc le reprend (ce peut être un titre de section).
        var leading = new HashSet<string>(codeLines.Take(head).Select(l => l.Trim()).Where(l => l.Length > 0), StringComparer.Ordinal);
        var bringsDocComment = leading.Any(l => l.StartsWith("///", StringComparison.Ordinal));
        var bringsAttribute = leading.Any(l => l.StartsWith('['));
        while (leading.Count > 0 && start > 0 && c.Lines[start - 1].Trim() is var above && IsCommentOrAttribute(above)
               && (above.StartsWith("///", StringComparison.Ordinal) ? bringsDocComment
                   : above.StartsWith('[') ? bringsAttribute
                   : leading.Contains(above)))
            start--;

        // Une classe ou un namespace réécrit en entier : ce qui n'y est plus disparaîtra — le dire en clair, le diff seul ne suffit pas.
        if (declaration is { IsType: true } && DroppedDeclarations(c.Lines[start..(end + 1)], codeLines) is { Count: > 0 } dropped)
            c.Warnings.Add(DroppedWarning(dropped));

        var lines = Reindent(codeLines, LeadingWhitespace(c.Lines[anchor]));
        var what = declaration is null ? "le passage correspondant" : $"{declaration.Label} « {declaration.Name} »";
        return new Placement(CodeApplyKind.ReplaceMember, new[] { c.ReplaceLines(start, end, lines) },
            $"Remplacer {what} ({LineRange(start, end)})",
            string.Join("\n", c.Lines[start..(end + 1)]), string.Join("\n", lines));
    }

    /// <summary>La ligne du fichier qui porte le même en-tête que le bloc (une seule), sinon la seule déclaration du même nom, sinon -1.</summary>
    private static int FindAnchor(Context c, string headLine, Declaration? declaration)
    {
        var key = HeadKey(headLine);
        if (key.Length >= 6)
        {
            var exact = new List<int>();
            for (var i = 0; i < c.RealLineCount; i++)
                if (HeadKey(c.Lines[i]) == key) exact.Add(i);
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1) return -1; // même en-tête à plusieurs endroits : on ne devine pas lequel
        }

        if (declaration is null) return -1;
        var byName = c.DeclarationsNamed(declaration);
        return byName.Count == 1 ? byName[0] : -1; // surcharges (plusieurs Foo(…)) : on ne devine pas non plus
    }

    /// <summary>L'étendue (lignes, base 0) de la déclaration qui commence à <paramref name="anchor"/> : jusqu'à son « } », ou sa ligne finie par « ; ».</summary>
    private static (int Start, int End)? MemberRegion(IReadOnlyList<StructureBlock> blocks, IReadOnlyList<string> lines, int anchor)
    {
        var (body, semicolonLine) = BodyAfter(blocks, lines, anchor);
        if (semicolonLine >= 0) return (anchor, semicolonLine);
        if (body is null) return null;

        var end = body.CloseLine - 1;
        return ClosingLineIsClean(lines[end]) ? (anchor, end) : null; // « } else { » : ce n'est pas la fin d'une déclaration
    }

    /// <summary>
    /// Le corps « { … } » qui suit la ligne <paramref name="from"/> (le plus englobant s'il en commence plusieurs sur la même ligne), ou, si une
    /// ligne finie par « ; » arrive avant toute accolade, le numéro de cette ligne (déclaration sur une ligne, membre « => … ; »).
    /// </summary>
    private static (StructureBlock? Body, int SemicolonLine) BodyAfter(IReadOnlyList<StructureBlock> blocks, IReadOnlyList<string> lines, int from)
    {
        for (var j = from; j < lines.Count && j <= from + MaxSignatureLines; j++)
        {
            var opening = blocks.Where(b => b.OpenLine == j + 1).OrderByDescending(b => b.CloseLine).FirstOrDefault();
            if (opening is not null) return (opening, -1);
            if (StripLineComment(lines[j]).TrimEnd().EndsWith(';')) return (null, j);
        }
        return (null, -1);
    }

    /// <summary>Le bloc est UNE déclaration : une seule ligne de code, ou un en-tête dont le corps « { … } » se ferme sur la dernière ligne de code.</summary>
    private static bool IsSingleDeclaration(List<string> lines, int head)
    {
        var last = LastCodeIndex(lines);
        if (last == head) return true;

        var blocks = CodeStructure.Scan(lines);
        if (blocks is null) return false;
        var (body, semicolonLine) = BodyAfter(blocks, lines, head);
        return semicolonLine < 0 && body is not null && body.CloseLine - 1 == last && ClosingLineIsClean(lines[last]);
    }

    /// <summary>Ajout du code : au curseur si l'endroit convient, sinon (méthode, classe) à la fin de la classe, sinon on demande où.</summary>
    private static (Placement?, string?) Insert(Context c, List<string> codeLines)
    {
        var head = HeadIndex(codeLines);
        var declaration = head >= 0 && c.IsBraceLanguage ? Declaration.Parse(codeLines[head].Trim()) : null;

        var existing = declaration is null ? 0 : c.DeclarationsNamed(declaration).Count;
        if (existing > 0)
            c.Warnings.Add($"« {declaration!.Name} » existe déjà dans ce fichier{(existing > 1 ? $" ({existing} fois)" : string.Empty)} : ce code sera AJOUTÉ à côté, "
                         + "pas à sa place — il y aura deux versions. Pour remplacer l'ancienne, sélectionne-la dans l'éditeur, puis reclique sur Appliquer.");

        int? caretLine = c.Request.CaretIndex is int caret && caret >= 0 && caret <= c.Doc.Length ? InsertionLine(c, caret) : null;
        var text = string.Join("\n", codeLines);

        if (declaration is { IsType: false } && c.Blocks is not null)
        {
            // Une méthode, une propriété… : au curseur seulement s'il est ENTRE deux membres d'une classe (pas dans une méthode, pas entre une
            // signature et son « { » : garde-fou de l'agent), sinon en fin de classe.
            if (caretLine is int at && IsDirectlyInType(c.Blocks, at) && InsertionGuard.Check(c.Request.DisplayPath, c.Lines, at + 1, text) is null)
                return (InsertLines(c, at, codeLines, separate: true, CodeApplyKind.InsertAtCursor, $"Insérer à la ligne {at + 1}, là où est ton curseur"), null);

            if (TargetType(c.Blocks, caretLine) is { } type && AppendToType(c, type, codeLines) is { } appended)
            {
                if (caretLine is int refused)
                    c.Warnings.Add($"Ton curseur (ligne {refused + 1}) est à un endroit où ce code ne peut pas aller (dans une autre méthode, ou hors de la classe) : "
                                 + $"je l'ajoute plutôt à la fin de la classe « {type.Name} ».");
                return (appended, null);
            }
        }
        else if (declaration is { IsType: true } && c.Blocks is not null)
        {
            // Une classe : au curseur s'il n'est dans aucune méthode, sinon à la fin du fichier (ou de son namespace).
            if (caretLine is int at && IsTopLevelSpot(c.Blocks, at))
                return (InsertLines(c, at, codeLines, separate: true, CodeApplyKind.InsertAtCursor, $"Insérer à la ligne {at + 1}, là où est ton curseur"), null);

            // Une classe va à la fin du namespace du fichier ; un namespace, à la fin du fichier (pas dans l'autre namespace).
            var ns = declaration.Category == "namespace" ? null
                : c.Blocks.Where(b => b.Kind == BlockKind.Namespace && b.CloseLine > b.OpenLine).OrderByDescending(b => b.CloseLine - b.OpenLine).FirstOrDefault();
            var endLine = ns is null ? c.RealLineCount : ns.CloseLine - 1;
            return (InsertLines(c, endLine, codeLines, separate: true, CodeApplyKind.InsertAtCursor,
                $"Ajouter {declaration.Label} « {declaration.Name} » à la fin du fichier"), null);
        }

        if (caretLine is int line)
            return (InsertLines(c, line, codeLines, separate: false, CodeApplyKind.InsertAtCursor, $"Insérer à la ligne {line + 1}, là où est ton curseur"), null);

        return (null, $"Je ne sais pas où poser ce code dans « {c.Name} » : clique dans le fichier à l'endroit voulu (ou sélectionne le passage à remplacer), "
                    + "puis reclique sur Appliquer. Rien n'a été modifié.");
    }

    /// <summary>
    /// La ligne (base 0) AVANT laquelle le code s'insère, d'après le curseur : sur une ligne vide ou en début de ligne → avant elle ;
    /// en milieu ou en fin de ligne → après elle (on ne coupe jamais une ligne en deux).
    /// </summary>
    private static int InsertionLine(Context c, int caret)
    {
        var line = c.LineOf(caret);
        var text = c.Lines[line];
        var col = Math.Min(caret - c.Offset(line), text.Length);
        return text[..col].Trim().Length == 0 ? line : line + 1;
    }

    /// <summary>La classe qui contient le curseur (la plus intérieure), sinon la plus grande du fichier.</summary>
    private static StructureBlock? TargetType(IReadOnlyList<StructureBlock> blocks, int? caretLine)
    {
        var types = blocks.Where(b => b.Kind == BlockKind.Type && b.CloseLine > b.OpenLine).ToList();
        if (types.Count == 0) return null;

        if (caretLine is int at && types.Where(b => b.OpenLine < at + 1 && at + 1 <= b.CloseLine).OrderByDescending(b => b.OpenLine).FirstOrDefault() is { } inner)
            return inner;
        return types.OrderByDescending(b => b.CloseLine - b.OpenLine).First();
    }

    /// <summary>Le point d'insertion n'est dans aucun bloc, ou directement dans un namespace ou une classe (pas dans une méthode).</summary>
    private static bool IsTopLevelSpot(IReadOnlyList<StructureBlock> blocks, int at)
        => InnermostAt(blocks, at) is not { } inner || inner.Kind is BlockKind.Namespace or BlockKind.Type;

    /// <summary>Le point d'insertion est directement dans une classe (entre ses membres), pas dans une méthode ni hors de toute classe.</summary>
    private static bool IsDirectlyInType(IReadOnlyList<StructureBlock> blocks, int at)
        => InnermostAt(blocks, at) is { Kind: BlockKind.Type };

    /// <summary>Le bloc le plus intérieur qui contient le point d'insertion « avant la ligne <paramref name="at"/> » (base 0).</summary>
    private static StructureBlock? InnermostAt(IReadOnlyList<StructureBlock> blocks, int at)
        => blocks.Where(b => b.OpenLine < at + 1 && at + 1 <= b.CloseLine).OrderByDescending(b => b.OpenLine).FirstOrDefault();

    private static Placement? AppendToType(Context c, StructureBlock type, List<string> codeLines)
    {
        var close = type.CloseLine - 1; // la ligne de l'accolade fermante : on insère juste avant
        if (close <= type.OpenLine - 1) return null;

        var lines = Reindent(codeLines, MemberIndent(c, type));
        var previous = c.Lines[close - 1].Trim();
        if (previous.Length > 0 && !previous.EndsWith('{')) lines.Insert(0, string.Empty); // une ligne vide entre le dernier membre et le nouveau

        return new Placement(CodeApplyKind.AppendToType, new[] { c.InsertBefore(close, lines) },
            $"Ajouter à la fin de la classe « {type.Name} » (avant la ligne {type.CloseLine})", string.Empty, string.Join("\n", lines));
    }

    /// <summary>L'indentation des membres de la classe (celle de sa première ligne de contenu), ou celle de son « } » plus un cran si elle est vide.</summary>
    private static string MemberIndent(Context c, StructureBlock type)
    {
        for (var i = type.OpenLine; i < type.CloseLine - 1; i++)
            if (c.Lines[i].Trim().Length > 0) return LeadingWhitespace(c.Lines[i]);
        return LeadingWhitespace(c.Lines[type.CloseLine - 1]) + c.IndentUnit;
    }

    private static Placement InsertLines(Context c, int at, List<string> codeLines, bool separate, CodeApplyKind kind, string description)
    {
        var lines = Reindent(codeLines, IndentForInsertion(c, at));
        if (separate)
        {
            if (at > 0 && c.Lines[at - 1].Trim() is { Length: > 0 } previous && !previous.EndsWith('{')) lines.Insert(0, string.Empty);
            if (at < c.RealLineCount && c.Lines[at].Trim() is { Length: > 0 } next && !next.StartsWith('}')) lines.Add(string.Empty);
        }
        return new Placement(kind, new[] { c.InsertBefore(at, lines) }, description, string.Empty, string.Join("\n", lines));
    }

    /// <summary>L'indentation d'un code inséré avant la ligne <paramref name="at"/> : celle de cette ligne, sinon de la précédente (un cran de plus si elle ouvre un bloc).</summary>
    private static string IndentForInsertion(Context c, int at)
    {
        if (at < c.RealLineCount && c.Lines[at].Trim() is { Length: > 0 } here && !here.StartsWith('}') && !here.StartsWith("</", StringComparison.Ordinal))
            return LeadingWhitespace(c.Lines[at]);

        for (var i = at - 1; i >= 0; i--)
        {
            var t = c.Lines[i].Trim();
            if (t.Length == 0) continue;
            var ws = LeadingWhitespace(c.Lines[i]);
            return OpensBlock(t) ? ws + c.IndentUnit : ws;
        }
        return string.Empty;
    }

    private static bool OpensBlock(string trimmed)
        => trimmed.EndsWith('{') || trimmed.EndsWith(':')
        || (trimmed.StartsWith('<') && !trimmed.StartsWith("</", StringComparison.Ordinal) && !trimmed.StartsWith("<!--", StringComparison.Ordinal)
            && !trimmed.StartsWith("<?", StringComparison.Ordinal) && trimmed.EndsWith('>') && !trimmed.EndsWith("/>", StringComparison.Ordinal)
            && !trimmed.Contains("</", StringComparison.Ordinal));

    // ── « using » (C#) ──────────────────────────────────────────────────────

    private static readonly Regex UsingDirective = new(
        @"^\s*(?:global\s+)?using\s+(?:static\s+)?[\w.]+(?:\s*=\s*[\w.<>,\s]+)?\s*;\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Les « using » en tête du bloc, et le reste du code.</summary>
    private static (List<string> Usings, List<string> Code) SplitLeadingUsings(List<string> lines)
    {
        var usings = new List<string>();
        var i = 0;
        for (; i < lines.Count; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            if (!UsingDirective.IsMatch(lines[i])) break;
            usings.Add(lines[i].Trim());
        }
        return usings.Count == 0 ? (usings, lines) : (usings, lines.Skip(i).ToList());
    }

    private static (Placement?, string?) AddUsingsOnly(Context c, List<string> usings)
    {
        var missing = MissingUsings(c, usings);
        if (missing.Count == 0) return (null, "Ces directives « using » sont déjà en haut du fichier : rien à modifier.");

        var edit = UsingsEdit(c, missing, out var inserted);
        return (new Placement(CodeApplyKind.AddUsings, new[] { edit }, $"Ajouter {Quoted(missing)} en haut du fichier", string.Empty, inserted), null);
    }

    private static Placement WithUsings(Context c, Placement main, List<string> usings)
    {
        var missing = MissingUsings(c, usings);
        if (missing.Count == 0) return main;

        var edit = UsingsEdit(c, missing, out var inserted);
        return main with
        {
            Edits = main.Edits.Append(edit).ToList(), // appliqué après le reste : à égalité de position, les using passent devant
            Description = $"{main.Description} — et ajouter {Quoted(missing)} en haut du fichier",
            Inserted = main.Inserted + "\n" + inserted,
        };
    }

    private static List<string> MissingUsings(Context c, List<string> usings)
    {
        var present = new HashSet<string>(c.Lines.Select(l => CollapseWhitespace(l.Trim())), StringComparer.Ordinal);
        return usings.Select(CollapseWhitespace).Where(u => !present.Contains(u)).Distinct().ToList();
    }

    /// <summary>Juste après le dernier « using » du haut du fichier ; sans « using », avant la première ligne de code (après l'en-tête en commentaire).</summary>
    private static Edit UsingsEdit(Context c, List<string> missing, out string inserted)
    {
        var lastUsing = -1;
        var firstCode = -1;
        for (var i = 0; i < c.RealLineCount; i++)
        {
            var t = c.Lines[i].Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith('*')) continue;
            if (UsingDirective.IsMatch(c.Lines[i])) { lastUsing = i; continue; }
            firstCode = i;
            break;
        }

        var lines = new List<string>(missing);
        int at;
        if (lastUsing >= 0) at = lastUsing + 1;
        else
        {
            at = firstCode >= 0 ? firstCode : c.RealLineCount;
            if (at < c.RealLineCount) lines.Add(string.Empty); // une ligne vide entre les using et le code
        }

        inserted = string.Join("\n", lines);
        return c.InsertBefore(at, lines);
    }

    private static string Quoted(List<string> usings) => string.Join(", ", usings.Select(u => $"« {u} »"));

    // ── Fin : contrôles communs ─────────────────────────────────────────────

    private static CodeApplyOutcome Finish(Context c, Placement placement)
    {
        var newDoc = ApplyEdits(c.Doc, placement.Edits);
        if (newDoc == c.Doc)
            return CodeApplyOutcome.Failure("Ce code est déjà dans le fichier, à l'identique : rien à modifier.");

        if (Elisions.Count(placement.Inserted) > Elisions.Count(placement.Replaced))
            return CodeApplyOutcome.Failure("Ce code est abrégé (des « ... » ou un « reste du code inchangé » à la place de vrai code) : le poser effacerait "
                + "ce qu'il cache. Rien n'a été modifié. Demande au chat le code COMPLET.");

        if (FileSanity.Check(c.Request.DisplayPath, c.Doc, newDoc) is { } broken)
            return CodeApplyOutcome.Failure(InlineEditPlanner.HumanSanityMessage(c.Request.DisplayPath, broken));

        var diff = LineDiff.Compute(c.Doc, newDoc);
        if (!diff.HasChanges)
            return CodeApplyOutcome.Failure("Seules des fins de ligne diffèrent : rien à modifier.");

        if (placement.Replaced.Length >= 400 && placement.Inserted.Length < placement.Replaced.Length * 0.5)
            c.Warnings.Add($"Le nouveau code ne fait que {100 * placement.Inserted.Length / placement.Replaced.Length} % de la taille de ce qu'il remplace : "
                         + "vérifie que rien d'important ne disparaît.");

        var crlf = c.Request.DocumentText.Contains("\r\n", StringComparison.Ordinal);
        return new CodeApplyOutcome(new CodeApplyPlan
        {
            Kind = placement.Kind,
            NewText = crlf ? newDoc.Replace("\n", "\r\n") : newDoc,
            Diff = diff,
            Description = placement.Description,
            Warnings = c.Warnings,
            FirstLine = c.LineOf(placement.Edits.Min(e => e.Start)) + 1,
        }, null);
    }

    /// <summary>Applique les modifications de la dernière à la première ; à position égale, celle listée plus tard passe devant.</summary>
    private static string ApplyEdits(string doc, IReadOnlyList<Edit> edits)
    {
        var sb = new StringBuilder(doc);
        foreach (var e in edits.Select((e, i) => (e, i)).OrderByDescending(x => x.e.Start).ThenBy(x => x.i).Select(x => x.e))
            sb.Remove(e.Start, e.Length).Insert(e.Start, e.Text);
        return sb.ToString();
    }

    // ── Aides ───────────────────────────────────────────────────────────────

    /// <summary>Lignes du code, sans les lignes vides du début et de la fin ni les espaces en bout de ligne ; l'indentation est gardée.</summary>
    private static List<string> CleanLines(string code)
    {
        var lines = InlineEditPlanner.Normalize(code).Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    /// <summary>Le nom de fichier annoncé par le chat, s'il désigne un AUTRE fichier que celui affiché.</summary>
    private static string? HintedOtherFile(string? hint, string displayPath)
    {
        if (string.IsNullOrWhiteSpace(hint)) return null;
        var hinted = Path.GetFileName(hint.Replace('\\', '/').TrimEnd('/'));
        return hinted.Length > 0 && !string.Equals(hinted, Path.GetFileName(displayPath), StringComparison.OrdinalIgnoreCase) ? hinted : null;
    }

    /// <summary>Les lignes qui portent du texte (lettre ou chiffre), sans leur indentation — les « { », « } » et lignes vides ne comptent pas.</summary>
    private static List<string> SignificantLines(string text)
        => text.Split('\n').Select(l => l.Trim()).Where(l => l.Any(char.IsLetterOrDigit)).ToList();

    /// <summary>Nombre de lignes de <paramref name="a"/> qu'on retrouve dans <paramref name="b"/> (chaque ligne de b ne sert qu'une fois).</summary>
    private static int CommonCount(List<string> a, List<string> b)
    {
        var pool = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in b) pool[line] = pool.TryGetValue(line, out var n) ? n + 1 : 1;

        var common = 0;
        foreach (var line in a)
        {
            if (!pool.TryGetValue(line, out var n) || n == 0) continue;
            pool[line] = n - 1;
            common++;
        }
        return common;
    }

    private static string LeadingWhitespace(string line)
    {
        var n = 0;
        while (n < line.Length && (line[n] == ' ' || line[n] == '\t')) n++;
        return line[..n];
    }

    /// <summary>Les lignes décalées pour que leur indentation commune devienne <paramref name="indent"/> (les écarts entre elles sont gardés).</summary>
    private static List<string> Reindent(IReadOnlyList<string> lines, string indent)
    {
        string? common = null;
        foreach (var line in lines)
        {
            if (line.Trim().Length == 0) continue;
            var ws = LeadingWhitespace(line);
            common = common is null ? ws : CommonPrefix(common, ws);
        }
        var cut = common?.Length ?? 0;
        return lines.Select(l => l.Trim().Length == 0 ? string.Empty : indent + l[cut..]).ToList();
    }

    private static string CommonPrefix(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
        return a[..n];
    }

    private static string CollapseWhitespace(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    private static string Shorten(string s) => s.Length <= 60 ? s : s[..60] + "…";

    /// <summary>Les noms déclarés (méthodes, propriétés, champs, classes…) dans <paramref name="before"/> qu'on ne retrouve plus dans <paramref name="after"/>.</summary>
    private static List<string> DroppedDeclarations(IEnumerable<string> before, IEnumerable<string> after)
    {
        var kept = new HashSet<string>(after.Select(l => Declaration.Parse(l.Trim())?.Name).OfType<string>(), StringComparer.Ordinal);
        return before.Select(l => Declaration.Parse(l.Trim())?.Name).OfType<string>().Where(n => !kept.Contains(n)).Distinct().ToList();
    }

    private static string DroppedWarning(List<string> names)
    {
        var list = string.Join(", ", names.Take(5).Select(n => $"« {n} »")) + (names.Count > 5 ? $" et {names.Count - 5} autre(s)" : string.Empty);
        return $"Attention : ce code ne contient plus {list} — {(names.Count > 1 ? "ils disparaîtront" : "il disparaîtra")} du fichier si tu acceptes.";
    }

    /// <summary>« ligne 12 » ou « lignes 12 à 30 », d'après des numéros de ligne en base 0.</summary>
    private static string LineRange(int first, int last) => first == last ? $"ligne {first + 1}" : $"lignes {first + 1} à {last + 1}";

    /// <summary>L'en-tête d'une ligne, pour comparer : espaces réduits, sans « { » final ni commentaire de fin de ligne.</summary>
    private static string HeadKey(string line)
    {
        var key = CollapseWhitespace(StripLineComment(line));
        return key.EndsWith('{') ? key[..^1].TrimEnd() : key;
    }

    private static bool IsCommentOrAttribute(string trimmed)
        => trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal) || trimmed.StartsWith('*')
        || (trimmed.StartsWith('[') && trimmed.EndsWith(']'));

    /// <summary>La première ligne de code (ni vide, ni commentaire, ni attribut seul sur sa ligne, ni directive #), ou -1.</summary>
    private static int HeadIndex(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0 || IsCommentOrAttribute(t) || t.StartsWith('#')) continue;
            return i;
        }
        return -1;
    }

    private static int LastCodeIndex(IReadOnlyList<string> lines)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var t = lines[i].Trim();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith('#')) continue;
            return i;
        }
        return -1;
    }

    /// <summary>La ligne sans son commentaire « // » de fin (hors chaîne : un nombre pair de guillemets doit le précéder).</summary>
    private static string StripLineComment(string line)
    {
        var at = line.IndexOf("//", StringComparison.Ordinal);
        while (at >= 0)
        {
            if (line[..at].Count(ch => ch == '"') % 2 == 0) return line[..at];
            at = line.IndexOf("//", at + 2, StringComparison.Ordinal);
        }
        return line;
    }

    /// <summary>Après la dernière « } » (ou « ; ») de la ligne : rien d'autre qu'un commentaire, « ; », « , » ou « ) ».</summary>
    private static bool ClosingLineIsClean(string line)
    {
        var t = StripLineComment(line).TrimEnd();
        return t.Length > 0 && t[^1] is '}' or ';' or ',' or ')';
    }

    // ── Types internes ──────────────────────────────────────────────────────

    /// <summary>Une modification du texte aux sauts de ligne « \n » : <paramref name="Length"/> caractères à partir de <paramref name="Start"/> remplacés par <paramref name="Text"/>.</summary>
    private sealed record Edit(int Start, int Length, string Text);

    /// <param name="Replaced">Le texte remplacé (vide pour un ajout) — pour les contrôles « code abrégé » et « rétrécissement ».</param>
    /// <param name="Inserted">Le texte posé.</param>
    private sealed record Placement(CodeApplyKind Kind, IReadOnlyList<Edit> Edits, string Description, string Replaced, string Inserted);

    /// <summary>Une déclaration C#/Java reconnue sur une ligne : sa catégorie, son libellé (« la méthode ») et son nom.</summary>
    private sealed record Declaration(string Category, string Label, string Name)
    {
        /// <summary>Une classe (struct, record…) ou un namespace à accolades : un conteneur, pas un membre.</summary>
        public bool IsType => Category is "type" or "namespace";

        /// <summary>« namespace X » à accolades (celui « namespace X; » d'une ligne ne contient rien : il n'est pas reconnu).</summary>
        private static readonly Regex NamespaceDecl = new(
            @"^namespace\s+(?<name>[A-Za-z_@][\w.]*)\s*\{?\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex TypeDecl = new(
            @"^(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|unsafe|new|file|ref)\s+)*(?<kind>class|struct|interface|enum|record)\s+(?<name>[A-Za-z_@]\w*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex MethodDecl = new(
            @"^(?:\[[^\]]*\]\s*)*(?<pre>[\w<>\[\],.?\s]*?)\b(?<name>[A-Za-z_@]\w*)\s*(?:<[^()]*>)?\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PropertyDecl = new(
            @"^(?:\[[^\]]*\]\s*)*(?<pre>[\w<>\[\],.?\s]+?)\s+(?<name>[A-Za-z_@]\w*)\s*(?:\{|=>)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex FieldDecl = new(
            @"^(?:\[[^\]]*\]\s*)*(?<pre>(?:(?:public|private|protected|internal|static|readonly|const|volatile|new)\s+)+[\w<>\[\],.?\s]+?)\s+(?<name>[A-Za-z_@]\w*)\s*(?:=|;)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Mots qui commencent une INSTRUCTION, jamais une déclaration (« return Foo( », « if ( », « await Bar( »…).</summary>
        private static readonly HashSet<string> StatementWords = new(StringComparer.Ordinal)
        {
            "return", "await", "var", "if", "else", "while", "for", "foreach", "switch", "using", "lock", "throw", "new", "yield", "case", "do", "try",
            "catch", "finally", "goto", "base", "this", "fixed", "checked", "unchecked", "nameof", "typeof", "sizeof", "default", "when", "in", "is",
            "as", "out", "ref", "stackalloc", "namespace", "get", "set", "init", "add", "remove", "where", "select", "from", "let", "orderby", "group",
        };

        public static Declaration? Parse(string line)
        {
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('*') || line.StartsWith("/*", StringComparison.Ordinal))
                return null;
            if (line.StartsWith('[') && line.EndsWith(']'))
                return null; // attribut seul sur sa ligne : « [DllImport("x")] » n'est pas une méthode DllImport

            if (NamespaceDecl.Match(line) is { Success: true } n)
                return new("namespace", "le namespace", n.Groups["name"].Value);

            if (TypeDecl.Match(line) is { Success: true } t)
                return new("type", TypeLabel(t.Groups["kind"].Value), t.Groups["name"].Value);

            if (MethodDecl.Match(line) is { Success: true } m && IsDeclarationPrefix(m.Groups["pre"].Value, m.Groups["name"].Value))
                return new("method", "la méthode", m.Groups["name"].Value);

            if (PropertyDecl.Match(line) is { Success: true } p && IsDeclarationPrefix(p.Groups["pre"].Value, p.Groups["name"].Value))
                return new("property", "la propriété", p.Groups["name"].Value);

            if (FieldDecl.Match(line) is { Success: true } f && IsDeclarationPrefix(f.Groups["pre"].Value, f.Groups["name"].Value))
                return new("field", "le champ", f.Groups["name"].Value);

            return null;
        }

        /// <summary>Ce qui précède le nom : au moins un mot (type, modificateur), aucun mot d'instruction, ni « = » ni « objet. » (un appel).</summary>
        private static bool IsDeclarationPrefix(string pre, string name)
        {
            var words = pre.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 1
                && !StatementWords.Contains(words[0]) && !StatementWords.Contains(name)
                && !pre.Contains('=') && !pre.TrimEnd().EndsWith('.') && !pre.Contains(':');
        }

        private static string TypeLabel(string kind) => kind switch
        {
            "struct" => "la structure",
            "interface" => "l'interface",
            "enum" => "l'énumération",
            "record" => "le record",
            _ => "la classe",
        };
    }

    /// <summary>Le fichier affiché, découpé une fois pour toutes, et ce que le plan accumule (avertissements).</summary>
    private sealed class Context
    {
        public Context(CodeApplyRequest request, List<string> codeLines)
        {
            Request = request;
            CodeLines = codeLines;
            Code = string.Join("\n", codeLines);
            Doc = InlineEditPlanner.Normalize(request.DocumentText);
            Lines = Doc.Split('\n');
            RealLineCount = Doc.EndsWith('\n') ? Lines.Length - 1 : Lines.Length;
            _starts = new int[Lines.Length];
            for (var i = 1; i < Lines.Length; i++) _starts[i] = _starts[i - 1] + Lines[i - 1].Length + 1;

            Name = string.IsNullOrWhiteSpace(request.DisplayPath) ? "sans nom" : Path.GetFileName(request.DisplayPath);
            var ext = Path.GetExtension(request.DisplayPath).ToLowerInvariant();
            IsCSharp = ext == ".cs";
            IsBraceLanguage = ext is ".cs" or ".java";
            Blocks = IsBraceLanguage ? CodeStructure.Scan(Lines) : null;
            IndentUnit = DetectIndentUnit(Lines);
        }

        private readonly int[] _starts;
        private Dictionary<(string, string), List<int>>? _declarations;

        public CodeApplyRequest Request { get; }
        public List<string> CodeLines { get; }
        public string Code { get; }
        public string Doc { get; }

        /// <summary>Les lignes du texte ; s'il finit par « \n », la dernière est vide (et n'est pas comptée dans <see cref="RealLineCount"/>).</summary>
        public string[] Lines { get; }

        public int RealLineCount { get; }
        public string Name { get; }
        public bool IsCSharp { get; }
        public bool IsBraceLanguage { get; }

        /// <summary>Les blocs « { … } » du fichier (C#/Java), ou null si l'analyse n'est pas fiable.</summary>
        public IReadOnlyList<StructureBlock>? Blocks { get; }

        public string IndentUnit { get; }
        public List<string> Warnings { get; } = new();

        /// <summary>Position du début de la ligne <paramref name="line"/> (base 0) ; au-delà de la fin, la fin du texte.</summary>
        public int Offset(int line) => line < Lines.Length ? _starts[line] : Doc.Length;

        public int LineOf(int offset)
        {
            var i = Array.BinarySearch(_starts, Math.Clamp(offset, 0, Doc.Length));
            return i >= 0 ? i : ~i - 1;
        }

        /// <summary>Insère des lignes entières avant la ligne <paramref name="line"/> (base 0 ; <see cref="RealLineCount"/> = à la fin).</summary>
        public Edit InsertBefore(int line, IReadOnlyList<string> lines)
        {
            var text = string.Join("\n", lines);
            if (line >= RealLineCount && !Doc.EndsWith('\n') && Doc.Length > 0)
                return new Edit(Doc.Length, 0, "\n" + text); // dernière ligne sans saut de ligne final
            return new Edit(Offset(line), 0, text + "\n");
        }

        /// <summary>Remplace les lignes <paramref name="first"/> à <paramref name="last"/> (base 0, incluses).</summary>
        public Edit ReplaceLines(int first, int last, IReadOnlyList<string> lines)
        {
            var start = Offset(first);
            var hasNewline = last + 1 < Lines.Length;
            var end = hasNewline ? Offset(last + 1) : Doc.Length;
            return new Edit(start, end - start, string.Join("\n", lines) + (hasNewline ? "\n" : string.Empty));
        }

        /// <summary>Les lignes (base 0) du fichier qui déclarent un membre de même catégorie et de même nom.</summary>
        public List<int> DeclarationsNamed(Declaration declaration)
        {
            if (_declarations is null)
            {
                _declarations = new();
                for (var i = 0; i < RealLineCount; i++)
                {
                    if (Declaration.Parse(Lines[i].Trim()) is not { } d) continue;
                    var key = (d.Category, d.Name);
                    if (!_declarations.TryGetValue(key, out var list)) _declarations[key] = list = new List<int>();
                    list.Add(i);
                }
            }
            return _declarations.TryGetValue((declaration.Category, declaration.Name), out var found) ? found : new List<int>();
        }

        /// <summary>Un cran d'indentation : tabulation si le fichier en utilise, sinon 2 ou 4 espaces selon la plus petite indentation vue.</summary>
        private static string DetectIndentUnit(string[] lines)
        {
            var smallest = int.MaxValue;
            foreach (var line in lines)
            {
                if (line.Length == 0 || line.Trim().Length == 0) continue;
                if (line[0] == '\t') return "\t";
                var n = 0;
                while (n < line.Length && line[n] == ' ') n++;
                if (n > 0 && n < smallest) smallest = n;
            }
            return smallest == 2 ? "  " : "    ";
        }
    }
}
