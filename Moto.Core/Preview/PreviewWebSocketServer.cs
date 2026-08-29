// Moto.Core/Preview/PreviewWebSocketServer.cs
// Serveur WebSocket local pour live-reload du preview HTML/JS/CSS.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Preview
{
    /// <summary>
    /// Serveur WebSocket local (port 5050 par défaut).
    /// Diffuse les notifications de refresh aux navigateurs connectés.
    /// </summary>
    public sealed class PreviewWebSocketServer : IDisposable
    {
        private readonly ILogger<PreviewWebSocketServer> _logger;
        private readonly int _port;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly List<WebSocket> _clients = new();
        private readonly SemaphoreSlim _clientsLock = new(1, 1);
        private int _version;

        public event Action<int>? ClientCountChanged;

        public PreviewWebSocketServer(
            ILogger<PreviewWebSocketServer> logger,
            int port = 5050)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _port = port;
        }

        public int ClientCount
        {
            get
            {
                _clientsLock.Wait();
                try { return _clients.Count; }
                finally { _clientsLock.Release(); }
            }
        }

        public bool IsRunning => _listener?.IsListening ?? false;

        /// <summary>Démarre le serveur WebSocket.</summary>
        public async Task StartAsync()
        {
            if (_listener?.IsListening == true) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                _logger.LogInformation("[PreviewWS] Serveur démarré sur ws://localhost:{Port}", _port);

                _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PreviewWS] Erreur démarrage serveur");
            }

            await Task.CompletedTask;
        }

        /// <summary>Arrête le serveur.</summary>
        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _logger.LogInformation("[PreviewWS] Serveur arrêté");
        }

        /// <summary>
        /// Notifie tous les clients connectés de recharger le preview.
        /// </summary>
        public async Task NotifyRefreshAsync(string? changedFile = null)
        {
            var version = Interlocked.Increment(ref _version);
            var message = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "reload",
                version,
                file = changedFile,
                timestamp = DateTime.UtcNow
            });

            var bytes = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(bytes);

            await _clientsLock.WaitAsync().ConfigureAwait(false);
            var toRemove = new List<WebSocket>();
            try
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        if (client.State == WebSocketState.Open)
                        {
                            await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            toRemove.Add(client);
                        }
                    }
                    catch
                    {
                        toRemove.Add(client);
                    }
                }

                foreach (var ws in toRemove)
                {
                    _clients.Remove(ws);
                    try { ws.Dispose(); } catch { }
                }
            }
            finally
            {
                _clientsLock.Release();
            }

            ClientCountChanged?.Invoke(ClientCount);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(wsContext.WebSocket, ct));
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        _logger.LogError(ex, "[PreviewWS] Erreur accept");
                }
            }
        }

        private async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
        {
            await _clientsLock.WaitAsync(ct).ConfigureAwait(false);
            try { _clients.Add(ws); }
            finally { _clientsLock.Release(); }

            ClientCountChanged?.Invoke(ClientCount);
            _logger.LogInformation("[PreviewWS] Client connecté ({Count})", ClientCount);

            var buffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct)
                        .ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            catch { }
            finally
            {
                await _clientsLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    _clients.Remove(ws);
                    try { ws.Dispose(); } catch { }
                }
                finally { _clientsLock.Release(); }

                ClientCountChanged?.Invoke(ClientCount);
                _logger.LogInformation("[PreviewWS] Client déconnecté ({Count})", ClientCount);
            }
        }

        public void Dispose()
        {
            Stop();
            _clientsLock.Dispose();
            _cts?.Dispose();
        }
    }
}
