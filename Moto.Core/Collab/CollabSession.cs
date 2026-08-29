// Moto.Core/Collab/CollabSession.cs
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Collab
{
    /// <summary>
    /// 5. SESSION DE CO-WORKING.
    /// Fonctionne en mode CLIENT (rejoint un serveur distant)
    /// ou SERVEUR (héberge la session sur ce PC).
    /// Protocole : JSON sur WebSocket.
    /// </summary>
    public class CollabSession : IDisposable
    {
        public CollabPresence Presence { get; } = new();
        public PatchEngine Patches { get; } = new();
        public CollabPeer Self { get; } = new();

        private ClientWebSocket _clientWs;
        private CancellationTokenSource _cts;

        /// <summary>Reçu du serveur ou des autres pairs : patch, présence, message.</summary>
        public event Action<TextPatch> RemotePatch;
        public event Action<CollabPeer> PeerUpdated;
        public event Action<string> RemoteMessage;

        public bool IsHost { get; private set; }
        public bool IsConnected => _clientWs?.State == WebSocketState.Open;

        /// <summary>Rejoint une session distante (WebSocket).</summary>
        public async Task<bool> JoinAsync(string host, int port, string userName)
        {
            Self.Name = userName;

            _clientWs = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            try
            {
                await _clientWs.ConnectAsync(
                    new Uri($"ws://{host}:{port}/collab"),
                    _cts.Token);

                await SendAsync(new { type = "join", peer = Self });
                _ = ReceiveLoopAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Quitte la session.</summary>
        public async Task LeaveAsync()
        {
            _cts?.Cancel();

            if (_clientWs?.State == WebSocketState.Open)
            {
                try { await _clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
                catch { }
            }
        }

        /// <summary>Envoie un patch (modification locale) aux autres.</summary>
        public async Task BroadcastPatchAsync(TextPatch patch)
        {
            await SendAsync(new { type = "patch", patch });
        }

        /// <summary>Envoie sa présence (curseur, fichier actif).</summary>
        public async Task BroadcastPresenceAsync()
        {
            await SendAsync(new { type = "presence", peer = Self });
        }

        /// <summary>Envoie un message de chat.</summary>
        public async Task SendMessageAsync(string text)
        {
            await SendAsync(new { type = "chat", from = Self.Name, text });
        }

        private async Task SendAsync(object payload)
        {
            if (_clientWs?.State != WebSocketState.Open) return;

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _clientWs.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];

            try
            {
                while (_clientWs.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var result = await _clientWs.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close) return;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleIncoming(json);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private void HandleIncoming(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "patch":
                        var patch = JsonSerializer.Deserialize<TextPatch>(
                            root.GetProperty("patch").GetRawText());
                        RemotePatch?.Invoke(patch);
                        break;

                    case "presence":
                        var peer = JsonSerializer.Deserialize<CollabPeer>(
                            root.GetProperty("peer").GetRawText());
                        Presence.Upsert(peer);
                        PeerUpdated?.Invoke(peer);
                        break;

                    case "chat":
                        var from = root.GetProperty("from").GetString();
                        var text = root.GetProperty("text").GetString();
                        RemoteMessage?.Invoke($"{from} : {text}");
                        break;
                }
            }
            catch
            {
                // Message malformé : ignoré.
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _clientWs?.Dispose();
        }
    }
}
