// Moto.Core/AI/Embedded/ModelDownloader.cs
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Télécharge les modèles LLM avec progression.
/// Supporte la reprise sur erreur (HTTP Range).
/// </summary>
public sealed class ModelDownloader
{
    private readonly HttpClient _httpClient;

    public event Action<DownloadProgress>? ProgressChanged;
    public event Action<DownloadState>? StateChanged;

    public ModelDownloader()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
    }

    /// <summary>
    /// Télécharge un modèle avec progression.
    /// </summary>
    public async Task DownloadAsync(EmbeddedLlmConfig config, CancellationToken ct = default)
    {
        Directory.CreateDirectory(config.ModelsDirectory);
        var targetPath = Path.Combine(config.ModelsDirectory, config.ModelFileName);
        var tempPath = targetPath + ".part";

        StateChanged?.Invoke(DownloadState.Starting);

        try
        {
            long startByte = 0;
            if (File.Exists(tempPath))
            {
                startByte = new FileInfo(tempPath).Length;
                StateChanged?.Invoke(DownloadState.Resuming);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, config.DownloadUrl);
            if (startByte > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startByte, null);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? config.ExpectedSizeBytes;
            var buffer = new byte[81920]; // 80 KB chunks

            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None, 81920, false);

            long totalRead = startByte;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                ProgressChanged?.Invoke(new DownloadProgress
                {
                    BytesDownloaded = totalRead,
                    TotalBytes = totalBytes,
                    Percent = (double)totalRead / totalBytes * 100
                });
            }

            fileStream.Close();
            File.Move(tempPath, targetPath, overwrite: true);

            StateChanged?.Invoke(DownloadState.Completed);
        }
        catch (OperationCanceledException)
        {
            StateChanged?.Invoke(DownloadState.Cancelled);
            throw;
        }
        catch (Exception ex)
        {
            StateChanged?.Invoke(DownloadState.Failed);
            throw new InvalidOperationException($"Échec du téléchargement : {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Vérifie si le modèle est déjà téléchargé.
    /// </summary>
    public bool IsModelDownloaded(EmbeddedLlmConfig config)
    {
        var path = Path.Combine(config.ModelsDirectory, config.ModelFileName);
        return File.Exists(path) && new FileInfo(path).Length > config.ExpectedSizeBytes * 0.95;
    }

    /// <summary>
    /// Supprime le modèle pour libérer de l'espace.
    /// </summary>
    public void DeleteModel(EmbeddedLlmConfig config)
    {
        var path = Path.Combine(config.ModelsDirectory, config.ModelFileName);
        if (File.Exists(path)) File.Delete(path);
    }
}

public class DownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double Percent { get; set; }
}

public enum DownloadState
{
    Idle,
    Starting,
    Resuming,
    Downloading,
    Completed,
    Cancelled,
    Failed
}
