using System.Text.Json;

namespace Moto.Core.Refactor;

public class RefactorLearningStore
{
    private readonly string _storePath;
    private Dictionary<string, FeedbackEntry> _feedback;

    public RefactorLearningStore()
    {
        _storePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor",
            "refactor-feedback.json"
        );
        _feedback = LoadFeedback();
    }

    public void RecordFeedback(string suggestionId, FeedbackType type, string? userVariant = null)
    {
        if (!_feedback.ContainsKey(suggestionId))
        {
            _feedback[suggestionId] = new FeedbackEntry { SuggestionId = suggestionId };
        }

        var entry = _feedback[suggestionId];
        entry.TotalFeedback++;

        switch (type)
        {
            case FeedbackType.Like:
                entry.Likes++;
                entry.Score = Math.Min(1.0, entry.Score + 0.1);
                break;
            case FeedbackType.Dislike:
                entry.Dislikes++;
                entry.Score = Math.Max(0.0, entry.Score - 0.1);
                break;
            case FeedbackType.Edit:
                entry.Edits++;
                entry.UserVariant = userVariant;
                break;
        }

        SaveFeedback();
    }

    public double GetAdjustedScore(string suggestionId, double baseScore)
    {
        if (_feedback.TryGetValue(suggestionId, out var entry))
        {
            return entry.Score;
        }
        return baseScore;
    }

    private Dictionary<string, FeedbackEntry> LoadFeedback()
    {
        if (File.Exists(_storePath))
        {
            var json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<Dictionary<string, FeedbackEntry>>(json) ?? new();
        }
        return new();
    }

    private void SaveFeedback()
    {
        var json = JsonSerializer.Serialize(_feedback, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        File.WriteAllText(_storePath, json);
    }
}

public class FeedbackEntry
{
    public string SuggestionId { get; set; } = string.Empty;
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    public int Edits { get; set; }
    public int TotalFeedback { get; set; }
    public double Score { get; set; } = 0.5;
    public string? UserVariant { get; set; }
}

public enum FeedbackType
{
    Like,
    Dislike,
    Edit
}
