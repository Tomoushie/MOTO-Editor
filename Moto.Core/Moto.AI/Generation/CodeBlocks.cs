// Moto.Core/Moto.AI/Generation/CodeBlocks.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : extraction des blocs de code d'une réponse de modèle.
// Remplace les deux expressions régulières de MainPage (ExtractCodeBlock / ExtractCodeBlockWithLanguage), qui
//  - ne voyaient PAS un bloc non refermé : une réponse coupée par la limite de jetons donnait « pas de code » au lieu de
//    « réponse tronquée » — et, avec l'ancienne recherche du premier « ``` », un fichier remplacé par la moitié d'un fichier ;
//  - ne gardaient pas le nom de fichier annoncé par le modèle (```csharp Program.cs, ou « **Program.cs** » juste avant) ;
//  - prenaient pour une fermeture n'importe quelle suite de trois accents graves, même au milieu d'une ligne.
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Generation;

/// <param name="Language">Étiquette du bloc en minuscules (« csharp », « python »…), vide si le bloc n'en porte pas.</param>
/// <param name="Code">Le code, sans les lignes d'ouverture et de fermeture. L'indentation de la première ligne est conservée.</param>
/// <param name="PathHint">Nom de fichier annoncé pour ce bloc, sinon null.</param>
/// <param name="IsComplete">Faux si la réponse s'arrête avant la fermeture du bloc : le modèle a été coupé, le code est probablement INCOMPLET.</param>
public sealed record CodeBlock(string Language, string Code, string? PathHint, bool IsComplete);

/// <summary>Un morceau de réponse, dans l'ordre : du texte (<see cref="Code"/> null) ou un bloc de code.</summary>
public sealed record ReplyPart(string Text, CodeBlock? Code)
{
    public bool IsCode => Code is not null;
}

