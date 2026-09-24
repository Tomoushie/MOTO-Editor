// Moto.Core/Moto.AI/Autonomy/V2/InsertionGuard.cs
// ★ AJOUT (24/09, agent v2) : garde-fou de insert_lines pour le C# et le Java.
// Constat du banc d'essai (petit modèle) : pour « ajouter une méthode », il donne le numéro de ligne d'après la
// dernière accolade du fichier (donc EN DEHORS de la classe) ou une ligne située entre la signature d'une méthode et son
// accolade ouvrante. Le diff est faux, le build casse, et l'humain a déjà dû juger un diff absurde. On refuse donc AVANT
// la confirmation, avec le numéro de ligne à utiliser à la place. En cas de doute (fichier que l'analyse ne comprend pas)
// on laisse passer : ce garde-fou ne doit jamais bloquer une insertion légitime.
using System.Text;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Autonomy.V2;

internal enum BlockKind { Type, Namespace, Other }

/// <summary>Un bloc « { … } ». Les numéros de ligne commencent à 1 ; CloseLine est la ligne qui contient l'accolade fermante.</summary>
internal sealed record CodeBlock(BlockKind Kind, string Name, int OpenLine, int CloseLine);

internal static class CodeStructure
{
    private static readonly Regex NamespaceHeader = new(@"\bnamespace\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TypeHeader = new(
        @"\b(?:class|struct|interface|enum|record)\s+(?!where\b|new\b)(?<name>[A-Za-z_@][\w]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Découpe le texte en blocs d'accolades. Renvoie null si l'analyse n'est pas fiable (accolades déséquilibrées, chaîne non fermée…).</summary>
    public static List<CodeBlock>? Scan(IReadOnlyList<string> lines)
    {
        var blocks = new List<CodeBlock>();
        var open = new Stack<(BlockKind Kind, string Name, int Line)>();
        var header = new StringBuilder();
        var inBlockComment = false;
        var inVerbatim = false;
        var rawQuotes = 0; // > 0 : à l'intérieur d'une chaîne brute """ … """ avec ce nombre de guillemets

        for (var ln = 0; ln < lines.Count; ln++)
        {
            var line = lines[ln];
            var i = 0;

            if (!inBlockComment && !inVerbatim && rawQuotes == 0 && line.TrimStart().StartsWith('#'))
                continue; // directive de préprocesseur (#if, #region…)

            while (i < line.Length)
            {
                if (inBlockComment)
                {
                    var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (end < 0) { i = line.Length; break; }
                    inBlockComment = false;
                    i = end + 2;
                    continue;
                }

                if (rawQuotes > 0)
                {
                    var closing = new string('"', rawQuotes);
                    var end = line.IndexOf(closing, i, StringComparison.Ordinal);
                    if (end < 0) { i = line.Length; break; }
                    rawQuotes = 0;
                    i = end + closing.Length;
                    continue;
                }

                if (inVerbatim)
                {
                    var q = line.IndexOf('"', i);
                    if (q < 0) { i = line.Length; break; }
                    if (q + 1 < line.Length && line[q + 1] == '"') { i = q + 2; continue; } // "" = guillemet échappé
                    inVerbatim = false;
                    i = q + 1;
                    continue;
                }

                var c = line[i];

                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*') { inBlockComment = true; i += 2; continue; }

                if (c == '"')
                {
                    var quotes = 1;
                    while (i + quotes < line.Length && line[i + quotes] == '"') quotes++;
                    if (quotes >= 3) { rawQuotes = quotes; i += quotes; continue; }
                    if (quotes == 2) { i += 2; continue; } // chaîne vide ""

                    var verbatim = i > 0 && (line[i - 1] == '@' || (i > 1 && line[i - 1] == '$' && line[i - 2] == '@'));
                    if (verbatim) { inVerbatim = true; i++; continue; }

                    i++;
                    while (i < line.Length) // chaîne ordinaire ; non fermée sur la ligne = ignorée jusqu'à la fin de la ligne
                    {
                        if (line[i] == '\\') { i += 2; continue; }
                        if (line[i] == '"') { i++; break; }
                        i++;
                    }
                    continue;
                }

                if (c == '\'')
                {
                    // Littéral de caractère : 'a', '\n', '\''.
                    i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '\\') { i += 2; continue; }
                        if (line[i] == '\'') { i++; break; }
                        i++;
                    }
                    continue;
                }

                if (c == '{')
                {
                    var text = header.ToString();
                    var kind = BlockKind.Other;
                    var name = string.Empty;
                    if (NamespaceHeader.IsMatch(text)) kind = BlockKind.Namespace;
                    else if (TypeHeader.Match(text) is { Success: true } m) { kind = BlockKind.Type; name = m.Groups["name"].Value; }
                    open.Push((kind, name, ln + 1));
                    header.Clear();
                    i++;
                    continue;
                }

                if (c == '}')
                {
                    if (open.Count == 0) return null;
                    var (kind, name, openLine) = open.Pop();
                    blocks.Add(new CodeBlock(kind, name, openLine, ln + 1));
                    header.Clear();
                    i++;
                    continue;
                }

                if (c == ';') header.Clear();
                else header.Append(c);
                i++;
            }
            header.Append(' ');
        }

