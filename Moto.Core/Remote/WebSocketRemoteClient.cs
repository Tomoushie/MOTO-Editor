// Moto.Core/Remote/WebSocketRemoteClient.cs
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Remote
{
    /// <summary>
    /// 4. Client WebSocket pour connexion à un serveur distant
    /// (RAG, serveur VS Code, serveur MOTO, PC distant).
    /// </summary>
    public class WebSocketRemoteClient : RemoteClient
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;

        public WebSocketRemoteClient()
        {
            Kind = RemoteKind.WebSocket;
        }

        public override async Task<bool> ConnectAsync(string host, int port, string user = null, string token = null)
        {
            Host = host;
            Port = port;

            try
            {
                _ws = new ClientWebSocket();
                _cts = new CancellationTokenSource();

                var uri = new Uri($"ws://{host}:{port}/moto");
                await _ws.ConnectAsync(uri, _cts.Token);

                IsConnected = true;

                // Handshake
                await SendAsync(JsonSerializer.Serialize(new
                {
                    type = "hello",
                    user = user ?? "anonymous",
                    token = token ?? ""
                }));

                _ = ReceiveLoopAsync();
                return true;
            }
            catch (Exception ex)
            {
                RaiseDisconnected("Connexion échouée : " + ex.Message);
                return false;
            }
        }

        public override async Task DisconnectAsync()
        {
            if (_ws == null) return;

            try
            {
                _cts?.Cancel();
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { }

            IsConnected = false;
            RaiseDisconnected("Déconnecté.");
        }

        public override async Task SendAsync(string payload)
        {
            if (_ws?.State != WebSocketState.Open) return;

            var bytes = Encoding.UTF8.GetBytes(payload);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }

        public override async Task<IReadOnlyList<string>> ListFilesAsync(string root = "/")
        {
            await SendAsync(JsonSerializer.Serialize(new { type = "list", path = root }));
            return new List<string> { "(liste reçue via MessageReceived)" };
        }

        public override async Task<string> ReadFileAsync(string path)
        {
            await SendAsync(JsonSerializer.Serialize(new { type = "read", path }));
            return "(contenu reçu via MessageReceived)";
        }

        public override async Task WriteFileAsync(string path, string content)
        {
            await SendAsync(JsonSerializer.Serialize(new { type = "write", path, content }));
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[8192];

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            IsConnected = false;
                            RaiseDisconnected("Fermé par le serveur.");
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    RaiseMessage(sb.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                IsConnected = false;
                RaiseDisconnected("Erreur : " + ex.Message);
            }
        }

        public override void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}
