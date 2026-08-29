using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Editor.Services;

/// <summary>
/// Historique persistant + scoring contextuel/fuzzy pour la palette de commandes.
/// Priorise : commandes récentes > commandes contextuelles > score fuzzy.
/// </summary>
public sealed class CommandPaletteHistoryService
{
    private readonly ILogger<CommandPaletteHistoryService> _logger;
    private readonly Dictionary<string, CommandUsage> _usage = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string _storePath;

    public CommandPaletteHistoryService(ILogger<CommandPaletteHistoryService> logger)
    {
        _logger = logger;
        _storePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor", "palette-history.json");
    }

    /// <summary>Charge l'historique au démarrage.</summary>
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_storePath)) return;
            var json = await File.ReadAllTextAsync(_storePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, CommandUsage>>(json);
            if (loaded != null)
            {
                foreach (var kv in loaded) _usage[kv.Key] = kv.Value;
            }
            _logger.LogInformation("Palette history chargé : {Count} entrées.", _usage.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec chargement historique palette.");
        }
    }

    /// <summary>Enregistre l'exécution d'une commande.</summary>
    public async Task RecordUsageAsync(string commandId, string? context = null)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_usage.TryGetValue(commandId, out var usage))
            {
                usage = new CommandUsage { CommandId = commandId };
                _usage[commandId] = usage;
            }
            usage.Count++;
            usage.LastUsed = DateTime.UtcNow;
            if (context != null) usage.LastContext = context;

            await PersistAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Score une commande : récence + fréquence + contexte + fuzzy match.
    /// </summary>
    public double Score(string commandId, string query, string? currentContext)
    {
        _usage.TryGetValue(commandId, out var usage);
        var score = 0.0;

        // 1. Récence (decay exponentiel, demi-vie 7 jours)
        if (usage != null)
        {
            var daysSince = (DateTime.UtcNow - usage.LastUsed).TotalDays;
            score += 50.0 * Math.Pow(0.5, daysSince / 7.0);

            // 2. Fréquence (log pour éviter domination)
            score += 10.0 * Math.Log(1 + usage.Count);

            // 3. Contexte
            if (currentContext != null && usage.LastContext == currentContext)
                score += 25.0;
        }

        // 4. Fuzzy match sur le nom
        score += 20.0 * FuzzyScore(query, commandId);

        return score;
    }

    /// <summary>Score fuzzy sous-séquence (type Sublime/VS Code).</summary>
    public static double FuzzyScore(string query, string candidate)
    {
        if (string.IsNullOrEmpty(query)) return 1.0;
        query = query.ToLowerInvariant();
        candidate = candidate.ToLowerInvariant();

        var qi = 0; var consecutive = 0; var score = 0.0;
        for (var ci = 0; ci < candidate.Length && qi < query.Length; ci++)
        {
            if (candidate[ci] == query[qi])
            {
                qi++;
                consecutive++;
                score += 1.0 + consecutive * 0.5; // bonus consécutif
            }
            else consecutive = 0;
        }
        return qi == query.Length ? score / (query.Length * 2.0) : 0.0;
    }

    private async Task PersistAsync()
    {
        var dir = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_usage);
        await File.WriteAllTextAsync(_storePath, json);
    }
}

public class CommandUsage
{
    public string CommandId { get; set; } = "";
    public int Count { get; set; }
    public DateTime LastUsed { get; set; } = DateTime.MinValue;
    public string? LastContext { get; set; }
}
