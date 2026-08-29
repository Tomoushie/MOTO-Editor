// Moto.Core/Collab/CrdtSyncService.cs
// Service de synchronisation CRDT via WebSocket.
// Gère la connexion, la reconnexion, et la diffusion des patches.
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Collab
{
    public sealed class CrdtPeerInfo
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Color { get; init; } = "#D97757";
        public bool IsOnline { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }

    public sealed class CrdtSyncMessage
    {
        public string Type { get; init; } = string.Empty; // patch, cursor, presence, state
        public string DocumentId { get; init; } = string.Empty;
        public byte[]? Data { get; init; }
        public string? UserId { get; init; }
    }

    /// <summary>
    /// Service de synchronisation CRDT.
    /// Diffuse les patches Automerge aux pairs connectés.
    /// </summary>
    public sealed class CrdtSyncService : IAsyncDisposable
    {
        private readonly ILogger<CrdtSyncService> _logger;
        private readonly CrdtAutomergeClient _automerge;
        private ClientWebSocket? _socket;
        private CancellationTokenSource? _cts;
        private readonly Dictionary<string, CrdtPeerInfo> _peers = new();
        private bool _connected;

        /// <summary>Déclenché quand un patch distant est reçu.</summary>
        public event Action<CrdtPatch>? RemotePatchReceived;

        /// <summary>Déclenché quand la liste des pairs change.</summary>
        public event Action<IReadOnlyList<CrdtPeerInfo>>? PeersChanged;

        /// <summary>Déclenché quand la connexion change.</summary>
        public event Action<bool>? ConnectionChanged;

        public CrdtSyncService(
            CrdtAutomergeClient automerge,
            ILogger<CrdtSyncService> logger)
        {
            _automerge = automerge ?? throw new ArgumentNullException(nameof(automerge));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConnected => _connected;
        public IReadOnlyList<CrdtPeerInfo> Peers => _peers.Values.ToList();

        /// <summary>
        /// Se connecte à un serveur de synchronisation CRDT.
        /// </summary>
        public async Task ConnectAsync(string serverUrl, string userId, string displayName, CancellationToken ct = default)
        {
            try
            {
                _socket = new ClientWebSocket();
                _cts = new CancellationTokenSource();

                await _socket.ConnectAsync(new Uri(serverUrl), ct).ConfigureAwait(false);
                _connected = true;
                ConnectionChanged?.Invoke(true);
                _logger.LogInformation("[CRDT] Connecté à {Url}", serverUrl);

                // S'abonner aux patches locaux pour diffusion
                _automerge.PatchGenerated += OnLocalPatchGenerated;

                // Envoyer la présence initiale
                await SendPresenceAsync(userId, displayName, online: true).ConfigureAwait(false);

                // Démarrer la boucle de réception
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRDT] Échec de connexion à {Url}", serverUrl);
                _connected = false;
                ConnectionChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Se déconnecte proprement.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_socket == null) return;

            _automerge.PatchGenerated -= OnLocalPatchGenerated;

            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch { }

            _connected = false;
            ConnectionChanged?.Invoke(false);
            _logger.LogInformation("[CRDT] Déconnecté.");
        }

        /// <summary>
        /// Diffuse un patch local aux pairs.
        /// </summary>
        private async void OnLocalPatchGenerated(CrdtPatch patch)
        {
            if (!_connected || _socket == null) return;

            try
            {
                var message = new CrdtSyncMessage
                {
                    Type = "patch",
                    Data = patch.Data,
                    UserId = patch.ActorId.ToString()
                };

                await SendAsync(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRDT] Échec d'envoi du patch.");
            }
        }

        /// <summary>
        /// Envoie la présence (curseur, statut en ligne).
        /// </summary>
        public async Task SendPresenceAsync(string userId, string displayName, bool online)
        {
            var message = new CrdtSyncMessage
            {
                Type = "presence",
                UserId = userId,
                Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    displayName,
                    online,
                    timestampUtc = DateTime.UtcNow
                }))
            };
            await SendAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Envoie la position du curseur.
        /// </summary>
        public async Task SendCursorAsync(string userId, string documentId, int line, int column)
        {
            var message = new CrdtSyncMessage
            {
                Type = "cursor",
                DocumentId = documentId,
                UserId = userId,
                Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { line, column }))
            };
            await SendAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Demande l'état complet d'un document (synchronisation initiale).
        /// </summary>
        public async Task RequestStateAsync(string documentId)
        {
            var message = new CrdtSyncMessage
            {
                Type = "state_request",
                DocumentId = documentId
            };
            await SendAsync(message).ConfigureAwait(false);
        }

        // ── Boucle de réception ──
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _connected = false;
                        ConnectionChanged?.Invoke(false);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleMessageAsync(json).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRDT] Erreur dans la boucle de réception.");
                _connected = false;
                ConnectionChanged?.Invoke(false);
            }
        }

        private async Task HandleMessageAsync(string json)
        {
            try
            {
                var message = JsonSerializer.Deserialize<CrdtSyncMessage>(json);
                if (message == null) return;

                switch (message.Type)
                {
                    case "patch":
                        if (message.Data != null)
                        {
                            var patch = new CrdtPatch
                            {
                                Data = message.Data,
                                ActorId = long.TryParse(message.UserId, out var id) ? id : 0
                            };
                            await _automerge.MergeAsync(patch).ConfigureAwait(false);
                            RemotePatchReceived?.Invoke(patch);
                        }
                        break;

                    case "presence":
                        if (message.Data != null && message.UserId != null)
                        {
                            var presenceJson = Encoding.UTF8.GetString(message.Data);
                            var presence = JsonSerializer.Deserialize<Dictionary<string, object>>(presenceJson);
                            _peers[message.UserId] = new CrdtPeerInfo
                            {
                                UserId = message.UserId,
                                DisplayName = presence?.ContainsKey("displayName") == true
                                    ? presence["displayName"]?.ToString() ?? message.UserId
                                    : message.UserId,
                                IsOnline = presence?.ContainsKey("online") == true
                                    && bool.TryParse(presence["online"]?.ToString(), out var on) && on,
                                LastSeenUtc = DateTime.UtcNow
                            };
                            PeersChanged?.Invoke(_peers.Values.ToList());
                        }
                        break;

                    case "state":
                        if (message.Data != null)
                        {
                            await _automerge.ImportStateAsync(message.Data).ConfigureAwait(false);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CRDT] Erreur de traitement du message.");
            }
        }

        private async Task SendAsync(CrdtSyncMessage message)
        {
            if (_socket == null || _socket.State != WebSocketState.Open) return;

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            if (_connected) await DisconnectAsync().ConfigureAwait(false);
            _socket?.Dispose();
            _cts?.Dispose();
        }
    }
}
