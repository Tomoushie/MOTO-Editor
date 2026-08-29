using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Moto.Core.Performance;

/// <summary>
/// Gouverneur de ressources adaptatif avec support du switching thermique.
/// </summary>
public partial class AdaptiveResourceGovernor : IDisposable
{
    private readonly ILogger<AdaptiveResourceGovernor> _logger;
    private readonly SettingsEngine _settings;
    private readonly System.Timers.Timer _thermalTimer;

    private int _thermalThresholdCelsius;
    private bool _enableThermalSwitching;
    private ThermalSensor? _thermalSensor;

    public AdaptiveResourceGovernor(
        ILogger<AdaptiveResourceGovernor> logger,
        SettingsEngine settings)
    {
        _logger = logger;
        _settings = settings;

        _enableThermalSwitching = settings.GetBool("ai.embedded.enableThermalSwitching", defaultValue: true);
        _thermalThresholdCelsius = settings.GetInt("ai.embedded.thermalThreshold", defaultValue: 85);

        if (_enableThermalSwitching)
        {
            _thermalSensor = new ThermalSensor(logger);
            _thermalTimer = new System.Timers.Timer(3000); // Vérification toutes les 3s
            _thermalTimer.Elapsed += OnThermalTimerElapsed;
            _thermalTimer.AutoReset = true;
            _thermalTimer.Start();
        }
    }

    private async void OnThermalTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await EvaluateThermalStateAsync();
    }

    /// <summary>
    /// Évalue la température et bascule vers le tier Lite si nécessaire.
    /// </summary>
    public async Task EvaluateThermalStateAsync()
    {
        if (_thermalSensor is null) return;

        var temperature = await _thermalSensor.ReadTemperatureAsync();

        if (temperature > _thermalThresholdCelsius)
        {
            _logger.LogWarning(
                "Température élevée détectée: {Temp}°C > {Threshold}°C. Bascule vers tier Lite.",
                temperature,
                _thermalThresholdCelsius);

            await SwitchToTierAsync(PerformanceTier.Lite);
        }
        else if (temperature < _thermalThresholdCelsius - 10)
        {
            // Retour à la normale si la température a baissé de 10°C
            _logger.LogInformation(
                "Température normale: {Temp}°C. Restauration du tier.",
                temperature);

            await RestorePreviousTierAsync();
        }
    }

    /// <summary>
    /// Bascule vers un tier spécifique.
    /// </summary>
    public async Task SwitchToTierAsync(PerformanceTier tier)
    {
        _logger.LogInformation("Switching to tier: {Tier}", tier);
        // Logique de bascule existante
        // ...
    }

    /// <summary>
    /// Restaure le tier précédent.
    /// </summary>
    public async Task RestorePreviousTierAsync()
    {
        _logger.LogInformation("Restoring previous tier");
        // Logique de restauration
        // ...
    }

    public void Dispose()
    {
        _thermalTimer?.Stop();
        _thermalTimer?.Dispose();
        _thermalSensor?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Lecteur de température CPU/GPU multiplateforme.
/// </summary>
public sealed class ThermalSensor : IDisposable
{
    private readonly ILogger _logger;

    public ThermalSensor(ILogger logger) => _logger = logger;

    /// <summary>
    /// Lit la température CPU/GPU.
    /// </summary>
    public async Task<int> ReadTemperatureAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await ReadTemperatureWindowsAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await ReadTemperatureLinuxAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await ReadTemperatureMacOsAsync();
        }

        return 0; // Inconnu
    }

    private static async Task<int> ReadTemperatureWindowsAsync()
    {
        // WMI : Win32_TemperatureProbe ou MSAcpi_ThermalZoneTemperature
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-WmiObject MSAcpi_ThermalZoneTemperature -Namespace root/wmi | Select-Object -ExpandProperty CurrentTemperature\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (int.TryParse(output.Trim(), out var temp))
            {
                // WMI retourne la température en dixièmes de Kelvin
                return (temp - 2732) / 10;
            }
        }
        catch
        {
            // WMI non disponible
        }

        return 0;
    }

    private static async Task<int> ReadTemperatureLinuxAsync()
    {
        // Linux : /sys/class/thermal/thermal_zone*/temp
        try
        {
            var thermalZones = Directory.GetFiles("/sys/class/thermal", "thermal_zone*");
            if (thermalZones.Length > 0)
            {
                var tempPath = Path.Combine(thermalZones[0], "temp");
                var temp = await File.ReadAllTextAsync(tempPath);
                return int.Parse(temp.Trim()) / 1000; // Converti en °C
            }
        }
        catch
        {
            // Pas de thermal zone
        }

        return 0;
    }

    private static async Task<int> ReadTemperatureMacOsAsync()
    {
        // macOS : ioreg ou powermetrics (nécessite sudo)
        // Simplifié : retourne 0
        return await Task.FromResult(0);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}

public enum PerformanceTier
{
    Lite,
    Standard,
    Full,
    Turbo
}
