// Moto.Marketplace.Api/Services/AnalyticsWebSocketServer.cs
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Moto.Marketplace.Api.Services
{
    public sealed class AnalyticsWebSocketServer : IDisposable
    {
        private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
        private readonly ILogger<AnalyticsWebSocketServer> _logger;

        public AnalyticsWebSocketServer(ILogger<AnalyticsWebSocketServer> logger)
        {
            _logger = logger;
        }

        public int ClientCount => _clients.Count;

        public async Task HandleClientAsync(WebSocket webSocket, string clientId)
        {
            _clients.TryAdd(clientId, webSocket);
            _logger.LogInformation("[Analytics WS] Client connecté : {ClientId} (total: {Count})", clientId, ClientCount);

            var buffer = new byte[1024];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                _logger.LogInformation("[Analytics WS] Client déconnecté : {ClientId}", clientId);
            }
        }

        public async Task BroadcastAnalyticsUpdateAsync(AnalyticsUpdate update)
        {
            var json = JsonSerializer.Serialize(update);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            var disconnectedClients = new List<string>();

            foreach (var (clientId, webSocket) in _clients)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        disconnectedClients.Add(clientId);
                    }
                }
                catch
                {
                    disconnectedClients.Add(clientId);
                }
            }

            foreach (var clientId in disconnectedClients)
                _clients.TryRemove(clientId, out _);
        }

        public void Dispose()
        {
            foreach (var webSocket in _clients.Values)
            {
                try
                {
                    webSocket.Abort();
                    webSocket.Dispose();
                }
                catch { }
            }
            _clients.Clear();
        }
    }

    public sealed class AnalyticsUpdate
    {
        public string Type { get; init; } = string.Empty; // "download", "rating", "install"
        public string PluginId { get; init; } = string.Empty;
        public long TotalDownloads { get; init; }
        public double AverageRating { get; init; }
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }
}
