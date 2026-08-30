// Moto.Core/Plugins/Marketplace/MarketplaceServerClient.cs
// Client complet pour le serveur Marketplace Phase 3.
// Comptes, signatures, versioning, dépendances, catégories, recherche, analytics.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Plugins.Marketplace
{
    public sealed class MarketplaceServerAccount
    {
        public string Username { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiresUtc { get; init; }
        public bool IsPublisher { get; init; }
    }

    public sealed class MarketplaceSearchResult
    {
        public IReadOnlyList<PluginManifestPro> Plugins { get; init; } = Array.Empty<PluginManifestPro>();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    public sealed class MarketplaceInstallResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? InstalledPath { get; init; }
        public IReadOnlyList<string> InstalledDependencies { get; init; } = Array.Empty<string>();

        public static MarketplaceInstallResult Ok(string path, IReadOnlyList<string> deps)
            => new() { Success = true, Message = "Plugin installé.", InstalledPath = path, InstalledDependencies = deps };
        public static MarketplaceInstallResult Fail(string message)
            => new() { Success = false, Message = message };
    }

    /// <summary>
    /// Client Marketplace Phase 3 : comptes, signatures, dépendances, analytics.
    /// </summary>
    public sealed class MarketplaceServerClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly ILogger<MarketplaceServerClient> _logger;
        private MarketplaceAccount? _account;

        public MarketplaceServerClient(
            string baseUrl = "https://marketplace.moto-editor.dev/api/v3",
            ILogger<MarketplaceServerClient>? logger = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MarketplaceServerClient>.Instance;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public bool IsLoggedIn => _account != null && _account.ExpiresUtc > DateTime.UtcNow;

        // ── Comptes ──
        public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { username, password });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{_baseUrl}/auth/login", content, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _account = JsonSerializer.Deserialize<MarketplaceAccount>(body);
                if (_account == null) return false;

                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _account.Token);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Marketplace] Échec login.");
                return false;
            }
        }

        public async Task<bool> RegisterAsync(string username, string email, string password, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { username, email, password });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{_baseUrl}/auth/register", content, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public void Logout()
        {
            _account = null;
            _http.DefaultRequestHeaders.Authorization = null;
        }

        // ── Recherche et catalogue ──
        public async Task<MarketplaceSearchResult> SearchAsync(
            string? query = null,
            PluginKind? kind = null,
            PluginCategory? category = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            try
            {
                var url = $"{_baseUrl}/plugins?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrWhiteSpace(query)) url += $"&q={Uri.EscapeDataString(query)}";
                if (kind.HasValue) url += $"&kind={kind.Value.ToString().ToLower()}";
                if (category.HasValue) url += $"&category={category.Value.ToString().ToLower()}";

                var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<MarketplaceSearchResult>(json)
                    ?? new MarketplaceSearchResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Marketplace] Échec recherche.");
                return new MarketplaceSearchResult();
            }
        }

        public async Task<PluginManifestPro?> GetPluginAsync(string pluginId, CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync($"{_baseUrl}/plugins/{pluginId}", ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<PluginManifestPro>(json);
            }
            catch { return null; }
        }

        // ── Installation avec dépendances et signatures ──
        public async Task<MarketplaceInstallResult> InstallAsync(
            PluginManifestPro plugin,
            string pluginsDirectory,
            CancellationToken ct = default)
        {
            try
            {
                // 1. Résoudre les dépendances
                var deps = await ResolveDependenciesAsync(plugin, ct).ConfigureAwait(false);
                var installedDeps = new List<string>();

                // 2. Installer chaque dépendance
                foreach (var dep in deps)
                {
                    var depResult = await DownloadAndVerifyAsync(dep, pluginsDirectory, ct).ConfigureAwait(false);
                    if (!depResult.Success)
                        return MarketplaceInstallResult.Fail($"Dépendance {dep.Id} : {depResult.Message}");
                    installedDeps.Add(dep.Id);
                }

                // 3. Installer le plugin principal
                var result = await DownloadAndVerifyAsync(plugin, pluginsDirectory, ct).ConfigureAwait(false);
                if (!result.Success) return result;

                return MarketplaceInstallResult.Ok(result.InstalledPath!, installedDeps);
            }
            catch (Exception ex)
            {
                return MarketplaceInstallResult.Fail($"Erreur : {ex.Message}");
            }
        }

        private async Task<IReadOnlyList<PluginManifestPro>> ResolveDependenciesAsync(
            PluginManifestPro plugin, CancellationToken ct)
        {
            var resolved = new List<PluginManifestPro>();
            var visited = new HashSet<string> { plugin.Id };
            var queue = new Queue<PluginDependencyInfo>(plugin.Dependencies);

            while (queue.Count > 0)
            {
                var dep = queue.Dequeue();
                if (visited.Contains(dep.PluginId)) continue;
                visited.Add(dep.PluginId);

                var depPlugin = await GetPluginAsync(dep.PluginId, ct).ConfigureAwait(false);
                if (depPlugin != null)
                {
                    resolved.Add(depPlugin);
                    foreach (var sub in depPlugin.Dependencies)
                        queue.Enqueue(sub);
                }
            }
            return resolved;
        }

        private async Task<MarketplaceInstallResult> DownloadAndVerifyAsync(
            PluginManifestPro plugin, string pluginsDirectory, CancellationToken ct)
        {
            try
            {
                var url = $"{_baseUrl}/plugins/{plugin.Id}/download?version={plugin.Version}";
                var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);

                // Vérifier la signature
                if (plugin.Signature != null && !VerifySignature(bytes, plugin.Signature))
                    return MarketplaceInstallResult.Fail("Signature invalide.");

                // Écrire sur disque
                System.IO.Directory.CreateDirectory(pluginsDirectory);
                var path = System.IO.Path.Combine(pluginsDirectory, $"{plugin.Id}-{plugin.Version}.dll");
                await System.IO.File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);

                return MarketplaceInstallResult.Ok(path, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                return MarketplaceInstallResult.Fail($"Téléchargement : {ex.Message}");
            }
        }

        private static bool VerifySignature(byte[] data, PluginSignatureInfo signature)
        {
            try
            {
                // En production : Ed25519 via NSec.Cryptography ou BouncyCastle
                // Ici : vérification SHA256 simplifiée
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(data);
                var expectedHash = Convert.FromHexString(signature.SignatureHash);
                return hash.SequenceEqual(expectedHash);
            }
            catch { return false; }
        }

        // ── Mises à jour ──
        public async Task<IReadOnlyList<(PluginManifestPro Plugin, string LatestVersion)>> CheckUpdatesAsync(
            IReadOnlyDictionary<string, string> installedVersions, CancellationToken ct = default)
        {
            var updates = new List<(PluginManifestPro, string)>();
            foreach (var (id, currentVersion) in installedVersions)
            {
                var plugin = await GetPluginAsync(id, ct).ConfigureAwait(false);
                if (plugin != null && string.Compare(plugin.Version, currentVersion, StringComparison.Ordinal) > 0)
                    updates.Add((plugin, plugin.Version));
            }
            return updates;
        }

        // ── Analytics ──
        public async Task RecordInstallAsync(string pluginId, CancellationToken ct = default)
        {
            try
            {
                await _http.PostAsync($"{_baseUrl}/plugins/{pluginId}/analytics/install", null, ct).ConfigureAwait(false);
            }
            catch { }
        }

        public void Dispose() => _http.Dispose();
    }
}
