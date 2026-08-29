// Moto.Core/Plugins/PluginManifestPro.cs — AJOUTS
public sealed class PluginManifestPro
{
    // ... propriétés existantes ...

    // ── Métadonnées étendues ──
    public long DownloadCount { get; set; }
    public DateTime PublishedUtc { get; init; }
    public DateTime LastUpdatedUtc { get; set; }
    public long LikeCount { get; set; }
    public long ReportCount { get; set; }
    public List<string> LikedBy { get; set; } = new(); // UserIds
    public List<ContentReport> Reports { get; set; } = new();
}

public sealed class ContentReport
{
    public string UserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty; // spam, malware, inappropriate, other
    public string? Details { get; init; }
    public DateTime ReportedUtc { get; init; } = DateTime.UtcNow;
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
}

public enum ReportStatus { Pending, Reviewed, Resolved, Dismissed }
