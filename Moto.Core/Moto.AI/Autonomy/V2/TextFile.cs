// Moto.Core/Moto.AI/Autonomy/V2/TextFile.cs
// ★ AJOUT (24/09, agent v2) : lecture/écriture d'un fichier texte SANS l'abîmer.
// Une modification par l'IA ne doit changer que ce qu'elle annonce : on garde donc le style de fin de
// ligne (CRLF ou LF) et la présence d'un BOM du fichier d'origine, et on REFUSE un fichier qui n'est pas
// de l'UTF-8 valide (le réécrire en UTF-8 transformerait ses accents en caractères illisibles).
using System.Text;

namespace Moto.Core.AI.Autonomy.V2;

internal sealed class TextFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public string Path { get; }

    /// <summary>Contenu avec des fins de ligne normalisées en \n (le style d'origine est remis à l'écriture).</summary>
    public string Text { get; }

    public bool HasBom { get; }

    /// <summary>"\r\n" ou "\n".</summary>
    public string NewLine { get; }

    private TextFile(string path, string text, bool hasBom, string newLine)
    {
        Path = path;
        Text = text;
        HasBom = hasBom;
        NewLine = newLine;
    }

    /// <summary>Lit un fichier existant. Lève InvalidDataException s'il est binaire ou pas en UTF-8.</summary>
    public static TextFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (Array.IndexOf(bytes, (byte)0) >= 0)
            throw new InvalidDataException("Ce fichier est binaire (octets nuls) : l'agent ne le modifie pas.");

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        string raw;
        try
        {
            raw = StrictUtf8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("Ce fichier n'est pas en UTF-8 : modification refusée pour ne pas abîmer ses accents.");
        }

        var crlf = CountOf(raw, "\r\n");
        var lf = CountOf(raw, "\n") - crlf;
        var newLine = crlf > 0 && crlf >= lf ? "\r\n" : "\n";
        return new TextFile(path, raw.Replace("\r\n", "\n"), hasBom, newLine);
    }

    /// <summary>Prépare un fichier NEUF : fins de ligne alignées sur les fichiers voisins de même extension
    /// (on ne remonte jamais au-dessus de <paramref name="root"/> si elle est donnée).</summary>
    public static TextFile CreateNew(string path, string text, string? root = null)
        => new(path, text.Replace("\r\n", "\n"), hasBom: false, DetectNeighbourNewLine(path, root));

    public void Save(string normalizedText)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var text = NewLine == "\r\n" ? normalizedText.Replace("\n", "\r\n") : normalizedText;
        var body = Utf8NoBom.GetBytes(text);
        if (HasBom)
        {
            var withBom = new byte[body.Length + 3];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Buffer.BlockCopy(body, 0, withBom, 3, body.Length);
            body = withBom;
        }

        // Écriture dans un fichier temporaire puis remplacement : une coupure en plein milieu
        // ne laisse jamais un fichier à moitié écrit.
        var tmp = Path + ".moto-tmp";
        File.WriteAllBytes(tmp, body);
        File.Move(tmp, Path, overwrite: true);
    }

    /// <summary>Style de fin de ligne des fichiers de même extension dans le dossier, sinon dans les dossiers parents
    /// (un fichier neuf dans un dossier neuf reprend le style du projet) ; à défaut, celui du système.</summary>
    private static string DetectNeighbourNewLine(string path, string? root)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            var ext = System.IO.Path.GetExtension(path);
            for (var level = 0; level < 6 && !string.IsNullOrEmpty(dir); level++, dir = System.IO.Path.GetDirectoryName(dir))
            {
                if (root is not null && !AgentPathResolver.IsWithinRoot(root, dir)) break;
                if (!Directory.Exists(dir)) continue;

                var crlf = 0;
                var lf = 0;
                foreach (var f in Directory.EnumerateFiles(dir, "*" + ext).Take(5))
                {
                    var head = File.ReadAllText(f);
                    if (head.Contains("\r\n")) crlf++;
                    else if (head.Contains('\n')) lf++;
                }
                if (crlf + lf > 0) return crlf >= lf ? "\r\n" : "\n";
            }
        }
        catch (Exception)
        {
            // Détection facultative : on retombe sur le style du système.
        }
        return Environment.NewLine;
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = text.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
