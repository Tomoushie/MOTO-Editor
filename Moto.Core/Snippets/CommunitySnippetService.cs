// Moto.Core/Snippets/CommunitySnippetService.cs
// Service de partage et rating des snippets communautaires.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Snippets
{
    public sealed class CommunitySnippet
    {
        public string Id { get; init; } = string.Empty;
        public string Trigger { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public long DownloadCount { get; set; }
        public double AverageRating { get; set; }
        public int RatingCount { get; set; }
        public List<string> Tags { get; init; } = new();
    }

    /// <summary>
    /// Service de partage et rating des snippets communautaires.
    /// </summary>
    public sealed class CommunitySnippetService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger<CommunitySnippetService> _logger;
        private readonly string _baseUrl;

        public CommunitySnippetService(
            ILogger<CommunitySnippetService> logger,
            string baseUrl = "https://marketplace.moto-editor.dev/api/v1/snippets")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseUrl = baseUrl;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Récupère les snippets communautaires.
        /// </summary>
        public async Task<IReadOnlyList<CommunitySnippet>> GetCommunitySnippetsAsync(
            string? language = null,
            string? search = null,
            CancellationToken ct = default)
        {
            try
            {
                var url = _baseUrl;
                var queryParams = new List<string>();

                if (!string.IsNullOrWhiteSpace(language))
                    queryParams.Add($"language={Uri.EscapeDataString(language)}");
                if (!string.IsNullOrWhiteSpace(search))
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<CommunitySnippet>>(json)
                    ?? new List<CommunitySnippet>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CommunitySnippets] Erreur récupération");
                return new List<CommunitySnippet>();
            }
        }

        /// <summary>
        /// Partage un snippet avec la communauté.
        /// </summary>
        public async Task<bool> ShareSnippetAsync(
            Snippet snippet,
            string authorName,
            string authorEmail,
            CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    trigger = snippet.Trigger,
                    description = snippet.Description,
                    body = snippet.Body,
                    language = snippet.Language,
                    author = authorName,
                    authorEmail,
                    tags = snippet.Tags,
                    submittedUtc = DateTime.UtcNow
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8, "application/json");

                var response = await _http.PostAsync($"{_baseUrl}/share", content, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CommunitySnippets] Erreur partage");
                return false;
            }
        }

        /// <summary>
        /// Note un snippet (1-5 étoiles).
        /// </summary>
        public async Task<bool> RateSnippetAsync(
            string snippetId,
            int rating,
            string userId,
            CancellationToken ct = default)
        {
            try
            {
                var payload = new { snippetId, rating, userId };
                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8, "application/json");

                var response = await _http.PostAsync($"{_baseUrl}/rate", content, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CommunitySnippets] Erreur rating");
                return false;
            }
        }

        /// <summary>
        /// Télécharge un snippet communautaire.
        /// </summary>
        public async Task<Snippet?> DownloadSnippetAsync(
            CommunitySnippet communitySnippet,
            CancellationToken ct = default)
        {
            try
            {
                return new Snippet
                {
                    Id = communitySnippet.Id,
                    Trigger = communitySnippet.Trigger,
                    Description = communitySnippet.Description,
                    Body = communitySnippet.Body,
                    Language = communitySnippet.Language,
                    Author = communitySnippet.Author,
                    Tags = communitySnippet.Tags
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CommunitySnippets] Erreur téléchargement");
                return null;
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
