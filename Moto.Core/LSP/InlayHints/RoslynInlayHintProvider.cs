// Moto.Core/LSP/InlayHints/RoslynLspInlayHintProvider.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Core.LSP.InlayHints
{
    /// <summary>
    /// Fournisseur d'inlay hints basé sur le LSP Roslyn réel.
    /// Remplace RoslynInlayHintProvider (regex) par le vrai LSP.
    /// </summary>
    public sealed class RoslynLspInlayHintProvider : IInlayHintProvider
    {
        private readonly LanguageServerManager _lspManager;

        public RoslynLspInlayHintProvider(LanguageServerManager lspManager)
        {
            _lspManager = lspManager;
        }

        public async Task<IReadOnlyList<InlayHint>> GetHintsAsync(
            string filePath, string content, int startLine, int endLine)
        {
            var lspHints = await _lspManager.GetInlayHintsAsync(filePath, startLine, endLine);

            var result = new List<InlayHint>();
            foreach (var h in lspHints)
            {
                result.Add(new InlayHint
                {
                    Line = h.Line,
                    Column = h.Column,
                    Label = h.Label,
                    Kind = h.Kind switch
                    {
                        LspInlayHintKind.Type => InlayHintKind.Type,
                        LspInlayHintKind.Parameter => InlayHintKind.Parameter,
                        LspInlayHintKind.ReturnValue => InlayHintKind.ReturnValue,
                        _ => InlayHintKind.Type
                    }
                });
            }
            return result;
        }
    }
}
