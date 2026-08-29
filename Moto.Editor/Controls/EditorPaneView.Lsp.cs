// Moto.Editor/Controls/EditorPaneView.Lsp.cs
// Partial class : intègre les features LSP dans EditorPaneView.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.LSP;

namespace Moto.Editor.Controls
{
    public partial class EditorPaneView
    {
        private LanguageServerManager? _lspManager;
        private CancellationTokenSource? _lspCts;
        private readonly List<LspDiagnostic> _currentDiagnostics = new();
        private readonly List<LspInlayHint> _currentInlayHints = new();
        private readonly List<LspSemanticToken> _currentSemanticTokens = new();

        // ── Événements pour l'UI ──
        public event Action<IReadOnlyList<LspDiagnostic>>? DiagnosticsUpdated;
        public event Action<LspHoverInfo>? HoverRequested;
        public event Action<IReadOnlyList<LspCompletionItem>>? CompletionsReceived;
        public event Action<IReadOnlyList<LspCodeAction>>? CodeActionsReceived;

        /// <summary>
        /// Initialise l'intégration LSP.
        /// À appeler après InitializeComponent() ou via DI.
        /// </summary>
        public void InitializeLsp(LanguageServerManager manager)
        {
            _lspManager = manager;
            _lspManager.DiagnosticsPublished += OnLspDiagnosticsPublished;
        }

        // ── Gestion du document ──

        public async Task OpenDocumentWithLspAsync(string filePath, string content)
        {
            if (_lspManager == null) return;
            _lspCts?.Cancel();
            _lspCts = new CancellationTokenSource();

            try
            {
                await _lspManager.OpenDocumentAsync(filePath, content, _lspCts.Token);
                await RefreshSemanticTokensAsync(filePath);
            }
            catch (OperationCanceledException) { }
        }

        public async Task UpdateDocumentWithLspAsync(string filePath, string content)
        {
            if (_lspManager == null) return;
            _lspCts?.Cancel();
            _lspCts = new CancellationTokenSource();

            try
            {
                // Debounce : attendre 500ms avant de notifier le serveur
                await Task.Delay(500, _lspCts.Token);
                await _lspManager.UpdateDocumentAsync(filePath, content, _lspCts.Token);
                await RefreshInlayHintsAsync(filePath);
            }
            catch (OperationCanceledException) { }
        }

        public async Task CloseDocumentWithLspAsync(string filePath)
        {
            if (_lspManager == null) return;
            await _lspManager.CloseDocumentAsync(filePath);
            _currentDiagnostics.Clear();
            _currentInlayHints.Clear();
            _currentSemanticTokens.Clear();
        }

        // ── Features LSP ──

        public async Task RequestCompletionsAsync(string filePath, int line, int column)
        {
            if (_lspManager == null) return;

            var completions = await _lspManager.GetCompletionsAsync(filePath, line, column);
            CompletionsReceived?.Invoke(completions);
        }

        public async Task RequestHoverAsync(string filePath, int line, int column)
        {
            if (_lspManager == null) return;

            var hover = await _lspManager.GetHoverAsync(filePath, line, column);
            if (hover != null)
                HoverRequested?.Invoke(hover);
        }

        public async Task RequestDefinitionAsync(string filePath, int line, int column)
        {
            if (_lspManager == null) return;

            var locations = await _lspManager.GetDefinitionAsync(filePath, line, column);
            if (locations.Count > 0)
            {
                // Navigue vers la première définition
                var loc = locations[0];
                NavigateToLocation(loc);
            }
        }

        public async Task RequestReferencesAsync(string filePath, int line, int column)
        {
            if (_lspManager == null) return;

            var locations = await _lspManager.GetReferencesAsync(filePath, line, column);
            // Affiche les références dans un panneau (à intégrer avec l'UI existante)
            System.Diagnostics.Debug.WriteLine($"[LSP] {locations.Count} références trouvées.");
        }

        public async Task RequestCodeActionsAsync(string filePath, int startLine, int startCol, int endLine, int endCol)
        {
            if (_lspManager == null) return;

            var actions = await _lspManager.GetCodeActionsAsync(filePath, startLine, startCol, endLine, endCol);
            CodeActionsReceived?.Invoke(actions);
        }

        public async Task ApplyCodeActionAsync(string filePath, LspCodeAction action)
        {
            if (action.Edits == null) return;

            // Applique les edits dans l'ordre inverse pour préserver les positions
            var sortedEdits = action.Edits
                .OrderByDescending(e => e.StartLine)
                .ThenByDescending(e => e.StartColumn)
                .ToList();

            var currentText = EditorText ?? "";
            var lines = currentText.Split('\n').ToList();

            foreach (var edit in sortedEdits)
            {
                ApplyTextEdit(lines, edit);
            }

            EditorText = string.Join("\n", lines);
        }

        public async Task RenameSymbolAsync(string filePath, int line, int column, string newName)
        {
            if (_lspManager == null) return;

            var result = await _lspManager.RenameSymbolAsync(filePath, line, column, newName);
            if (result.Success && result.Changes != null)
            {
                foreach (var (file, edits) in result.Changes)
                {
                    // Applique les edits pour chaque fichier
                    System.Diagnostics.Debug.WriteLine($"[LSP] Rename dans {file} : {edits.Count} edits");
                }
            }
        }

