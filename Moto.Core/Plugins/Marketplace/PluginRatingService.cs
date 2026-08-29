using System.Text.Json;

namespace Moto.Core.Plugins.Marketplace;

public sealed class PluginRatingService
{
    private readonly string _ratingsPath;

    public PluginRatingService()
    {
        _ratingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MotoEditor", "ratings.json");
    }

    public async Task SubmitRatingAsync(string pluginId, int stars, string review)
    {
        var ratings = await LoadRatingsAsync();
        if (!ratings.ContainsKey(pluginId)) ratings[pluginId] = new List<PluginReview>();

        ratings[pluginId].Add(new PluginReview
        {
            Stars = stars,
            Review = review,
            Date = DateTime.UtcNow,
            UserId = "current_user_id" // À remplacer par l'ID réel
        });

        await SaveRatingsAsync(ratings);
    }

    public async Task<PluginStats> GetStatsAsync(string pluginId)
    {
        var ratings = await LoadRatingsAsync();
        if (!ratings.ContainsKey(pluginId) || ratings[pluginId].Count == 0)
            return new PluginStats { AverageRating = 0, TotalReviews = 0 };

        var reviews = ratings[pluginId];
        return new PluginStats
        {
            AverageRating = Math.Round(reviews.Average(r => r.Stars), 1),
            TotalReviews = reviews.Count,
            RecentReviews = reviews.OrderByDescending(r => r.Date).Take(3).ToList()
        };
    }

    private async Task<Dictionary<string, List<PluginReview>>> LoadRatingsAsync()
    {
        if (!File.Exists(_ratingsPath)) return new();
        var json = await File.ReadAllTextAsync(_ratingsPath);
        return JsonSerializer.Deserialize<Dictionary<string, List<PluginReview>>>(json) ?? new();
    }

    private async Task SaveRatingsAsync(Dictionary<string, List<PluginReview>> ratings)
    {
        var json = JsonSerializer.Serialize(ratings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_ratingsPath, json);
    }
}

public class PluginReview { public int Stars { get; set; } public string Review { get; set; } = ""; public DateTime Date { get; set; } public string UserId { get; set; } = ""; }
public class PluginStats { public double AverageRating { get; set; } public int TotalReviews { get; set; } public List<PluginReview> RecentReviews { get; set; } = new(); }
