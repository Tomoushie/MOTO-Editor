// Moto.Core/LSP/LspSessionManager.cs
// Gère le cycle de vie des sessions LSP par workspace.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.LSP
{
    /// <summary>
    /// Gère les sessions LSP par workspace.
    /// Un seul client par workspace, partage des diagnostics.
    /// </summary>
    public sealed class LspSessionManager : IAsyncDisposable
    {
        private readonly ILogger<LspSessionManager> _logger;
        private readonly Dictionary<string, RoslynLanguageServerClient> _sessions = new();
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<string, int> _documentVersions = new();

        public event Action<string, IReadOnlyList<LspDiagnostic>>? DiagnosticsPublished;

        public LspSessionManager(ILogger<LspSessionManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Obtient ou crée une session pour un workspace.
        /// </summary>
        public async Task<RoslynLanguageServerClient?> GetOrCreateSessionAsync(
            string workspaceRoot, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_sessions.TryGetValue(workspaceRoot, out var existing))
                    return existing;

                var serverPath = FindOmniSharpServer();
                if (serverPath == null)
                {
                    _logger.LogWarning("[LSP] Serveur OmniSharp non trouvé.");
                    return null;
                }

                var client = new RoslynLanguageServerClient(serverPath, workspaceRoot,
                    _logger);

                client.DiagnosticsPublished += (path, diags) =>
                    DiagnosticsPublished?.Invoke(path, diags);

                await client.InitializeAsync(ct).ConfigureAwait(false);
                _sessions[workspaceRoot] = client;

                return client;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Notifie l'ouverture d'un document.
        /// </summary>
        public async Task OpenDocumentAsync(string filePath, string content, CancellationToken ct = default)
        {
            var workspace = GetWorkspaceForFile(filePath);
            var session = await GetOrCreateSessionAsync(workspace, ct).ConfigureAwait(false);
            if (session == null) return;

            _documentVersions[filePath] = 1;
            await session.OpenDocumentAsync(filePath, content, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Notifie la modification d'un document.
        /// </summary>
        public async Task UpdateDocumentAsync(string filePath, string content, CancellationToken ct = default)
        {
            var workspace = GetWorkspaceForFile(filePath);
            var session = await GetOrCreateSessionAsync(workspace, ct).ConfigureAwait(false);
            if (session == null) return;

            if (!_documentVersions.ContainsKey(filePath))
                _documentVersions[filePath] = 0;

            _documentVersions[filePath]++;
            await session.UpdateDocumentAsync(filePath, content, _documentVersions[filePath], ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Notifie la fermeture d'un document.
        /// </summary>
        public async Task CloseDocumentAsync(string filePath, CancellationToken ct = default)
        {
            var workspace = GetWorkspaceForFile(filePath);
            var session = await GetOrCreateSessionAsync(workspace, ct).ConfigureAwait(false);
            if (session == null) return;

            _documentVersions.Remove(filePath);
            await session.CloseDocumentAsync(filePath, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Obtient le client pour un fichier donné.
        /// </summary>
        public async Task<RoslynLanguageServerClient?> GetClientForFileAsync(
            string filePath, CancellationToken ct = default)
        {
            var workspace = GetWorkspaceForFile(filePath);
            return await GetOrCreateSessionAsync(workspace, ct).ConfigureAwait(false);
        }

        private static string GetWorkspaceForFile(string filePath)
        {
            // Remonte jusqu'à trouver un .sln, .csproj ou le dossier racine
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                    Directory.GetFiles(dir, "*.csproj").Length > 0)
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.GetDirectoryName(filePath) ?? "";
        }

        private static string? FindOmniSharpServer()
        {
            // Recherche dans les emplacements standards
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".omnisharp", "OmniSharp.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "omnisharp", "OmniSharp.exe"),
                "OmniSharp.exe" // dans le PATH
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            // Vérifie si OmniSharp est dans le PATH
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("OmniSharp.exe", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(2000);
                if (proc?.ExitCode == 0) return "OmniSharp.exe";
            }
            catch { }

            return null;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var session in _sessions.Values)
                await session.DisposeAsync().ConfigureAwait(false);
            _sessions.Clear();
            _gate.Dispose();
        }
    }
}
