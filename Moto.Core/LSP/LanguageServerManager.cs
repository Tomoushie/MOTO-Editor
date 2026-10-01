// Moto.Core/LSP/LanguageServerManager.cs (v31 — intègre LspSessionManager)
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.LSP
{
    /// <summary>
    /// Manager centralisé pour LSP.
    /// Délègue à LspSessionManager pour la gestion des sessions.
    /// </summary>
    public sealed class LanguageServerManager : IAsyncDisposable
    {
        private readonly LspSessionManager _sessionManager;

        public event Action<string, IReadOnlyList<LspDiagnostic>>? DiagnosticsPublished;

        public LanguageServerManager(ILogger logger)
        {
            _sessionManager = new LspSessionManager(logger ?? throw new ArgumentNullException(nameof(logger)));
            _sessionManager.DiagnosticsPublished += (path, diags) =>
                DiagnosticsPublished?.Invoke(path, diags);
        }

        public async Task OpenDocumentAsync(string filePath, string content, CancellationToken ct = default)
            => await _sessionManager.OpenDocumentAsync(filePath, content, ct).ConfigureAwait(false);

        public async Task UpdateDocumentAsync(string filePath, string content, CancellationToken ct = default)
            => await _sessionManager.UpdateDocumentAsync(filePath, content, ct).ConfigureAwait(false);

        public async Task CloseDocumentAsync(string filePath, CancellationToken ct = default)
            => await _sessionManager.CloseDocumentAsync(filePath, ct).ConfigureAwait(false);

        public async Task<IReadOnlyList<LspCompletionItem>> GetCompletionsAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? Array.Empty<LspCompletionItem>()
                : await client.GetCompletionsAsync(filePath, line, column, ct).ConfigureAwait(false);
        }

        public async Task<LspHoverInfo?> GetHoverAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? null
                : await client.GetHoverAsync(filePath, line, column, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LspLocation>> GetDefinitionAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? Array.Empty<LspLocation>()
                : await client.GetDefinitionAsync(filePath, line, column, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LspLocation>> GetReferencesAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? Array.Empty<LspLocation>()
                : await client.GetReferencesAsync(filePath, line, column, true, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
            string filePath, int startLine, int startCol, int endLine, int endCol, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? Array.Empty<LspCodeAction>()
                : await client.GetCodeActionsAsync(filePath, startLine, startCol, endLine, endCol, ct).ConfigureAwait(false);
        }

        public async Task<LspRenameResult> RenameSymbolAsync(
            string filePath, int line, int column, string newName, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            if (client is null)
                return new LspRenameResult { Success = false, Message = "Client non disponible." };
            return await client.RenameSymbolAsync(filePath, line, column, newName, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LspInlayHint>> GetInlayHintsAsync(
            string filePath, int startLine, int endLine, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? Array.Empty<LspInlayHint>()
                : await client.GetInlayHintsAsync(filePath, startLine, endLine, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LspSemanticToken>> GetSemanticTokensAsync(
            string filePath, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? Array.Empty<LspSemanticToken>()
                : await client.GetSemanticTokensAsync(filePath, ct).ConfigureAwait(false);
        }

        public async Task<LspSignatureHelp?> GetSignatureHelpAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client is null
                ? null
                : await client.GetSignatureHelpAsync(filePath, line, column, ct).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _sessionManager.DisposeAsync().ConfigureAwait(false);
        }
    }
}
