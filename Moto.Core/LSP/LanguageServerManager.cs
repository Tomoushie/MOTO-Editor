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
        private readonly ILogger<LanguageServerManager> _logger;

        public event Action<string, IReadOnlyList<LspDiagnostic>>? DiagnosticsPublished;

        public LanguageServerManager(ILogger<LanguageServerManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionManager = new LspSessionManager(logger);
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
            return client?.GetCompletionsAsync(filePath, line, column, ct).Result ?? Array.Empty<LspCompletionItem>();
        }

        public async Task<LspHoverInfo?> GetHoverAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetHoverAsync(filePath, line, column, ct).Result;
        }

        public async Task<IReadOnlyList<LspLocation>> GetDefinitionAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetDefinitionAsync(filePath, line, column, ct).Result ?? Array.Empty<LspLocation>();
        }

        public async Task<IReadOnlyList<LspLocation>> GetReferencesAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetReferencesAsync(filePath, line, column, true, ct).Result ?? Array.Empty<LspLocation>();
        }

        public async Task<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
            string filePath, int startLine, int startCol, int endLine, int endCol, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetCodeActionsAsync(filePath, startLine, startCol, endLine, endCol, ct).Result ?? Array.Empty<LspCodeAction>();
        }

        public async Task<LspRenameResult> RenameSymbolAsync(
            string filePath, int line, int column, string newName, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            if (client == null)
                return new LspRenameResult { Success = false, Message = "Client non disponible." };
            return await client.RenameSymbolAsync(filePath, line, column, newName, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LspInlayHint>> GetInlayHintsAsync(
            string filePath, int startLine, int endLine, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetInlayHintsAsync(filePath, startLine, endLine, ct).Result ?? Array.Empty<LspInlayHint>();
        }

        public async Task<IReadOnlyList<LspSemanticToken>> GetSemanticTokensAsync(
            string filePath, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetSemanticTokensAsync(filePath, ct).Result ?? Array.Empty<LspSemanticToken>();
        }

        public async Task<LspSignatureHelp?> GetSignatureHelpAsync(
            string filePath, int line, int column, CancellationToken ct = default)
        {
            var client = await _sessionManager.GetClientForFileAsync(filePath, ct).ConfigureAwait(false);
            return client?.GetSignatureHelpAsync(filePath, line, column, ct).Result;
        }

        public IReadOnlyList<LspDiagnostic> GetDiagnostics(string filePath)
        {
            var workspace = GetWorkspaceForFile(filePath);
            var session = _sessionManager.GetOrCreateSessionAsync(workspace).Result;
            return session?.GetCachedDiagnostics(filePath) ?? Array.Empty<LspDiagnostic>();
        }

        private static string GetWorkspaceForFile(string filePath)
        {
            var dir = System.IO.Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (System.IO.Directory.GetFiles(dir, "*.sln").Length > 0 ||
                    System.IO.Directory.GetFiles(dir, "*.csproj").Length > 0)
                    return dir;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            return System.IO.Path.GetDirectoryName(filePath) ?? "";
        }

        public async ValueTask DisposeAsync()
        {
            await _sessionManager.DisposeAsync().ConfigureAwait(false);
        }
    }
}
