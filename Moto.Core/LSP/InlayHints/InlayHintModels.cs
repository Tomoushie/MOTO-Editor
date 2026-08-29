// Moto.Core/LSP/InlayHints/InlayHintModels.cs
namespace Moto.Core.LSP.InlayHints
{
    public enum InlayHintKind { Type, Parameter, ReturnValue }

    /// <summary>Un inlay hint à rendre dans l'éditeur.</summary>
    public sealed class InlayHint
    {
        public int Line { get; init; }
        public int Column { get; init; }
        public string Label { get; init; } = string.Empty;
        public InlayHintKind Kind { get; init; }
    }

    /// <summary>Fournisseur d'inlay hints (implémenté par Roslyn, etc.).</summary>
    public interface IInlayHintProvider
    {
        System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<InlayHint>>
            GetHintsAsync(string filePath, string content, int startLine, int endLine);
    }
}
