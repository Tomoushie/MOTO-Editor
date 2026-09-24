// Moto.Core/Moto.AI/Autonomy/V2/EditMatcher.cs
// ★ AJOUT (24/09, agent v2) : « remplace CE passage par CELUI-CI » — le cœur d'une modification
// minimale. La v1 n'avait que « réécris tout le fichier », qui fait perdre du code dès que le modèle
// tronque (plafond de 256 jetons de réponse mesuré) ou oublie une partie.
//
// Un petit modèle recopie rarement un passage au caractère près (indentation perdue, numéros de
// ligne recopiés, espaces en fin de ligne). On essaie donc, dans l'ordre, du plus strict au plus
// tolérant, et on DIT laquelle a servi — l'humain voit de toute façon le diff réel avant d'accepter.
using System.Text;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Autonomy.V2;

internal sealed class EditOutcome
{
    public string? NewContent { get; init; }
    public string? Error { get; init; }
    public string Strategy { get; init; } = "exact";
    public int Replacements { get; init; }

    /// <summary>Ligne (base 1) où commence le premier remplacement, dans le NOUVEAU contenu.</summary>
    public int FirstLine { get; init; }

    /// <summary>Nombre de lignes du texte inséré (pour montrer le passage modifié).</summary>
    public int InsertedLines { get; init; }

    public bool Success => Error is null;
}

internal static class EditMatcher
{
    private static readonly Regex LineNumberPrefix = new(@"^\s*\d+\s*[|:→]\s?", RegexOptions.Compiled);

    public static EditOutcome Apply(string content, string oldText, string newText, bool replaceAll)
    {
        oldText = Normalize(oldText);
        newText = Normalize(newText);

        if (oldText.Trim().Length == 0)
            return Fail("old_text est vide. edit_file REMPLACE un passage : donne le passage EXACT à remplacer (recopié depuis read_file). " +
                        "Pour AJOUTER du code sans rien remplacer, utilise insert_lines (path, line, text) ; pour créer un fichier neuf, write_file.");

        // 1) Correspondance exacte.
        var strategy = "exact";
        var hits = IndexesOf(content, oldText);

        // 2) Le modèle a recopié les numéros de ligne de read_file.
        if (hits.Count == 0)
        {
            var strippedOld = StripLineNumbers(oldText);
            if (strippedOld is not null)
            {
                var strippedHits = IndexesOf(content, strippedOld);
                if (strippedHits.Count > 0)
                {
                    oldText = strippedOld;
                    newText = StripLineNumbers(newText) ?? newText;
                    hits = strippedHits;
                    strategy = "sans les numéros de ligne";
                }
            }
        }

        if (hits.Count > 0)
        {
            if (hits.Count > 1 && !replaceAll)
                return Fail(NotUnique(content, hits));

            // Garder le même « saut de ligne final » que le passage remplacé : sinon la ligne suivante
            // se colle au texte inséré (ou une ligne vide apparaît en trop).
            if (oldText.EndsWith('\n') && newText.Length > 0 && !newText.EndsWith('\n')) newText += "\n";
            else if (!oldText.EndsWith('\n') && newText.EndsWith('\n')) newText = newText[..^1];

            var sb = new StringBuilder();
            var cursor = 0;
            var first = -1;
            var toReplace = replaceAll ? hits : new List<int> { hits[0] };
            foreach (var at in toReplace)
            {
                sb.Append(content, cursor, at - cursor);
                if (first < 0) first = CountNewlines(sb) + 1;
                sb.Append(newText);
                cursor = at + oldText.Length;
            }
            sb.Append(content, cursor, content.Length - cursor);

            return new EditOutcome
            {
                NewContent = sb.ToString(),
                Strategy = strategy,
                Replacements = toReplace.Count,
                FirstLine = first,
                InsertedLines = CountLines(newText),
            };
        }

        // 3) Comparaison ligne à ligne, d'abord en ignorant les espaces de fin, puis l'indentation.
        var byLine = ApplyLineBased(content, oldText, newText, replaceAll);
        if (byLine is not null) return byLine;

        return Fail("old_text est introuvable dans ce fichier. " + Hint(content, oldText));
    }

    // ── Correspondance par lignes ───────────────────────────────────────────

