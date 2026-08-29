using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Moto.Core.Refactor;

public class RefactorAnalyzer
{
    public async Task<List<RefactorSuggestion>> AnalyzeAsync(string code, string filePath)
    {
        var suggestions = new List<RefactorSuggestion>();

        if (filePath.EndsWith(".cs"))
        {
            suggestions.AddRange(await AnalyzeCSharpAsync(code, filePath));
        }
        else
        {
            suggestions.AddRange(AnalyzeWithHeuristics(code, filePath));
        }

        return suggestions;
    }

    private async Task<List<RefactorSuggestion>> AnalyzeCSharpAsync(string code, string filePath)
    {
        var suggestions = new List<RefactorSuggestion>();
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = await tree.GetRootAsync();

        // Extract Method : méthodes trop longues (> 50 lignes)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var lines = method.ToString().Split('\n').Length;
            if (lines > 50)
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Description = $"Méthode '{method.Identifier.Text}' trop longue ({lines} lignes). Envisager d'extraire des sous-méthodes.",
                    Category = "ExtractMethod",
                    OriginalCode = method.ToString(),
                    RefactoredCode = GenerateExtractedMethod(method),
                    Score = 0.8,
                    LineStart = method.GetLocation().GetLineSpan().StartLinePosition.Line,
                    LineEnd = method.GetLocation().GetLineSpan().EndLinePosition.Line,
                    FilePath = filePath
                });
            }
        }

        // Simplify : var au lieu de types explicites
        var variableDeclarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>();
        foreach (var decl in variableDeclarations)
        {
            if (decl.Type.ToString() != "var" && !decl.Type.ToString().Contains("<"))
            {
                suggestions.Add(new RefactorSuggestion
                {
                    Description = "Utiliser 'var' pour simplifier la déclaration",
                    Category = "Simplify",
                    OriginalCode = decl.ToString(),
                    RefactoredCode = decl.WithType(SyntaxFactory.ParseTypeName("var")).ToString(),
                    Score = 0.6,
                    LineStart = decl.GetLocation().GetLineSpan().StartLinePosition.Line,
                    LineEnd = decl.GetLocation().GetLineSpan().EndLinePosition.Line,
                    FilePath = filePath
                });
            }
        }

        return suggestions;
    }

    private List<RefactorSuggestion> AnalyzeWithHeuristics(string code, string filePath)
    {
        var suggestions = new List<RefactorSuggestion>();
        var lines = code.Split('\n');

        // Détection de code dupliqué (heuristique simple)
        var lineGroups = lines
            .Select((line, index) => new { line = line.Trim(), index })
            .Where(x => x.line.Length > 20)
            .GroupBy(x => x.line)
            .Where(g => g.Count() > 2);

        foreach (var group in lineGroups)
        {
            suggestions.Add(new RefactorSuggestion
            {
                Description = $"Code dupliqué détecté ({group.Count()} occurrences)",
                Category = "ExtractMethod",
                OriginalCode = group.Key,
                RefactoredCode = "// Extraire en méthode commune",
                Score = 0.7,
                LineStart = group.First().index,
                LineEnd = group.First().index,
                FilePath = filePath
            });
        }

        return suggestions;
    }

    private string GenerateExtractedMethod(MethodDeclarationSyntax method)
    {
        // Simplification : génère un squelette
        return $"// TODO: Extraire la logique de {method.Identifier.Text} en sous-méthodes\n{method}";
    }
}
