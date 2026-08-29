using Moto.Core.Plugins;
using Moto.Core.Refactor;

namespace Moto.Plugins.AutoRefactorPro;

public class AutoRefactorProPlugin : IMotoPlugin
{
    public string Id => "auto-refactor-pro";
    public string Name => "AutoRefactor Pro";
    public string Version => "1.0.0";
    public string Author => "MOTO Team";
    public string Description => "Refactorisation automatique avancée avec XENO + Roslyn + apprentissage";

    private RefactorEngine? _engine;
    private IPluginContext? _context;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _engine = new RefactorEngine();

        // Enregistre les commandes slash
        context.RegisterCommand("/refactor", HandleRefactorCommand);
        context.RegisterCommand("/refactor all", HandleRefactorAllCommand);
        context.RegisterCommand("/refactor fix", HandleRefactorFixCommand);
        context.RegisterCommand("/refactor preview", HandleRefactorPreviewCommand);

        return Task.CompletedTask;
    }

    public Task ActivateAsync()
    {
        _context?.Logger.Info("AutoRefactor Pro activé");
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _context?.Logger.Info("AutoRefactor Pro désactivé");
        return Task.CompletedTask;
    }

    private async Task HandleRefactorCommand(CommandContext ctx)
    {
        var currentFile = ctx.GetCurrentFile();
        if (currentFile == null)
        {
            ctx.ShowMessage("Aucun fichier ouvert");
            return;
        }

        var code = await File.ReadAllTextAsync(currentFile.Path);
        var suggestions = await _engine!.AnalyzeAsync(code, currentFile.Path);

        ctx.ShowRefactorPanel(suggestions);
    }

    private async Task HandleRefactorAllCommand(CommandContext ctx)
    {
        var projectFiles = ctx.GetProjectFiles("*.cs");
        var allSuggestions = new List<RefactorSuggestion>();

        foreach (var file in projectFiles)
        {
            var code = await File.ReadAllTextAsync(file.Path);
            var suggestions = await _engine!.AnalyzeAsync(code, file.Path);
            allSuggestions.AddRange(suggestions);
        }

        ctx.ShowRefactorPanel(allSuggestions.OrderByDescending(s => s.Score).Take(50).ToList());
    }

    private async Task HandleRefactorFixCommand(CommandContext ctx, string suggestionId)
    {
        var suggestion = ctx.GetSuggestionById(suggestionId);
        if (suggestion == null)
        {
            ctx.ShowMessage("Suggestion introuvable");
            return;
        }

        var currentFile = ctx.GetCurrentFile();
        var code = await File.ReadAllTextAsync(currentFile!.Path);
        var refactored = await _engine!.ApplyFixAsync(code, suggestion);

        await File.WriteAllTextAsync(currentFile.Path, refactored);
        ctx.ShowMessage($"Correction appliquée : {suggestion.Description}");
    }

    private Task HandleRefactorPreviewCommand(CommandContext ctx, string suggestionId)
    {
        var suggestion = ctx.GetSuggestionById(suggestionId);
        if (suggestion == null)
        {
            ctx.ShowMessage("Suggestion introuvable");
            return Task.CompletedTask;
        }

        ctx.ShowDiffPreview(suggestion.Diff);
        return Task.CompletedTask;
    }

    public void OnDocumentChanged(DocumentContext doc)
    {
        // Analyse en temps réel (optionnel, désactivé par défaut pour la performance)
    }

    public void OnFeedbackReceived(string suggestionId, FeedbackType type, string? userVariant = null)
    {
        _engine?.RecordFeedback(suggestionId, type, userVariant);
    }
}
