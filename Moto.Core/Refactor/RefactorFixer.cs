namespace Moto.Core.Refactor;

public class RefactorFixer
{
    public async Task<string> ApplyFixAsync(string originalCode, RefactorSuggestion suggestion)
    {
        // Applique le diff
        var lines = originalCode.Split('\n').ToList();
        var refactoredLines = suggestion.RefactoredCode.Split('\n');

        // Remplace les lignes concernées
        if (suggestion.LineStart < lines.Count && suggestion.LineEnd < lines.Count)
        {
            lines.RemoveRange(suggestion.LineStart, suggestion.LineEnd - suggestion.LineStart + 1);
            lines.InsertRange(suggestion.LineStart, refactoredLines);
        }

        return string.Join('\n', lines);
    }

    public string GenerateDiff(RefactorSuggestion suggestion)
    {
        var original = suggestion.OriginalCode.Split('\n');
        var refactored = suggestion.RefactoredCode.Split('\n');

        var diff = new System.Text.StringBuilder();
        diff.AppendLine("--- Original");
        diff.AppendLine("+++ Refactored");

        foreach (var line in original)
        {
            if (!refactored.Contains(line))
                diff.AppendLine($"- {line}");
        }

        foreach (var line in refactored)
        {
            if (!original.Contains(line))
                diff.AppendLine($"+ {line}");
        }

        return diff.ToString();
    }
}
