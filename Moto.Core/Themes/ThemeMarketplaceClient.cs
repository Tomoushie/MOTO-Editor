// Moto.Core/Themes/ThemeMarketplaceClient.cs
// Client REST pour le marketplace de thèmes.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Themes
{
    public sealed class ThemeMarketplaceEntry
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string PreviewImageUrl { get; init; } = string.Empty;
        public long DownloadCount { get; init; }
        public double Rating { get; init; }
        public string DownloadUrl { get; init; } = string.Empty;
        public string Signature { get; init; } = string.Empty;
        public List<string> Tags { get; init; } = new();
    }

    /// <summary>
    /// Client REST pour le marketplace de thèmes en ligne.
    /// </summary>
    public sealed class ThemeMarketplaceClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger<ThemeMarketplaceClient> _logger;
        private readonly string _baseUrl;

        public ThemeMarketplaceClient(
            ILogger<ThemeMarketplaceClient> logger,
            string baseUrl = "https://marketplace.moto-editor.dev/api/v1/themes")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseUrl = baseUrl;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Récupère le catalogue des thèmes disponibles.
        /// </summary>
        public async Task<IReadOnlyList<ThemeMarketplaceEntry>> GetCatalogAsync(
            string? search = null,
            string? tag = null,
            CancellationToken ct = default)
        {
            try
            {
                var url = _baseUrl;
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(search))
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                if (!string.IsNullOrWhiteSpace(tag))
                    queryParams.Add($"tag={Uri.EscapeDataString(tag)}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<ThemeMarketplaceEntry>>(json)
                    ?? new List<ThemeMarketplaceEntry>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ThemeMarketplace] Erreur catalogue");
                return new List<ThemeMarketplaceEntry>();
            }
        }

        /// <summary>
        /// Télécharge un thème depuis le marketplace.
        /// </summary>
        public async Task<ThemeDefinition?> DownloadThemeAsync(
            ThemeMarketplaceEntry entry,
            CancellationToken ct = default)
        {
            try
            {
                var json = await _http.GetStringAsync(entry.DownloadUrl, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<ThemeDefinition>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ThemeMarketplace] Erreur téléchargement");
                return null;
            }
        }

        /// <summary>
        /// Soumet un thème créé par l'utilisateur au marketplace.
        /// </summary>
        public async Task<bool> SubmitThemeAsync(
            ThemeDefinition theme,
            string authorEmail,
            string signature,
            CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    theme = JsonSerializer.Serialize(theme),
                    authorEmail,
                    signature,
                    submittedUtc = DateTime.UtcNow
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8, "application/json");

                var response = await _http.PostAsync($"{_baseUrl}/submit", content, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ThemeMarketplace] Erreur soumission");
                return false;
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