    private static EditOutcome? ApplyLineBased(string content, string oldText, string newText, bool replaceAll)
    {
        var contentLines = content.Split('\n');
        var oldLines = TrimBlankEdges(oldText.Split('\n'));
        if (oldLines.Length == 0) return null;

        var newLines = newText.Split('\n').ToList();
        // « …\n » final : ne pas ajouter une ligne vide en plus de celle qui suit déjà le passage.
        if (newLines.Count > 0 && newLines[^1].Length == 0 && oldText.EndsWith('\n')) newLines.RemoveAt(newLines.Count - 1);
        newLines = TrimBlankEdges(newLines.ToArray()).ToList();

        foreach (var (name, equals, reindent) in new (string, Func<string, string, bool>, bool)[]
        {
            ("en ignorant les espaces de fin de ligne", (a, b) => a.TrimEnd() == b.TrimEnd(), false),
            ("en ignorant l'indentation", (a, b) => a.Trim() == b.Trim(), true),
        })
        {
            var starts = new List<int>();
            for (var i = 0; i + oldLines.Length <= contentLines.Length; i++)
            {
                var all = true;
                for (var j = 0; j < oldLines.Length; j++)
                    if (!equals(contentLines[i + j], oldLines[j])) { all = false; break; }
                if (!all) continue;
                starts.Add(i);
                i += oldLines.Length - 1; // pas de recouvrement
            }

            if (starts.Count == 0) continue;
            if (starts.Count > 1 && !replaceAll)
                return Fail($"old_text correspond à {starts.Count} endroits (lignes {string.Join(", ", starts.Take(5).Select(s => s + 1))}) : ajoute des lignes voisines pour le rendre unique, ou mets replace_all=true.");

            var chosen = replaceAll ? starts : new List<int> { starts[0] };
            var result = new List<string>();
            var cursor = 0;
            var firstLine = -1;
            foreach (var s in chosen)
            {
                result.AddRange(contentLines.Skip(cursor).Take(s - cursor));
                if (firstLine < 0) firstLine = result.Count + 1;

                IEnumerable<string> block = newLines;
                if (reindent)
                {
                    var extra = ExtraIndent(contentLines[s], oldLines[0]);
                    if (extra.Length > 0) block = newLines.Select(l => l.Length == 0 ? l : extra + l);
                }
                result.AddRange(block);
                cursor = s + oldLines.Length;
            }
            result.AddRange(contentLines.Skip(cursor));

            return new EditOutcome
            {
                NewContent = string.Join('\n', result),
                Strategy = name,
                Replacements = chosen.Count,
                FirstLine = firstLine,
                InsertedLines = newLines.Count,
            };
        }

        return null;
    }

    /// <summary>Indentation que le fichier a EN PLUS de celle recopiée par le modèle (0 si aucune).</summary>
    private static string ExtraIndent(string fileLine, string modelLine)
    {
        var fileIndent = LeadingWhitespace(fileLine);
        var modelIndent = LeadingWhitespace(modelLine);
        return fileIndent.Length > modelIndent.Length && fileIndent.EndsWith(modelIndent, StringComparison.Ordinal)
            ? fileIndent[..^modelIndent.Length]
            : string.Empty;
    }

    private static string LeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        return line[..i];
    }

    // ── Aides ───────────────────────────────────────────────────────────────

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string[] TrimBlankEdges(string[] lines)
    {
        var start = 0;
        var end = lines.Length;
        while (start < end && lines[start].Trim().Length == 0) start++;
        while (end > start && lines[end - 1].Trim().Length == 0) end--;
        return lines[start..end];
    }

    /// <summary>Retire « 12 | » ou « 12: » en tête de chaque ligne — seulement si TOUTES les lignes non vides l'ont.</summary>
    private static string? StripLineNumbers(string text)
    {
        var lines = text.Split('\n');
        var nonEmpty = lines.Where(l => l.Trim().Length > 0).ToList();
        if (nonEmpty.Count == 0 || !nonEmpty.All(l => LineNumberPrefix.IsMatch(l))) return null;
        return string.Join('\n', lines.Select(l => LineNumberPrefix.Replace(l, string.Empty, 1)));
    }

    private static List<int> IndexesOf(string content, string needle)
    {
        var list = new List<int>();
        var i = 0;
        while ((i = content.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            list.Add(i);
            i += needle.Length;
        }
        return list;
    }

    private static int CountNewlines(StringBuilder text)
    {
        var n = 0;
        for (var i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
        return n;
    }

    private static int CountLines(string text) => text.Length == 0 ? 0 : text.TrimEnd('\n').Split('\n').Length;

    private static string NotUnique(string content, List<int> hits)
    {
        var lines = hits.Take(5).Select(h => content.AsSpan(0, h).Count('\n') + 1);
        return $"old_text apparaît {hits.Count} fois (lignes {string.Join(", ", lines)}). Ajoute quelques lignes voisines pour le rendre unique, ou mets replace_all=true pour toutes les remplacer.";
    }

    /// <summary>Aide le modèle à se corriger : où se trouve la ligne qui ressemble le plus à son old_text.</summary>
    private static string Hint(string content, string oldText)
    {
        var first = oldText.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (first is null) return string.Empty;

        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].Trim() == first)
                return $"Ta première ligne existe bien à la ligne {i + 1}, mais la suite ne correspond pas : relis les lignes {i + 1}–{Math.Min(lines.Length, i + 12)} avec read_file et recopie-les EXACTEMENT (sans les numéros).";

        var best = -1;
        var bestScore = 0.0;
        var firstTokens = Tokens(first);
        if (firstTokens.Count > 0)
            for (var i = 0; i < lines.Length; i++)
            {
                var t = Tokens(lines[i]);
                if (t.Count == 0) continue;
                var score = (double)firstTokens.Intersect(t).Count() / firstTokens.Union(t).Count();
                if (score > bestScore) { bestScore = score; best = i; }
            }

        return best >= 0 && bestScore >= 0.5
            ? $"La ligne la plus proche est la ligne {best + 1} : « {Shorten(lines[best].Trim())} ». Relis ce passage avec read_file et recopie-le exactement."
            : "Aucune ligne ne ressemble à ton old_text : relis le fichier avec read_file (ou cherche avec search_text) avant de réessayer.";
    }

    private static HashSet<string> Tokens(string line)
        => Regex.Split(line, @"[^\p{L}\p{N}_]+").Where(t => t.Length > 0).ToHashSet(StringComparer.Ordinal);

    private static string Shorten(string s) => s.Length <= 120 ? s : s[..120] + "…";

    private static EditOutcome Fail(string message) => new() { Error = message };
}
