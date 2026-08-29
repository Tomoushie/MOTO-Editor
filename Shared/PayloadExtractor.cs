// Shared/PayloadExtractor.cs
using System.IO.Compression;
using System.Reflection;

namespace Moto.Shared;

/// <summary>
/// Extraction du payload (ZIP) — partagé entre Moto.Installer et Moto.Editor.
/// </summary>
public static class PayloadExtractor
{
    public static void ExtractTo(string destination, Action<int> onProgress, string? payloadPath = null)
    {
        Directory.CreateDirectory(destination);
        string fullDest = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        using var zip = OpenPayload(payloadPath);
        var entries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        int done = 0, lastPct = -1;

        foreach (var entry in entries)
        {
            string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));

            // Sécurité : empêche l'attaque "Zip Slip"
            if (!target.StartsWith(fullDest, StringComparison.Ordinal))
                throw new InvalidOperationException($"Entrée invalide : {entry.FullName}");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);

            int pct = ++done * 100 / entries.Count;
            if (pct != lastPct) { lastPct = pct; onProgress(pct); }
        }
    }

    private static ZipArchive OpenPayload(string? explicitPath)
    {
        // 1. Payload explicite (mise à jour téléchargée)
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            return ZipFile.OpenRead(explicitPath);

        // 2. Payload embarqué dans l'EXE (installateur)
        var res = Assembly.GetExecutingAssembly()
                          .GetManifestResourceStream("Moto.Installer.payload.zip");
        if (res != null) return new ZipArchive(res, ZipArchiveMode.Read);

        // 3. Fallback : payload.zip adjacent
        string side = Path.Combine(AppContext.BaseDirectory, "payload.zip");
        if (File.Exists(side)) return ZipFile.OpenRead(side);

        throw new FileNotFoundException("Payload introuvable (payload.zip manquant).");
    }
}
