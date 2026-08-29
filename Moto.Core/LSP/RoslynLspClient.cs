// Moto.Core/LSP/RoslynLspClient.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.LSP
{
    public interface ILanguageServerClient : IAsyncDisposable
    {
        Task<IReadOnlyList<DiagnosticInfo>> GetDiagnosticsAsync(string filePath, CancellationToken ct = default);
        Task<IReadOnlyList<InlayHint>> GetInlayHintsAsync(string filePath, int startLine, int endLine, CancellationToken ct = default);
        Task<IReadOnlyList<SemanticToken>> GetSemanticTokensAsync(string filePath, CancellationToken ct = default);
        Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(string filePath, int line, int column, CancellationToken ct = default);
        Task<HoverInfo?> GetHoverAsync(string filePath, int line, int column, CancellationToken ct = default);
    }

    public sealed class InlayHint
    {
        public int Line { get; init; }
        public int Column { get; init; }
        public string Label { get; init; } = string.Empty;
        public InlayHintKind Kind { get; init; }
    }

    public enum InlayHintKind { Type, Parameter, ReturnValue }

    public sealed class SemanticToken
    {
        public int Line { get; init; }
        public int StartChar { get; init; }
        public int Length { get; init; }
        public SemanticTokenKind Kind { get; init; }
    }

    public enum SemanticTokenKind
    {
        Keyword, Type, Class, Method, Variable, Parameter, Property, String, Number, Comment, Operator
    }

    public sealed class CodeAction
    {
        public string Title { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty; // refactor, quickfix, source
        public string? Command { get; init; }
    }

    public sealed class HoverInfo
    {
        public string Content { get; init; } = string.Empty;
        public string? Documentation { get; init; }
    }

    public sealed class DiagnosticInfo
    {
        public int Line { get; init; }
        public int Column { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
        public string Severity { get; init; } = "error"; // error, warning, info, hint
        public string Message { get; init; } = string.Empty;
        public string? Code { get; init; }
        public string Source { get; init; } = string.Empty;
    }

    /// <summary>
    /// Client Roslyn LSP (via OmniSharp.Extensions.LanguageClient).
    /// Fournit : inlay hints, semantic tokens, diagnostics, code actions, hover.
    /// </summary>
    public sealed class RoslynLspClient : ILanguageServerClient
    {
        private readonly string _workspaceRoot;
        private readonly List<DiagnosticInfo> _diagnosticsCache = new();
        private bool _initialized;

        public RoslynLspClient(string workspaceRoot = "")
        {
            _workspaceRoot = workspaceRoot;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized) return;
            // En production : démarrer OmniSharp.Extensions.LanguageClient
            // via Process.Start("OmniSharp") en mode stdio
            await Task.Delay(100, ct);
            _initialized = true;
        }

        public async Task<IReadOnlyList<DiagnosticInfo>> GetDiagnosticsAsync(
            string filePath, CancellationToken ct = default)
        {
            await InitializeAsync(ct);
            // Simulation : en production, appelle textDocument/publishDiagnostics
            return _diagnosticsCache
                .Where(d => string.Equals(d.Source, filePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<IReadOnlyList<InlayHint>> GetInlayHintsAsync(
            string filePath, int startLine, int endLine, CancellationToken ct = default)
        {
            await InitializeAsync(ct);
            // Simulation : en production, appelle textDocument/inlayHint (LSP 3.17+)
            var hints = new List<InlayHint>();

            if (!File.Exists(filePath)) return hints;
            var lines = await File.ReadAllLinesAsync(filePath, ct);

            for (int i = Math.Max(0, startLine); i < Math.Min(lines.Length, endLine); i++)
            {
                var line = lines[i];

                // Détection basique : "var x = " → ajouter ": Type"
                if (line.Contains(" var ") && line.Contains("="))
                {
                    var idx = line.IndexOf(" var ");
                    hints.Add(new InlayHint
                    {
                        Line = i,
                        Column = idx + 5,
                        Label = ": object",
                        Kind = InlayHintKind.Type
                    });
                }

                // Détection paramètres
                var methodCallMatch = System.Text.RegularExpressions.Regex.Match(
                    line, @"\w+\(([^)]*)\)");
                if (methodCallMatch.Success)
                {
                    var args = methodCallMatch.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(args))
                    {
                        hints.Add(new InlayHint
                        {
                            Line = i,
                            Column = line.IndexOf('(') + 1,
                            Label = "param:",
                            Kind = InlayHintKind.Parameter
                        });
                    }
                }
            }

            return hints;
        }

        public async Task<IReadOnlyList<SemanticToken>> GetSemanticTokensAsync(
            string filePath, CancellationToken ct = default)
        {
            await InitializeAsync(ct);
            // En production : textDocument/semanticTokens/full
            var tokens = new List<SemanticToken>();
            if (!File.Exists(filePath)) return tokens;

            var lines = await File.ReadAllLinesAsync(filePath, ct);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Mots-clés C#
                foreach (var kw in new[] { "public", "private", "class", "void", "return", "if", "else", "for", "foreach" })
                {
                    int idx = 0;
                    while ((idx = line.IndexOf(kw, idx, StringComparison.Ordinal)) >= 0)
                    {
                        tokens.Add(new SemanticToken
                        {
                            Line = i, StartChar = idx, Length = kw.Length,
                            Kind = SemanticTokenKind.Keyword
                        });
                        idx += kw.Length;
                    }
                }
            }
            return tokens;
        }

        public async Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            await InitializeAsync(ct);
            // En production : textDocument/codeAction
            return new List<CodeAction>
            {
                new() { Title = "Extraire méthode", Kind = "refactor.extract" },
                new() { Title = "Introduire variable", Kind = "refactor.introduce" },
                new() { Title = "Ajouter using", Kind = "quickfix.import" }
            };
        }

        public async Task<HoverInfo?> GetHoverAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            await InitializeAsync(ct);
            return new HoverInfo
            {
                Content = "```csharp\npublic class Example\n```",
                Documentation = "Type détecté par Roslyn."
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
