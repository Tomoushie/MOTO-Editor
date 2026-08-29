using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Shared;

/// <summary>Téléchargement résumable (Range) + mirrors fallback.</summary>
public static class ResumableDownloader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static async Task DownloadAsync(string[] urls, string dest,
        Action<long, long?>? onProgress = null, CancellationToken ct = default)
    {
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                await DownloadSingleAsync(url, dest, onProgress, ct);
                return;
            }
            catch (Exception ex) { last = ex; } // essaie le mirror suivant
        }
        throw last ?? new IOException("Tous les mirrors ont échoué.");
    }

    private static async Task DownloadSingleAsync(string url, string dest,
        Action<long, long?>? onProgress, CancellationToken ct)
    {
        long existing = File.Exists(dest) ? new FileInfo(dest).Length : 0;

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        // Si le serveur ne supporte pas Range → recommence à zéro
        if (existing > 0 && resp.StatusCode != System.Net.HttpStatusCode.PartialContent)
        { existing = 0; File.Delete(dest); }

        long? total = resp.Content.Headers.ContentLength.HasValue
            ? resp.Content.Headers.ContentLength.Value + existing : (long?)null;

        using var src = await resp.Content.ReadAsStreamAsync(ct);
        using var dst = new FileStream(dest, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write);

        var buf = new byte[81920];
        int n; long done = existing;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            done += n; onProgress?.Invoke(done, total);
        }
    }
}
