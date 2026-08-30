// Moto.Core/Cloud/CloudProviderClient.cs
// Clients OAuth2 pour Dropbox, Google Drive, OneDrive.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Cloud
{
    public enum CloudProvider { None, Dropbox, GoogleDrive, OneDrive }

    public sealed class CloudAuthResult
    {
        public bool Success { get; init; }
        public string? AccessToken { get; init; }
        public string? RefreshToken { get; init; }
        public DateTime? ExpiresUtc { get; init; }
        public string? Error { get; init; }
    }

    public sealed class CloudFileInfo
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public DateTime ModifiedUtc { get; init; }
        public bool IsFolder { get; init; }
    }

    /// <summary>
    /// Client unifié pour les fournisseurs cloud.
    /// Implémente OAuth2 + upload/download/listing.
    /// </summary>
    public sealed class CloudProviderClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger _logger;
        private readonly CloudProvider _provider;
        private string? _accessToken;
        private string? _refreshToken;

        public CloudProviderClient(CloudProvider provider, ILogger logger)
        {
            _provider = provider;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        /// <summary>
        /// Initialise l'authentification OAuth2.
        /// Retourne l'URL d'autorisation à ouvrir dans le navigateur.
        /// </summary>
        public string GetAuthorizationUrl(string clientId, string redirectUri)
        {
            return _provider switch
            {
                CloudProvider.Dropbox =>
                    $"https://www.dropbox.com/oauth2/authorize?client_id={clientId}" +
                    $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}",

                CloudProvider.GoogleDrive =>
                    $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}" +
                    $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                    "&scope=https://www.googleapis.com/auth/drive.file",

                CloudProvider.OneDrive =>
                    $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={clientId}" +
                    $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                    "&scope=Files.ReadWrite",

                _ => throw new NotSupportedException($"Provider non supporté : {_provider}")
            };
        }

        /// <summary>
        /// Échange le code d'autorisation contre des tokens.
        /// </summary>
        public async Task<CloudAuthResult> ExchangeCodeAsync(
            string clientId, string clientSecret, string code, string redirectUri,
            CancellationToken ct = default)
        {
            try
            {
                var (tokenUrl, parameters) = GetTokenEndpoint(clientId, clientSecret, code, redirectUri);

                var content = new FormUrlEncodedContent(parameters);
                var response = await _http.PostAsync(tokenUrl, content, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return new CloudAuthResult { Success = false, Error = error };
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (tokenResponse == null)
                    return new CloudAuthResult { Success = false, Error = "Réponse invalide" };

                _accessToken = tokenResponse["access_token"].GetString();
                _refreshToken = tokenResponse.TryGetValue("refresh_token", out var rt)
                    ? rt.GetString() : null;

                var expiresIn = tokenResponse.TryGetValue("expires_in", out var ei)
                    ? ei.GetInt32() : 3600;

                return new CloudAuthResult
                {
                    Success = true,
                    AccessToken = _accessToken,
                    RefreshToken = _refreshToken,
                    ExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cloud] Erreur échange code");
                return new CloudAuthResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Upload un fichier vers le cloud.
        /// </summary>
        public async Task<bool> UploadAsync(string localPath, string remotePath, CancellationToken ct = default)
        {
            if (!IsAuthenticated) return false;

            try
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(localPath, ct).ConfigureAwait(false);
                var fileName = System.IO.Path.GetFileName(localPath);

                var request = _provider switch
                {
                    CloudProvider.Dropbox => CreateDropboxUploadRequest(remotePath, fileBytes),
                    CloudProvider.GoogleDrive => CreateGoogleDriveUploadRequest(fileName, fileBytes),
                    CloudProvider.OneDrive => CreateOneDriveUploadRequest(remotePath, fileBytes),
                    _ => throw new NotSupportedException()
                };

                var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cloud] Erreur upload");
                return false;
            }
        }

        /// <summary>
        /// Liste les fichiers d'un dossier distant.
        /// </summary>
        public async Task<IReadOnlyList<CloudFileInfo>> ListAsync(string remotePath, CancellationToken ct = default)
        {
            if (!IsAuthenticated) return Array.Empty<CloudFileInfo>();

            try
            {
                var request = _provider switch
                {
                    CloudProvider.Dropbox => CreateDropboxListRequest(remotePath),
                    CloudProvider.GoogleDrive => CreateGoogleDriveListRequest(),
                    CloudProvider.OneDrive => CreateOneDriveListRequest(remotePath),
                    _ => throw new NotSupportedException()
                };

                var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return Array.Empty<CloudFileInfo>();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return ParseFileList(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Cloud] Erreur listing");
                return Array.Empty<CloudFileInfo>();
            }
        }

        public void SetTokens(string accessToken, string? refreshToken)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
        }

        // ── Helpers spécifiques par provider ──

        private (string Url, Dictionary<string, string> Params) GetTokenEndpoint(
            string clientId, string clientSecret, string code, string redirectUri)
        {
            return _provider switch
            {
                CloudProvider.Dropbox => (
                    "https://api.dropboxapi.com/oauth2/token",
                    new Dictionary<string, string>
                    {
                        ["code"] = code,
                        ["grant_type"] = "authorization_code",
                        ["redirect_uri"] = redirectUri,
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret
                    }),

                CloudProvider.GoogleDrive => (
                    "https://oauth2.googleapis.com/token",
                    new Dictionary<string, string>
                    {
                        ["code"] = code,
                        ["grant_type"] = "authorization_code",
                        ["redirect_uri"] = redirectUri,
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret
                    }),

                CloudProvider.OneDrive => (
                    "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                    new Dictionary<string, string>
                    {
                        ["code"] = code,
                        ["grant_type"] = "authorization_code",
                        ["redirect_uri"] = redirectUri,
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret
                    }),

                _ => throw new NotSupportedException()
            };
        }

        private HttpRequestMessage CreateDropboxUploadRequest(string path, byte[] content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://content.dropboxapi.com/2/files/upload");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("Dropbox-API-Arg",
                JsonSerializer.Serialize(new { path, mode = "overwrite" }));
            request.Content = new ByteArrayContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return request;
        }

        private HttpRequestMessage CreateGoogleDriveUploadRequest(string name, byte[] content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var boundary = Guid.NewGuid().ToString();
            var multipart = new MultipartContent("related", boundary);

            var metadata = new StringContent(
                JsonSerializer.Serialize(new { name }),
                Encoding.UTF8, "application/json");
            multipart.Add(metadata);

            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(fileContent);

            request.Content = multipart;
            return request;
        }

        private HttpRequestMessage CreateOneDriveUploadRequest(string path, byte[] content)
        {
            var request = new HttpRequestMessage(HttpMethod.Put,
                $"https://graph.microsoft.com/v1.0/me/drive/root:/{path}:/content");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content = new ByteArrayContent(content);
            return request;
        }

        private HttpRequestMessage CreateDropboxListRequest(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api.dropboxapi.com/2/files/list_folder");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { path }),
                Encoding.UTF8, "application/json");
            return request;
        }

        private HttpRequestMessage CreateGoogleDriveListRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://www.googleapis.com/drive/v3/files?pageSize=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            return request;
        }

        private HttpRequestMessage CreateOneDriveListRequest(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://graph.microsoft.com/v1.0/me/drive/root:/{path}:/children");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            return request;
        }

        private IReadOnlyList<CloudFileInfo> ParseFileList(string json)
        {
            // Parsing simplifié : à adapter selon le provider
            var files = new List<CloudFileInfo>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var entriesProp = _provider switch
                {
                    CloudProvider.Dropbox => "entries",
                    CloudProvider.GoogleDrive => "files",
                    CloudProvider.OneDrive => "value",
                    _ => ""
                };

                if (root.TryGetProperty(entriesProp, out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        files.Add(new CloudFileInfo
                        {
                            Id = entry.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Name = entry.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""
                        });
                    }
                }
            }
            catch { }
            return files;
        }

        public void Dispose() => _http.Dispose();
    }
}
