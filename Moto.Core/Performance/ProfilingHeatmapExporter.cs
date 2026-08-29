// Moto.Core/Performance/ProfilingHeatmapExporter.cs
using System.Text.Json;

namespace Moto.Core.Performance;

/// <summary>
/// Exporte les hotspots de performance en JSON pour analyse CI.
/// </summary>
public sealed class ProfilingHeatmapExporter
{
    private readonly PerformanceProfiler _profiler;

    public static ProfilingHeatmapExporter Instance { get; private set; } = null!;

    public ProfilingHeatmapExporter(PerformanceProfiler profiler)
    {
        _profiler = profiler;
        Instance = this;
    }

    /// <summary>
    /// Exporte les hotspots en JSON.
    /// </summary>
    public async Task ExportAsync(string outputPath)
    {
        var metrics = _profiler.GetMetrics();
        var heatmap = new ProfilingHeatmap
        {
            Timestamp = DateTime.UtcNow,
            Hotspots = metrics.Values
                .OrderByDescending(m => m.Value)
                .Take(20)
                .Select(m => new Hotspot
                {
                    Name = m.Name,
                    Value = m.Value,
                    Unit = m.Unit
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(heatmap, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json);
    }
}

public class ProfilingHeatmap
{
    public DateTime Timestamp { get; set; }
    public List<Hotspot> Hotspots { get; set; } = new();
}

public class Hotspot
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}
