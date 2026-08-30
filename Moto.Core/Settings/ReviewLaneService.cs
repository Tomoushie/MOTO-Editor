using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Collab;

public enum ReviewCommentStatus { Open, Resolved, Dismissed }

public sealed class ReviewComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public ReviewCommentStatus Status { get; set; } = ReviewCommentStatus.Open;
}

/// <summary>
/// Idées "Lightweight code review lanes" (P1), "Offline review queue" (P2), "Review replay" (P2).
/// Persiste dans .moto/review/ (cohérent avec TimeMachine/DocEngine). MOTO Editor affiche/édite
/// les commentaires ; aucune opération structurée projet (réservée à XENO).
/// </summary>
public sealed class ReviewLaneService
{
    private static readonly string ReviewDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "review");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<ReviewComment> _comments = new();
    private readonly Queue<ReviewComment> _offlineQueue = new();
    private readonly object _lock = new();

    public bool IsOnline { get; set; } = true;
    public event EventHandler? CommentsChanged;

    public ReviewLaneService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        Directory.CreateDirectory(ReviewDir);
        Load();
    }

    public void AddComment(ReviewComment comment)
    {
        if (!SettingsCatalog.Collab.ReviewLanesEnabled.Value) return;

        lock (_lock)
        {
            if (!IsOnline && SettingsCatalog.Collab.OfflineReviewQueueEnabled.Value)
            {
                _offlineQueue.Enqueue(comment);
                _log.Info("ReviewLane", "Commentaire mis en file hors-ligne", new { comment.FilePath });
                return;
            }
            _comments.Add(comment);
        }
        Persist();
        CommentsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Idée "Offline review queue" — vide la file quand on repasse en ligne.</summary>
    public void FlushOfflineQueue()
    {
        lock (_lock)
        {
            while (_offlineQueue.Count > 0)
                _comments.Add(_offlineQueue.Dequeue());
        }
        Persist();
        _log.Info("ReviewLane", "File hors-ligne vidée");
        CommentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetStatus(string commentId, ReviewCommentStatus status)
    {
        lock (_lock)
        {
            var c = _comments.FirstOrDefault(x => x.Id == commentId);
            if (c != null) c.Status = status;
        }
        Persist();
        CommentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<ReviewComment> GetCommentsForFile(string filePath)
    {
        lock (_lock) return _comments.Where(c => c.FilePath == filePath).ToList();
    }

    /// <summary>Idée "Review replay" — séquence ordonnée des actions d'un reviewer.</summary>
    public IReadOnlyList<ReviewComment> GetReplaySequence(string reviewer)
    {
        lock (_lock)
            return _comments.Where(c => c.Author == reviewer)
                            .OrderBy(c => c.TimestampUtc).ToList();
    }

    private void Persist()
    {
        lock (_lock)
        {
            var path = Path.Combine(ReviewDir, "comments.json");
            File.WriteAllText(path, JsonSerializer.Serialize(_comments));
        }
    }

    private void Load()
    {
        var path = Path.Combine(ReviewDir, "comments.json");
        if (!File.Exists(path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<ReviewComment>>(File.ReadAllText(path));
            if (loaded != null) lock (_lock) { _comments.Clear(); _comments.AddRange(loaded); }
        }
        catch (Exception ex) { _log.Error("ReviewLane", "Échec chargement", new { ex.Message }); }
    }
}
