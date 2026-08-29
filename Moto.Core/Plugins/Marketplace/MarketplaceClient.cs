// Moto.Core/Plugins/Marketplace/MarketplaceClient.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Plugins.Marketplace
{
    public sealed class MarketplaceEntry
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
    }

    public sealed class InstallResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? InstalledPath { get; init; }

        public static InstallResult Ok(string path)
            => new() { Success = true, Message = "Plugin installé.", InstalledPath = path };

        public static InstallResult Fail(string message)
            => new() { Success = false, Message = message };
    }

    public sealed class MarketplaceClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MarketplaceClient(string baseUrl = "https://marketplace.moto-editor.dev/api/v1")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<IReadOnlyList<MarketplaceEntry>> GetCatalogAsync(
            string? search = null,
            CancellationToken ct = default)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(search)
                    ? $"{_baseUrl}/plugins"
                    : $"{_baseUrl}/plugins?search={Uri.EscapeDataString(search)}";

                var json = await _http.GetStringAsync(url, ct);
                var list = JsonSerializer.Deserialize<List<MarketplaceEntry>>(json, JsonOpts);

                return list ?? new List<MarketplaceEntry>();
            }
            catch
            {
                // Hors ligne ou serveur indisponible : la galerie doit rester utilisable.
                return new List<MarketplaceEntry>();
            }
        }

        public async Task<InstallResult> InstallAsync(
            MarketplaceEntry entry,
            string pluginsDirectory,
            CancellationToken ct = default)
        {
            if (entry is null)
                return InstallResult.Fail("Entrée invalide.");

            if (string.IsNullOrWhiteSpace(entry.DownloadUrl))
                return InstallResult.Fail("URL de téléchargement manquante.");

            if (string.IsNullOrWhiteSpace(entry.Sha256))
                return InstallResult.Fail("Checksum manquant : installation refusée.");

            try
            {
                Directory.CreateDirectory(pluginsDirectory);

                var bytes = await _http.GetByteArrayAsync(entry.DownloadUrl, ct);

                var hash = ComputeSha256(bytes);
                if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return InstallResult.Fail(
                        $"Checksum invalide : attendu {entry.Sha256}, reçu {hash}.");
                }

                var fileName = $"{entry.Id}-{entry.Version}.dll";
                var targetPath = Path.Combine(pluginsDirectory, fileName);

                await File.WriteAllBytesAsync(targetPath, bytes, ct);

                return InstallResult.Ok(targetPath);
            }
            catch (OperationCanceledException)
            {
                return InstallResult.Fail("Installation annulée.");
            }
            catch (Exception ex)
            {
                return InstallResult.Fail($"Erreur : {ex.Message}");
            }
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public void Dispose() => _http.Dispose();
    }
}