public static class CodeBlocks
{
    /// <summary>« **Program.cs** », « ### src/Foo.cs », « Fichier : Foo.cs », « `Foo.cs`: » — seule sur sa ligne, juste avant un bloc.</summary>
    private static readonly Regex PathLine = new(
        @"^\s*(?:#{1,6}\s+|[-*+]\s+|\d+[.)]\s+)?(?:(?:fichier|file|chemin|path|dans|in|cr[ée]er|create)\s*:?\s*)?[*_`""'\[]*(?<p>\w[\w./\\-]*\.[A-Za-z][A-Za-z0-9]{0,7})[*_`""'\]]*\s*:?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex KeyValuePath = new(
        @"\b(?:path|file|filename|title|name)\s*=\s*""?(?<p>[^""\s]+)""?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Word = new(@"^[A-Za-z][\w+#-]*$", RegexOptions.Compiled);
    private static readonly Regex PathLike = new(@"^\w[\w./\\-]*\.[A-Za-z][A-Za-z0-9]{0,7}$", RegexOptions.Compiled);

    /// <summary>Tous les blocs de code de la réponse, dans l'ordre. Un bloc jamais refermé est renvoyé avec <see cref="CodeBlock.IsComplete"/> = faux.</summary>
    public static IReadOnlyList<CodeBlock> Extract(string? reply)
        => Split(reply).Where(p => p.Code is not null).Select(p => p.Code!).ToList();

    /// <summary>
    /// ★ AJOUT (24/09, chat en flux) : la réponse découpée en texte et blocs de code, dans l'ordre — pour l'affichage du chat. Même lecture des
    /// balises ``` qu'<see cref="Extract"/> : la ligne d'ouverture entière (« ```csharp Program.cs ») est retirée du code, et le nom de fichier
    /// annoncé est gardé. Les morceaux de texte vides (entre deux blocs) sont omis.
    /// </summary>
    public static IReadOnlyList<ReplyPart> Split(string? reply)
    {
        var parts = new List<ReplyPart>();
        if (string.IsNullOrEmpty(reply)) return parts;

        var lines = reply.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var text = new List<string>();
        string? previous = null; // dernière ligne non vide hors bloc : peut annoncer le nom du fichier

        void FlushText()
        {
            var joined = string.Join("\n", text).Trim('\n');
            if (!string.IsNullOrWhiteSpace(joined)) parts.Add(new ReplyPart(joined, null));
            text.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (!TryOpenFence(lines[i], out var fenceLength, out var info))
            {
                text.Add(lines[i]);
                if (!string.IsNullOrWhiteSpace(lines[i])) previous = lines[i];
                continue;
            }

            FlushText();
            var body = new List<string>();
            var closed = false;
            for (i++; i < lines.Length; i++)
            {
                if (IsClosingFence(lines[i], fenceLength)) { closed = true; break; }
                body.Add(lines[i]);
            }

            var (language, pathFromInfo) = ParseInfo(info);
            var block = new CodeBlock(language, Clean(body), pathFromInfo ?? PathFromLine(previous), closed);
            parts.Add(new ReplyPart(block.Code, block));
            previous = null;
        }

        FlushText();
        return parts;
    }

    /// <summary>Le premier bloc de la réponse, ou null s'il n'y en a aucun.</summary>
    public static CodeBlock? First(string? reply) => Extract(reply) is { Count: > 0 } all ? all[0] : null;

    /// <summary>
    /// ★ AJOUT (26/09, retour de Tom : « Appliquer » sur « pip install pygame » n'avait aucun sens) : l'étiquette annonce une commande à taper
    /// dans un terminal (```bash, ```powershell…), pas du code à poser dans un fichier. Un bloc sans étiquette n'en est pas une.
    /// </summary>
    public static bool IsTerminalCommand(string? language) => (language ?? string.Empty).Trim().ToLowerInvariant() is
        "bash" or "sh" or "shell" or "zsh" or "console" or "terminal" or "shellsession" or "sh-session" or "shell-session"
        or "cmd" or "bat" or "batch" or "powershell" or "ps" or "ps1" or "pwsh";

    /// <summary>Extension de fichier pour une étiquette de langage (« csharp » → « .cs ») ; « .txt » si elle est inconnue.</summary>
    public static string ExtensionFor(string? language) => (language ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "csharp" or "cs" or "c#" => ".cs",
        "python" or "py" => ".py",
        "javascript" or "js" or "node" => ".js",
        "typescript" or "ts" => ".ts",
        "tsx" => ".tsx",
        "jsx" => ".jsx",
        "html" => ".html",
        "css" => ".css",
        "scss" => ".scss",
        "json" => ".json",
        "xaml" => ".xaml",
        "xml" => ".xml",
        "csproj" => ".csproj",
        "sql" => ".sql",
        "bash" or "shell" or "sh" or "zsh" => ".sh",
        "powershell" or "ps1" or "pwsh" => ".ps1",
        "bat" or "batch" or "cmd" => ".bat",
        "java" => ".java",
        "kotlin" or "kt" => ".kt",
        "cpp" or "c++" or "cc" => ".cpp",
        "c" => ".c",
        "h" => ".h",
        "hpp" => ".hpp",
        "go" or "golang" => ".go",
        "rust" or "rs" => ".rs",
        "ruby" or "rb" => ".rb",
        "php" => ".php",
        "swift" => ".swift",
        "yaml" or "yml" => ".yml",
        "toml" => ".toml",
        "ini" => ".ini",
        "markdown" or "md" => ".md",
        _ => ".txt",
    };

    /// <summary>Nom lisible du langage d'un fichier d'après son extension (pour les consignes envoyées au modèle).</summary>
    public static string LanguageLabel(string? path) => Path.GetExtension(path ?? string.Empty).ToLowerInvariant() switch
    {
        ".cs" => "C#",
        ".xaml" => "XAML",
        ".csproj" or ".props" or ".targets" => "MSBuild (XML)",
        ".xml" or ".config" or ".resx" or ".svg" => "XML",
        ".json" => "JSON",
        ".py" => "Python",
        ".js" or ".mjs" => "JavaScript",
        ".ts" => "TypeScript",
        ".tsx" or ".jsx" => "JavaScript/React",
        ".html" or ".htm" => "HTML",
        ".css" or ".scss" => "CSS",
        ".md" => "Markdown",
        ".java" => "Java",
        ".kt" => "Kotlin",
        ".c" or ".h" => "C",
        ".cpp" or ".hpp" or ".cc" => "C++",
        ".go" => "Go",
        ".rs" => "Rust",
        ".sql" => "SQL",
        ".ps1" => "PowerShell",
        ".sh" => "shell",
        ".yml" or ".yaml" => "YAML",
        ".toml" => "TOML",
        _ => "texte",
    };

    // ── Analyse ─────────────────────────────────────────────────────────────

    private static bool TryOpenFence(string line, out int length, out string info)
    {
        length = 0;
        info = string.Empty;

        var t = line.TrimStart();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return false;

        var n = 0;
        while (n < t.Length && t[n] == '`') n++;

        var rest = t[n..].Trim();
        if (rest.Contains('`')) return false; // ```code``` sur une seule ligne : du code « en ligne », pas un bloc

        length = n;
        info = rest;
        return true;
    }

    private static bool IsClosingFence(string line, int openLength)
    {
        var t = line.Trim();
        return t.Length >= openLength && t.All(c => c == '`');
    }

    /// <summary>Lignes vides retirées en tête et en queue ; l'indentation de la première ligne est gardée.</summary>
    private static string Clean(List<string> body)
    {
        var start = 0;
        while (start < body.Count && string.IsNullOrWhiteSpace(body[start])) start++;

        var end = body.Count;
        while (end > start && string.IsNullOrWhiteSpace(body[end - 1])) end--;

        return string.Join("\n", body.Skip(start).Take(end - start)).TrimEnd();
    }

    /// <summary>« csharp », « csharp Program.cs », « csharp:src/Program.cs », « csharp title="Program.cs" », « Program.cs »…</summary>
    private static (string Language, string? Path) ParseInfo(string info)
    {
        if (info.Length == 0) return (string.Empty, null);

        string? path = null;
        var kv = KeyValuePath.Match(info);
        if (kv.Success)
        {
            path = kv.Groups["p"].Value;
            info = info.Remove(kv.Index, kv.Length).Trim();
        }

        var language = string.Empty;
        foreach (var raw in info.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim('{', '}', ',');
            if (token.StartsWith("language-", StringComparison.OrdinalIgnoreCase)) token = token["language-".Length..];
            token = token.TrimStart('.');
            if (token.Length == 0) continue;

            var colon = token.IndexOf(':');
            if (colon > 0 && Word.IsMatch(token[..colon]) && PathLike.IsMatch(token[(colon + 1)..]))
            {
                if (language.Length == 0) language = token[..colon].ToLowerInvariant();
                path ??= token[(colon + 1)..];
                continue;
            }

            if (PathLike.IsMatch(token)) { path ??= token; continue; }
            if (language.Length == 0 && Word.IsMatch(token)) language = token.ToLowerInvariant();
        }

        return (language, path);
    }

    private static string? PathFromLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Contains("://", StringComparison.Ordinal)) return null;
        var m = PathLine.Match(line);
        return m.Success ? m.Groups["p"].Value : null;
    }
}
