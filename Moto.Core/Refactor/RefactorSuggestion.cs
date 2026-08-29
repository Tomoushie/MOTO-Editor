namespace Moto.Core.Refactor;

public class RefactorSuggestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // ExtractMethod, Rename, Simplify, Inline, Reorder
    public string OriginalCode { get; set; } = string.Empty;
    public string RefactoredCode { get; set; } = string.Empty;
    public string Diff { get; set; } = string.Empty;
    public double Score { get; set; } = 0.5;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
