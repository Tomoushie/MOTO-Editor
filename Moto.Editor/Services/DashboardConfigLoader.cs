using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Editor.Services;

/// <summary>
/// Charge le template de dashboard et fournit les seuils d'alerte.
/// </summary>
public sealed class DashboardConfigLoader
{
    private readonly ILogger<DashboardConfigLoader> _logger;
    private DashboardTemplate? _template;

    public DashboardConfigLoader(ILogger<DashboardConfigLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Charge le template depuis le fichier JSON.
    /// </summary>
    public async Task<DashboardTemplate?> LoadAsync(string? configPath = null)
    {
        var path = configPath ?? GetDefaultConfigPath();

        try
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("Template de dashboard introuvable: {Path}", path);
                return null;
            }

            var json = await File.ReadAllTextAsync(path);
            _template = JsonSerializer.Deserialize<DashboardTemplate>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Template de dashboard chargé: {Path}", path);
            return _template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec du chargement du template de dashboard.");
            return null;
        }
    }

    /// <summary>
    /// Vérifie si une métrique dépasse son seuil d'alerte.
    /// </summary>
    public AlertResult? CheckThreshold(string metricId, double value)
    {
        if (_template is null) return null;

        var metric = _template.FindMetric(metricId);
        if (metric is null) return null;

        if (metric.Thresholds.TryGetValue("critical", out var critical) && value >= critical)
        {
            return new AlertResult { Severity = AlertSeverity.Critical, Message = metric.Alert?.Message ?? $"Seuil critique dépassé: {value}" };
        }

        if (metric.Thresholds.TryGetValue("warning", out var warning) && value >= warning)
        {
            return new AlertResult { Severity = AlertSeverity.Warning, Message = metric.Alert?.Message ?? $"Seuil d'alerte dépassé: {value}" };
        }

        return null;
    }

    private static string GetDefaultConfigPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "config", "dashboard-template.json");
    }
}

public class DashboardTemplate
{
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public MetricsConfig Metrics { get; set; } = new();
    public PanelsConfig Panels { get; set; } = new();
    public AlertsConfig Alerts { get; set; } = new();

    public MetricDefinition? FindMetric(string id)
    {
        return Metrics.Performance.FirstOrDefault(m => m.Id == id)
            ?? Metrics.Resources.FirstOrDefault(m => m.Id == id)
            ?? Metrics.Reliability.FirstOrDefault(m => m.Id == id);
    }
}

public class MetricsConfig
{
    public List<MetricDefinition> Performance { get; set; } = new();
    public List<MetricDefinition> Resources { get; set; } = new();
    public List<MetricDefinition> Reliability { get; set; } = new();
}

public class MetricDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, double> Thresholds { get; set; } = new();
    public AlertConfig? Alert { get; set; }
}

public class AlertConfig
{
    public string Condition { get; set; } = "";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = "warning";
}

public class PanelsConfig
{
    public string Layout { get; set; } = "grid";
    public int Columns { get; set; } = 3;
    public List<PanelItem> Items { get; set; } = new();
}

public class PanelItem
{
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Metrics { get; set; } = new();
    public int Span { get; set; } = 1;
}

public class AlertsConfig
{
    public List<string> Channels { get; set; } = new();
    public List<AlertRule> Rules { get; set; } = new();
}

public class AlertRule
{
    public string Name { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Severity { get; set; } = "";
    public List<string> Actions { get; set; } = new();
}

public class AlertResult
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = "";
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
