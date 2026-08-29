// Moto.Tests/PerfGateHarness.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Moto.Core.DevOps;
using Moto.Core.Logging;
using Moto.Core.Settings;
using Xunit;

namespace Moto.Tests;

/// <summary>
/// Item 106 — Harness CI perf gate : produit perf-current.json pour le workflow.
/// </summary>
public class PerfGateHarness
{
    [Fact]
    public void Capture_And_Export_Perf_Metrics()
    {
        var settings = SettingsEngine.Shared;
        var log = new StructuredLogCollector();
        var gate = new PerfGateService(settings, log);

        var metrics = gate.CaptureCurrentMetrics();
        bool passes = gate.PassesGate(metrics);

        var output = new
        {
            startupTimeMs = metrics.StartupTimeMs,
            peakMemoryMb = metrics.PeakMemoryMb,
            gatePass = passes,
            capturedAtUtc = metrics.CapturedAtUtc
        };

        File.WriteAllText("perf-current.json",
            JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(true); // Le harness produit le fichier ; le workflow tranche.
    }
}
