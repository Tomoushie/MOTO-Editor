// Moto.Core/I18n/MarketplaceLanguageClient.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.I18n
{
    public sealed class MarketplaceLanguageInfo
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string NativeName { get; init; } = string.Empty;
        public string Flag { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public long DownloadCount { get; init; }
        public double Rating { get; init; }
        public DateTime LastUpdatedUtc { get; init; }
        public string DownloadUrl { get; init; } = string.Empty;
    }

    /// <summary>
    /// Client pour télécharger des packs de langues depuis le marketplace.
    /// </summary>
    public sealed class MarketplaceLanguageClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger<MarketplaceLanguageClient> _logger;
        private readonly string _baseUrl;

        public MarketplaceLanguageClient(
            ILogger<MarketplaceLanguageClient> logger,
            string baseUrl = "https://marketplace.moto-editor.dev/api/v1/languages")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseUrl = baseUrl;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>Récupère le catalogue des langues disponibles.</summary>
        public async Task<IReadOnlyList<MarketplaceLanguageInfo>> GetCatalogAsync(
            CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync(_baseUrl, ct);
                return JsonSerializer.Deserialize<List<MarketplaceLanguageInfo>>(json)
                    ?? new List<MarketplaceLanguageInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Marketplace] Erreur récupération catalogue langues");
                return new List<MarketplaceLanguageInfo>();
            }
        }

        /// <summary>Télécharge un pack de langue.</summary>
        public async Task<LanguagePack?> DownloadPackAsync(
            string code, CancellationToken ct = default)
        {
            try
            {
                var url = $"{_baseUrl}/{code}/download";
                var json = await _http.GetStringAsync(url, ct);
                return JsonSerializer.Deserialize<LanguagePack>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Marketplace] Erreur téléchargement pack {Code}", code);
                return null;
            }
        }

        /// <summary>Enregistre une traduction proposée par la communauté.</summary>
        public async Task<bool> SubmitTranslationAsync(
            LanguagePack pack, string authorEmail, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(new
                {
                    pack,
                    authorEmail,
                    submittedUtc = DateTime.UtcNow
                });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{_baseUrl}/submit", content, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Marketplace] Erreur soumission traduction");
                return false;
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