        public async Task RequestSignatureHelpAsync(string filePath, int line, int column)
        {
            if (_lspManager == null) return;

            var sigHelp = await _lspManager.GetSignatureHelpAsync(filePath, line, column);
            if (sigHelp != null && sigHelp.Signatures.Count > 0)
            {
                // Affiche la signature dans un tooltip (à intégrer avec l'UI existante)
                System.Diagnostics.Debug.WriteLine($"[LSP] Signature : {sigHelp.Signatures[0].Label}");
            }
        }

        // ── Refresh des hints et tokens ──

        private async Task RefreshInlayHintsAsync(string filePath)
        {
            if (_lspManager == null) return;

            var visibleRange = GetVisibleLineRange();
            var hints = await _lspManager.GetInlayHintsAsync(filePath, visibleRange.Start, visibleRange.End);
            _currentInlayHints.Clear();
            _currentInlayHints.AddRange(hints);
            RenderInlayHints();
        }

        private async Task RefreshSemanticTokensAsync(string filePath)
        {
            if (_lspManager == null) return;

            var tokens = await _lspManager.GetSemanticTokensAsync(filePath);
            _currentSemanticTokens.Clear();
            _currentSemanticTokens.AddRange(tokens);
            ApplySemanticHighlighting();
        }

        // ── Handlers ──

        private void OnLspDiagnosticsPublished(string filePath, IReadOnlyList<LspDiagnostic> diagnostics)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentDiagnostics.Clear();
                _currentDiagnostics.AddRange(diagnostics);
                DiagnosticsUpdated?.Invoke(_currentDiagnostics);
                RenderDiagnosticsInGutter();
            });
        }

        // ── Rendu UI ──

        private void RenderDiagnosticsInGutter()
        {
            // Affiche les erreurs/warnings dans le gutter
            // (à intégrer avec le rendu existant du gutter)
            foreach (var diag in _currentDiagnostics)
            {
                var color = diag.Severity switch
                {
                    LspSeverity.Error => Colors.Red,
                    LspSeverity.Warning => Colors.Orange,
                    LspSeverity.Information => Colors.LightBlue,
                    _ => Colors.Gray
                };
                // TODO : intégrer avec le rendu du gutter existant
            }
        }

        private void RenderInlayHints()
        {
            // Utilise l'overlay existant InlayHintOverlay
            if (_inlayOverlay != null)
            {
                var mappedHints = _currentInlayHints.Select(h => new InlayHints.InlayHint
                {
                    Line = h.Line,
                    Column = h.Column,
                    Label = h.Label,
                    Kind = h.Kind switch
                    {
                        LspInlayHintKind.Type => InlayHints.InlayHintKind.Type,
                        LspInlayHintKind.Parameter => InlayHints.InlayHintKind.Parameter,
                        LspInlayHintKind.ReturnValue => InlayHints.InlayHintKind.ReturnValue,
                        _ => InlayHints.InlayHintKind.Type
                    }
                }).ToList();

                _inlayOverlay.RenderHints(mappedHints, 7.2, 18.0);
            }
        }

        private void ApplySemanticHighlighting()
        {
            // Applique les couleurs selon les semantic tokens
            // (à intégrer avec le colorateur syntaxique existant)
            foreach (var token in _currentSemanticTokens)
            {
                var color = GetSemanticTokenColor(token.Kind);
                // TODO : intégrer avec le colorateur existant
            }
        }

        private static Color GetSemanticTokenColor(LspSemanticTokenKind kind)
            => kind switch
            {
                LspSemanticTokenKind.Keyword => Color.FromArgb("#569CD6"),
                LspSemanticTokenKind.String => Color.FromArgb("#CE9178"),
                LspSemanticTokenKind.Number => Color.FromArgb("#B5CEA8"),
                LspSemanticTokenKind.Comment => Color.FromArgb("#6A9955"),
                LspSemanticTokenKind.Class => Color.FromArgb("#4EC9B0"),
                LspSemanticTokenKind.Method or LspSemanticTokenKind.Function => Color.FromArgb("#DCDCAA"),
                LspSemanticTokenKind.Property => Color.FromArgb("#9CDCFE"),
                LspSemanticTokenKind.Variable or LspSemanticTokenKind.Parameter => Color.FromArgb("#9CDCFE"),
                LspSemanticTokenKind.Type => Color.FromArgb("#4EC9B0"),
                _ => Color.FromArgb("#D4D4D4")
            };

        private void NavigateToLocation(LspLocation location)
        {
            // Navigue vers le fichier et la ligne
            // (à intégrer avec la navigation existante)
            System.Diagnostics.Debug.WriteLine($"[LSP] Navigation vers {location.FilePath}:{location.Line}");
        }

        private static void ApplyTextEdit(List<string> lines, LspTextEdit edit)
        {
            if (edit.StartLine >= lines.Count) return;

            var startLine = lines[edit.StartLine];
            var before = startLine.Substring(0, Math.Min(edit.StartColumn, startLine.Length));

            if (edit.StartLine == edit.EndLine)
            {
                var after = startLine.Substring(Math.Min(edit.EndColumn, startLine.Length));
                lines[edit.StartLine] = before + edit.NewText + after;
            }
            else
            {
                var endLine = lines[edit.EndLine];
                var after = endLine.Substring(Math.Min(edit.EndColumn, endLine.Length));
                lines[edit.StartLine] = before + edit.NewText + after;
                lines.RemoveRange(edit.StartLine + 1, edit.EndLine - edit.StartLine);
            }
        }

        private (int Start, int End) GetVisibleLineRange()
        {
            // Retourne la plage de lignes visibles (approximation)
            // À affiner avec le scroll réel
            return (0, 200);
        }
    }
}
