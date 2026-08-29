namespace Moto.Core.Plugins.Marketplace;

/// <summary>
/// Modération marketplace : signalements + revue automatique.
/// </summary>
public sealed class MarketplaceModerationService
{
    public async Task ReportPluginAsync(string pluginId, string reason, string userId)
    {
        // TODO: appel API ModerationController
        await Task.CompletedTask;
    }

    public async Task<List<PluginReport>> GetReportsAsync()
    {
        // TODO: appel API ModerationController
        return new List<PluginReport>();
    }
}

public class PluginReport
{
    public string Id { get; set; } = "";
    public string PluginId { get; set; } = "";
    public string ReporterId { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public ReportStatus Status { get; set; }
}

public enum ReportStatus
{
    Pending,
    Reviewed,
    Dismissed,
    Banned
}
