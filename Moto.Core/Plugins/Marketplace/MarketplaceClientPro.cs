// Moto.Core/Plugins/Marketplace/MarketplaceClientPro.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Plugins.Marketplace
{
    public sealed class MarketplaceAccount
    {
        public string Username { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiresUtc { get; init; }
    }

    public sealed class PluginDependency
    {
        public string PluginId { get; init; } = string.Empty;
        public string VersionRange { get; init; } = string.Empty;
    }

    public sealed class MarketplaceEntryPro
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string DownloadUrl { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
        public long DownloadCount { get; init; }
        public double Rating { get; init; }
        public string? Signature { get; init; }
        public IReadOnlyList<PluginDependency> Dependencies { get; init; } = Array.Empty<PluginDependency>();
        public DateTime PublishedUtc { get; init; }
        public bool IsVerified { get; init; }
    }

    /// <summary>
    /// Marketplace Phase 3 : comptes, signatures, dépendances, mises à jour auto.
    /// </summary>
    public sealed class MarketplaceClientPro : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private MarketplaceAccount? _account;

        public MarketplaceClientPro(string baseUrl = "https://marketplace.moto-editor.dev/api/v2")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { username, password });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{_baseUrl}/auth/login", content, ct);
                if (!response.IsSuccessStatusCode) return false;

                var body = await response.Content.ReadAsStringAsync(ct);
                _account = JsonSerializer.Deserialize<MarketplaceAccount>(body);
                if (_account == null) return false;

                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _account.Token);
                return true;
            }
            catch { return false; }
        }

        public bool IsLoggedIn => _account != null && _account.ExpiresUtc > DateTime.UtcNow;

        public async Task<IReadOnlyList<MarketplaceEntryPro>> GetCatalogProAsync(
            string? search = null, CancellationToken ct = default)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(search)
                    ? $"{_baseUrl}/plugins"
                    : $"{_baseUrl}/plugins?search={Uri.EscapeDataString(search)}";
                var json = await _http.GetStringAsync(url, ct);
                return JsonSerializer.Deserialize<List<MarketplaceEntryPro>>(json)
                    ?? new List<MarketplaceEntryPro>();
            }
            catch { return new List<MarketplaceEntryPro>(); }
        }

        /// <summary>Vérifie une signature (placeholder Ed25519 ; utiliser NSec/BouncyCastle en prod).</summary>
        public bool VerifySignature(byte[] pluginBytes, string signatureHex, string publicKeyHex)
        {
            try
            {
                var signature = Convert.FromHexString(signatureHex);
                var publicKey = Convert.FromHexString(publicKeyHex);
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(pluginBytes);
                return signature.Length == 64 && publicKey.Length == 32 && hash.Length == 32;
            }
            catch { return false; }
        }

        /// <summary>Résout les dépendances transitives d'un plugin.</summary>
        public async Task<IReadOnlyList<MarketplaceEntryPro>> ResolveDependenciesAsync(
            MarketplaceEntryPro plugin, CancellationToken ct = default)
        {
            var resolved = new List<MarketplaceEntryPro>();
            var visited = new HashSet<string> { plugin.Id };
            var queue = new Queue<PluginDependency>(plugin.Dependencies);

            while (queue.Count > 0)
            {
                var dep = queue.Dequeue();
                if (visited.Contains(dep.PluginId)) continue;
                visited.Add(dep.PluginId);

                var catalog = await GetCatalogProAsync(dep.PluginId, ct);
                foreach (var p in catalog)
                {
                    resolved.Add(p);
                    foreach (var sub in p.Dependencies) queue.Enqueue(sub);
                }
            }
            return resolved;
        }

        /// <summary>Détecte les mises à jour disponibles.</summary>
        public async Task<IReadOnlyList<(string Id, string Current, string Latest)>> CheckUpdatesAsync(
            IReadOnlyDictionary<string, string> installed, CancellationToken ct = default)
        {
            var updates = new List<(string, string, string)>();
            var catalog = await GetCatalogProAsync(ct: ct);

            foreach (var (id, current) in installed)
            {
                var latest = catalog.FirstOrDefault(p => p.Id == id);
                if (latest != null &&
                    string.Compare(latest.Version, current, StringComparison.Ordinal) > 0)
                    updates.Add((id, current, latest.Version));
            }
            return updates;
        }

        public void Dispose() => _http.Dispose();
    }
}
