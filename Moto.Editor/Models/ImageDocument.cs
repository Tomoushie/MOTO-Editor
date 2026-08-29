namespace Moto.Editor.Models;

/// <summary>Document image ouvrable dans un onglet (comme un fichier de code).</summary>
public class ImageDocument
{
    public string Path { get; init; } = "";
    public string FileName => System.IO.Path.GetFileName(Path);
    public long SizeBytes { get; init; }
    public int Width { get; set; }
    public int Height { get; set; }

    public static readonly string[] SupportedExtensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".svg" };

    public static bool IsSupported(string path)
        => SupportedExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
}
