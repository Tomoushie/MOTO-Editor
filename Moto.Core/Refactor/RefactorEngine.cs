namespace Moto.Core.Refactor;

public class RefactorEngine
{
    private readonly RefactorAnalyzer _analyzer;
    private readonly RefactorFixer _fixer;
    private readonly RefactorLearningStore _learningStore;

    public RefactorEngine()
    {
        _analyzer = new RefactorAnalyzer();
        _fixer = new RefactorFixer();
        _learningStore = new RefactorLearningStore();
    }

    public async Task<List<RefactorSuggestion>> AnalyzeAsync(string code, string filePath)
    {
        var suggestions = await _analyzer.AnalyzeAsync(code, filePath);

        // Ajuste les scores avec le feedback utilisateur
        foreach (var suggestion in suggestions)
        {
            suggestion.Score = _learningStore.GetAdjustedScore(suggestion.Id, suggestion.Score);
            suggestion.Diff = _fixer.GenerateDiff(suggestion);
        }

        // Trie par score décroissant
        return suggestions.OrderByDescending(s => s.Score).ToList();
    }

    public async Task<string> ApplyFixAsync(string originalCode, RefactorSuggestion suggestion)
    {
        return await _fixer.ApplyFixAsync(originalCode, suggestion);
    }

    public void RecordFeedback(string suggestionId, FeedbackType type, string? userVariant = null)
    {
        _learningStore.RecordFeedback(suggestionId, type, userVariant);
    }
}
