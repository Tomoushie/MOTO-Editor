using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.DevOps;

public sealed class JourneyStep
{
    public string Name { get; set; } = "";
    public Func<Task<bool>> Action { get; set; } = () => Task.FromResult(true);
}

public sealed class JourneyResult
{
    public string JourneyName { get; set; } = "";
    public bool Success { get; set; }
    public List<string> FailedSteps { get; set; } = new();
}

/// <summary>
/// Item 93 — User journeys synthétiques : exécutions headless nocturnes
/// qui parcourent les flux courants et signalent les régressions.
/// </summary>
public sealed class SyntheticJourneyService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<(string name, List<JourneyStep> steps)> _journeys = new();

    public SyntheticJourneyService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void RegisterJourney(string name, List<JourneyStep> steps)
    {
        _journeys.Add((name, steps));
    }

    public async Task<List<JourneyResult>> RunAllAsync()
    {
        var results = new List<JourneyResult>();
        if (!SettingsCatalog.DevOps.SyntheticJourneysEnabled.Value) return results;

        foreach (var (name, steps) in _journeys)
        {
            var result = new JourneyResult { JourneyName = name, Success = true };
            foreach (var step in steps)
            {
                try
                {
                    bool ok = await step.Action();
                    if (!ok)
                    {
                        result.Success = false;
                        result.FailedSteps.Add(step.Name);
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.FailedSteps.Add($"{step.Name} ({ex.Message})");
                }
            }
            results.Add(result);
            _log.Info("SyntheticJourney", $"Journey '{name}' : {(result.Success ? "OK" : "ÉCHEC")}");
        }
        return results;
    }
}
