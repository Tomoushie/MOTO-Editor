// Moto.Core/AI/Embedded/ModelBundleManager.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Gère le téléchargement groupé des modèles nécessaires aux optimisations :
/// - Draft model (500M, ~300 MB) → speculative decoding
/// - Small model (1.5B, ~1 GB) → dual routing
/// - Main model (7B, ~4.5 GB) → déjà géré par ModelDownloader
/// </summary>
public sealed class ModelBundleManager
{
    private readonly ModelDownloader _downloader;
    private readonly List<ModelBundleEntry> _entries;

    public static ModelBundleManager? Instance { get; private set; }

    public event Action<ModelBundleProgress>? BundleProgressChanged;

    public ModelBundleManager(ModelDownloader downloader)
    {
        _downloader = downloader;
        _entries = new List<ModelBundleEntry>
        {
            new()
            {
                Id = "draft",
                Name = "Draft model (500M)",
                Config = new EmbeddedLlmConfig
                {
                    ModelFileName = "qwen2.5-0.5b-q4.onnx",
                    DownloadUrl = "https://huggingface.co/moto-editor/models/resolve/main/qwen2.5-0.5b-q4.onnx",
                    ExpectedSizeBytes = 300_000_000
                },
                Purpose = "Speculative decoding"
            },
            new()
            {
                Id = "small",
                Name = "Small model (1.5B)",
                Config = new EmbeddedLlmConfig
                {
                    ModelFileName = "qwen2.5-1.5b-q4.onnx",
                    DownloadUrl = "https://huggingface.co/moto-editor/models/resolve/main/qwen2.5-1.5b-q4.onnx",
                    ExpectedSizeBytes = 1_000_000_000
                },
                Purpose = "Dual model routing"
            }
        };
        Instance = this;
    }

    /// <summary>
    /// Télécharge tous les modèles manquants.
    /// </summary>
    public async Task DownloadAllAsync(CancellationToken ct = default)
    {
        int total = _entries.Count;
        int current = 0;

        foreach (var entry in _entries)
        {
            if (_downloader.IsModelDownloaded(entry.Config))
            {
                current++;
                BundleProgressChanged?.Invoke(new ModelBundleProgress
                {
                    CurrentIndex = current,
                    TotalCount = total,
                    CurrentEntry = entry,
                    Status = BundleStatus.Skipped
                });
                continue;
            }

            BundleProgressChanged?.Invoke(new ModelBundleProgress
            {
                CurrentIndex = current,
                TotalCount = total,
                CurrentEntry = entry,
                Status = BundleStatus.Downloading
            });

            try
            {
                await _downloader.DownloadAsync(entry.Config, ct);
                BundleProgressChanged?.Invoke(new ModelBundleProgress
                {
                    CurrentIndex = current + 1,
                    TotalCount = total,
                    CurrentEntry = entry,
                    Status = BundleStatus.Completed
                });
            }
            catch (OperationCanceledException)
            {
                BundleProgressChanged?.Invoke(new ModelBundleProgress
                {
                    CurrentIndex = current,
                    TotalCount = total,
                    CurrentEntry = entry,
                    Status = BundleStatus.Cancelled
                });
                throw;
            }
            catch
            {
                BundleProgressChanged?.Invoke(new ModelBundleProgress
                {
                    CurrentIndex = current,
                    TotalCount = total,
                    CurrentEntry = entry,
                    Status = BundleStatus.Failed
                });
            }

            current++;
        }
    }

    /// <summary>
    /// Vérifie quels modèles sont disponibles.
    /// </summary>
    public IReadOnlyList<ModelBundleStatus> GetStatuses()
    {
        var result = new List<ModelBundleStatus>();
        foreach (var entry in _entries)
        {
            result.Add(new ModelBundleStatus
            {
                Entry = entry,
                IsDownloaded = _downloader.IsModelDownloaded(entry.Config)
            });
        }
        return result;
    }
}

public class ModelBundleEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public EmbeddedLlmConfig Config { get; set; } = new();
    public string Purpose { get; set; } = "";
}

public class ModelBundleProgress
{
    public int CurrentIndex { get; set; }
    public int TotalCount { get; set; }
    public ModelBundleEntry CurrentEntry { get; set; } = new();
    public BundleStatus Status { get; set; }
}

public class ModelBundleStatus
{
    public ModelBundleEntry Entry { get; set; } = new();
    public bool IsDownloaded { get; set; }
}

public enum BundleStatus
{
    Pending,
    Downloading,
    Completed,
    Skipped,
    Failed,
    Cancelled
}
