using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Moto.Core.Logging;

namespace Moto.Core.DevOps;

/// <summary>
/// Item 96 — Local reproducible perf bundles : capture perf trace + repro minimal
/// à partager avec les devs.
/// </summary>
public sealed class PerfBundleService
{
    private readonly StructuredLogCollector _log;
    private readonly PerfGateService _perfGate;

    public PerfBundleService(StructuredLogCollector log, PerfGateService perfGate)
    {
        _log = log;
        _perfGate = perfGate;
    }

    public async Task<string> CreateBundleAsync(string? reproDescription = null)
    {
        var bundleDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "perf-bundles");
        Directory.CreateDirectory(bundleDir);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string bundlePath = Path.Combine(bundleDir, $"perf-bundle-{stamp}.zip");

        var metrics = _perfGate.CaptureCurrentMetrics();
        var manifest = new
        {
            timestamp = DateTime.UtcNow,
            metrics,
            repro = reproDescription ?? "Aucune description",
            machine = Environment.MachineName
        };

        string tempDir = Path.Combine(bundleDir, $"temp-{stamp}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(_log.CurrentLogFile))
            File.Copy(_log.CurrentLogFile, Path.Combine(tempDir, "moto.log.jsonl"), overwrite: true);

        ZipFile.CreateFromDirectory(tempDir, bundlePath);
        Directory.Delete(tempDir, recursive: true);

        _log.Info("PerfBundle", "Bundle créé", new { bundlePath });
        await Task.CompletedTask;
        return bundlePath;
    }
}
