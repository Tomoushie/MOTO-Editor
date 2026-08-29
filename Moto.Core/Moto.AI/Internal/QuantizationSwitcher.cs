using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Bascule dynamiquement entre les niveaux de quantification (Q4 → Q3 → Q2)
/// selon la charge RAM/CPU. Réduit la mémoire sous pression.
/// </summary>
public sealed class QuantizationSwitcher : IDisposable
{
    private readonly ILogger<QuantizationSwitcher> _logger;
    private readonly SettingsEngine _settings;
    private readonly EmbeddedLlmEngine _engine;
    private readonly System.Timers.Timer _timer;

    private QuantizationLevel _currentLevel = QuantizationLevel.Q4;
    private readonly object _lock = new();

    public QuantizationSwitcher(
        ILogger<QuantizationSwitcher> logger,
        SettingsEngine settings,
        EmbeddedLlmEngine engine)
    {
        _logger = logger;
        _settings = settings;
        _engine = engine;

        _timer = new System.Timers.Timer(5000); // Vérification toutes les 5s
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public QuantizationLevel CurrentLevel => _currentLevel;

    private async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await EvaluateAndSwitchAsync();
    }

    /// <summary>
    /// Évalue la charge système et bascule le niveau de quantification si nécessaire.
    /// </summary>
    public async Task EvaluateAndSwitchAsync()
    {
        var ramPressure = GetRamPressure();
        var cpuPressure = GetCpuPressure();

        var targetLevel = DetermineTargetLevel(ramPressure, cpuPressure);

        if (targetLevel != _currentLevel)
        {
            _logger.LogInformation(
                "Quantization switch: {From} → {To} (RAM: {Ram}%, CPU: {Cpu}%)",
                _currentLevel,
                targetLevel,
                ramPressure,
                cpuPressure);

            await SwitchToLevelAsync(targetLevel);
        }
    }

    private QuantizationLevel DetermineTargetLevel(int ramPressure, int cpuPressure)
    {
        // Règles de bascule :
        // RAM > 80% → Q3
        // RAM > 90% → Q2
        // RAM < 60% → Q4 (retour à la normale)

        if (ramPressure > 90) return QuantizationLevel.Q2;
        if (ramPressure > 80) return QuantizationLevel.Q3;
        if (ramPressure < 60) return QuantizationLevel.Q4;

        return _currentLevel; // Pas de changement
    }

    private async Task SwitchToLevelAsync(QuantizationLevel level)
    {
        lock (_lock)
        {
            _currentLevel = level;
        }

        // Décharge le modèle actuel
        _engine.UnloadModel();

        // Recharge avec la nouvelle quantification
        var modelPath = GetModelPathForLevel(level);
        await _engine.LoadModelAsync(modelPath);

        _logger.LogInformation("Modèle rechargé avec quantification {Level}", level);
    }

    private static string GetModelPathForLevel(QuantizationLevel level)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "Models");

        var fileName = level switch
        {
            QuantizationLevel.Q2 => "phi-3-mini-q2.onnx",
            QuantizationLevel.Q3 => "phi-3-mini-q3.onnx",
            _ => "phi-3-mini-q4.onnx"
        };

        return Path.Combine(baseDir, fileName);
    }

    private static int GetRamPressure()
    {
        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64;
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        return (int)(workingSet * 100.0 / totalMemory);
    }

    private static int GetCpuPressure()
    {
        // Simplifié : à remplacer par une mesure réelle
        return Environment.ProcessorCount > 4 ? 20 : 50;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}

public enum QuantizationLevel
{
    Q2,
    Q3,
    Q4
}