        if (open.Count != 0 || inBlockComment || inVerbatim || rawQuotes > 0) return null;
        return blocks;
    }

    /// <summary>Accolades ouvrantes moins fermantes d'un extrait (chaînes et commentaires ignorés), ou null si l'analyse n'est pas fiable.</summary>
    public static int? NetBraces(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var depth = 0;
        var inBlockComment = false;
        foreach (var line in lines)
        {
            var i = 0;
            while (i < line.Length)
            {
                if (inBlockComment)
                {
                    var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (end < 0) { i = line.Length; break; }
                    inBlockComment = false;
                    i = end + 2;
                    continue;
                }
                var c = line[i];
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*') { inBlockComment = true; i += 2; continue; }
                if (c == '"' || c == '\'')
                {
                    var quote = c;
                    i++;
                    while (i < line.Length && line[i] != quote) i += line[i] == '\\' ? 2 : 1;
                    i++;
                    continue;
                }
                if (c == '{') depth++;
                else if (c == '}') depth--;
                i++;
            }
        }
        return inBlockComment ? null : depth;
    }
}

internal static class InsertionGuard
{
    private static readonly string[] Extensions = { ".cs", ".java" };

    /// <summary>Le texte commence par un modificateur d'accès : c'est un membre de classe ou un type.</summary>
    private static readonly Regex AccessStart = new(
        @"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>La déclaration d'un type (class, struct, interface, enum, record, delegate), avec ses modificateurs.</summary>
    private static readonly Regex TypeDeclaration = new(
        @"^\s*(?:\[[^\]]*\]\s*)*(?:\w+\s+)*?(?:class|struct|interface|enum|record|delegate)\s+[A-Za-z_@]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static bool IsMemberDeclaration(string firstCodeLine)
        => AccessStart.IsMatch(firstCodeLine) && !TypeDeclaration.IsMatch(firstCodeLine);

    /// <summary>Un message d'erreur à renvoyer au modèle, ou null si l'insertion est plausible (ou impossible à juger).</summary>
    public static string? Check(string displayPath, IReadOnlyList<string> lines, int insertBeforeLine, string text)
    {
        var ext = Path.GetExtension(displayPath);
        if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return null;

        // 1. Les accolades du texte inséré doivent s'équilibrer : une méthode sans son « } » casse tout le fichier.
        if (CodeStructure.NetBraces(text) is { } net && net != 0)
            return $"Refusé : le texte à insérer a {(net > 0 ? $"{net} accolade(s) « {{ » de trop" : $"{-net} accolade(s) « }} » de trop")}. " +
                   "Recopie la méthode ou le bloc COMPLET, avec toutes ses accolades ouvrantes et fermantes.";

        // 2. Pas entre la signature d'une déclaration et son « { » (style Allman).
        if (insertBeforeLine <= lines.Count && lines[insertBeforeLine - 1].TrimStart().StartsWith('{'))
        {
            var previous = PreviousNonBlank(lines, insertBeforeLine - 1);
            if (previous is not null && !previous.EndsWith('{') && !previous.EndsWith('}') && !previous.EndsWith(';') && !previous.EndsWith(','))
                return $"Refusé : la ligne {insertBeforeLine} est l'accolade « {{ » qui ouvre le corps de « {Shorten(previous)} » : ton texte serait inséré ENTRE la signature et son corps. " +
                       "Choisis une ligne située ENTRE deux membres (par exemple juste avant l'accolade fermante « } » de la classe).";
        }

        // 3. Un membre de classe ne s'insère que DANS une classe.
        if (IsMemberDeclaration(FirstCodeLine(text)) && CodeStructure.Scan(lines) is { } blocks)
        {
            // Les blocs qui contiennent le point d'insertion (début de la ligne demandée), du plus intérieur au plus extérieur.
            var containing = blocks
                .Where(b => b.OpenLine < insertBeforeLine && insertBeforeLine <= b.CloseLine)
                .OrderByDescending(b => b.OpenLine)
                .ToList();

            if (containing.FirstOrDefault() is not { Kind: BlockKind.Type })
            {
                if (containing.FirstOrDefault(b => b.Kind == BlockKind.Type) is { } enclosing)
                    return $"Refusé : ce texte est un membre de classe (méthode, propriété…) mais la ligne {insertBeforeLine} est À L'INTÉRIEUR d'une méthode ou d'un autre bloc de « {enclosing.Name} ». " +
                           $"Insère-le ENTRE deux membres, ou AVANT la ligne {enclosing.CloseLine} (line={enclosing.CloseLine}) pour l'ajouter à la fin de la classe.";

                var target = blocks.Where(b => b.Kind == BlockKind.Type && b.CloseLine < insertBeforeLine).OrderByDescending(b => b.CloseLine).FirstOrDefault()
                             ?? blocks.Where(b => b.Kind == BlockKind.Type && b.OpenLine >= insertBeforeLine).OrderBy(b => b.OpenLine).FirstOrDefault();
                return target is null
                    ? $"Refusé : ce texte est un membre de classe (méthode, propriété…) mais la ligne {insertBeforeLine} n'est dans aucune classe."
                    : $"Refusé : ce texte est un membre de classe (méthode, propriété…) mais la ligne {insertBeforeLine} est EN DEHORS de toute classe. " +
                      $"Pour l'ajouter DANS la classe « {target.Name} », insère-le AVANT la ligne {target.CloseLine} (line={target.CloseLine}) : c'est son accolade fermante « }} ».";
            }
        }

        return null;
    }

    private static string? PreviousNonBlank(IReadOnlyList<string> lines, int beforeIndex)
    {
        for (var i = beforeIndex - 1; i >= 0; i--)
        {
            var t = lines[i].Trim();
            if (t.Length > 0) return t;
        }
        return null;
    }

    private static string FirstCodeLine(string text)
    {
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var t = raw.Trim();
            if (t.Length == 0 || t.StartsWith("//") || t.StartsWith("/*") || t.StartsWith('*')) continue;
            if (t.StartsWith('[') && t.EndsWith(']')) continue; // attribut sur sa propre ligne
            return raw;
        }
        return string.Empty;
    }

    private static string Shorten(string s) => s.Length <= 60 ? s : s[..60] + "…";
}
