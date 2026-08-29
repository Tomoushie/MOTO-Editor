using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Services;

/// <summary>
/// Service de reprise après crash via snapshots périodiques.
/// Sauvegarde l'état de la session toutes les 30 secondes.
/// </summary>
public sealed class SnapshotResumeService : IDisposable
{
    private readonly ILogger<SnapshotResumeService> _logger;
    private readonly System.Timers.Timer _timer;
    private readonly string _snapshotDir;
    private SessionSnapshot? _lastSnapshot;

    public SnapshotResumeService(ILogger<SnapshotResumeService> logger)
    {
        _logger = logger;
        _snapshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "Snapshots");

        Directory.CreateDirectory(_snapshotDir);

        _timer = new System.Timers.Timer(30000); // 30 secondes
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await SaveSnapshotAsync();
    }

    /// <summary>
    /// Sauvegarde un snapshot de la session actuelle.
    /// </summary>
    public async Task SaveSnapshotAsync()
    {
        try
        {
            var snapshot = new SessionSnapshot
            {
                Timestamp = DateTime.UtcNow,
                OpenDocuments = GetOpenDocuments(),
                ActiveDocumentPath = GetActiveDocumentPath(),
                CursorPositions = GetCursorPositions(),
                PanelStates = GetPanelStates()
            };

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var path = Path.Combine(_snapshotDir, $"snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            await File.WriteAllTextAsync(path, json);

            // Nettoie les anciens snapshots (garde les 5 derniers)
            CleanupOldSnapshots(keepCount: 5);

            _lastSnapshot = snapshot;
            _logger.LogDebug("Snapshot sauvegardé: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec de la sauvegarde du snapshot.");
        }
    }

    /// <summary>
    /// Restaure le dernier snapshot disponible.
    /// </summary>
    public async Task<SessionSnapshot?> RestoreLastSnapshotAsync()
    {
        try
        {
            var snapshots = Directory.GetFiles(_snapshotDir, "snapshot_*.json")
                .OrderByDescending(f => f)
                .ToList();

            if (snapshots.Count == 0)
            {
                _logger.LogInformation("Aucun snapshot disponible.");
                return null;
            }

            var latestPath = snapshots[0];
            var json = await File.ReadAllTextAsync(latestPath);
            var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(json);

            _logger.LogInformation(
                "Snapshot restauré: {Path} ({Timestamp})",
                latestPath,
                snapshot?.Timestamp);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la restauration du snapshot.");
            return null;
        }
    }

    /// <summary>
    /// Vérifie si un snapshot existe (après crash).
    /// </summary>
    public bool HasSnapshotAvailable()
    {
        return Directory.Exists(_snapshotDir) &&
               Directory.GetFiles(_snapshotDir, "snapshot_*.json").Length > 0;
    }

    private void CleanupOldSnapshots(int keepCount)
    {
        var snapshots = Directory.GetFiles(_snapshotDir, "snapshot_*.json")
            .OrderByDescending(f => f)
            .Skip(keepCount)
            .ToList();

        foreach (var path in snapshots)
        {
            try { File.Delete(path); }
            catch { /* Ignore */ }
        }
    }

    // Méthodes à implémenter selon l'état réel de l'application
    private static List<string> GetOpenDocuments() => new();
    private static string? GetActiveDocumentPath() => null;
    private static Dictionary<string, int> GetCursorPositions() => new();
    private static Dictionary<string, bool> GetPanelStates() => new();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Snapshot de l'état de la session.
/// </summary>
public class SessionSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<string> OpenDocuments { get; set; } = new();
    public string? ActiveDocumentPath { get; set; }
    public Dictionary<string, int> CursorPositions { get; set; } = new();
    public Dictionary<string, bool> PanelStates { get; set; } = new();
}
