// ⚠️ Fichier partial — NE PAS toucher à CommandPaletteService.cs existant.
namespace Moto.Editor.Services;

public partial class CommandPaletteService
{
    private CommandPaletteHistoryService? _history;

    /// <summary>Injection différée de l'historique (compatible DI existante).</summary>
    public void AttachHistory(CommandPaletteHistoryService history) => _history = history;

    /// <summary>
    /// Version ordonnée par historique+fuzzy. À appeler à la place de l'ancien tri,
    /// sans supprimer l'ancienne méthode (conservée comme fallback).
    /// </summary>
    public IReadOnlyList<PaletteCommand> RankCommands(
        string query, IEnumerable<PaletteCommand> commands, string? context = null)
    {
        if (_history == null) return commands.ToList();

        return commands
            .OrderByDescending(c => _history.Score(c.Id, query, context))
            .ToList();
    }

    /// <summary>À appeler quand une commande est exécutée.</summary>
    public Task NotifyExecutedAsync(string commandId, string? context = null)
        => _history?.RecordUsageAsync(commandId, context) ?? Task.CompletedTask;
}

/// <summary>DTO minimal si PaletteCommand n'est pas déjà exposé.</summary>
public class PaletteCommand
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Shortcut { get; set; }
    public Action? Action { get; set; }
}
