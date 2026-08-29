// Moto.Core.Tests/Preview/WebSocketLiveReloadTests.cs
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Preview;
using Xunit;

namespace Moto.Core.Tests.Preview
{
    public class WebSocketLiveReloadTests : IDisposable
    {
        private readonly PreviewWebSocketServer _server;
        private readonly int _testPort;
        private readonly List<ClientWebSocket> _clients = new();

        public WebSocketLiveReloadTests()
        {
            _testPort = 5050 + new Random().Next(1000);
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<PreviewWebSocketServer>();
            _server = new PreviewWebSocketServer(logger, _testPort);
        }

        [Fact]
        public async Task E2E_Server_AcceptsMultipleClients()
        {
            await _server.StartAsync();
            await Task.Delay(100); // Laisser le serveur démarrer

            // Connecter 3 clients
            for (int i = 0; i < 3; i++)
            {
                var client = new ClientWebSocket();
                await client.ConnectAsync(new Uri($"ws://localhost:{_testPort}/"), CancellationToken.None);
                _clients.Add(client);
            }

            Assert.Equal(3, _server.ClientCount);
        }

        [Fact]
        public async Task E2E_NotifyRefresh_SendsToAllClients()
        {
            await _server.StartAsync();
            await Task.Delay(100);

            var receivedMessages = new List<string>();
            var clients = new List<ClientWebSocket>();

            // Connecter 2 clients
            for (int i = 0; i < 2; i++)
            {
                var client = new ClientWebSocket();
                await client.ConnectAsync(new Uri($"ws://localhost:{_testPort}/"), CancellationToken.None);
                clients.Add(client);

                // Démarrer la réception en arrière-plan
                _ = Task.Run(async () =>
                {
                    var buffer = new byte[1024];
                    while (client.State == WebSocketState.Open)
                    {
                        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            lock (receivedMessages) receivedMessages.Add(msg);
                        }
                    }
                });
            }

            await Task.Delay(200);

            // Envoyer une notification de reload
            await _server.NotifyRefreshAsync("/test/file.html");

            await Task.Delay(300);

            // Vérifier que les 2 clients ont reçu le message
            Assert.Equal(2, receivedMessages.Count);
            Assert.All(receivedMessages, msg => Assert.Contains("reload", msg));
            Assert.All(receivedMessages, msg => Assert.Contains("file.html", msg));

            foreach (var client in clients)
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "test done", CancellationToken.None);
        }

        [Fact]
        public async Task E2E_ClientDisconnect_RemovedFromList()
        {
            await _server.StartAsync();
            await Task.Delay(100);

            var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"ws://localhost:{_testPort}/"), CancellationToken.None);
            _clients.Add(client);

            await Task.Delay(200);
            Assert.Equal(1, _server.ClientCount);

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            await Task.Delay(300);

            Assert.Equal(0, _server.ClientCount);
        }

        [Fact]
        public async Task E2E_Reconnect_AfterDisconnect()
        {
            await _server.StartAsync();
            await Task.Delay(100);

            // Première connexion
            var client1 = new ClientWebSocket();
            await client1.ConnectAsync(new Uri($"ws://localhost:{_testPort}/"), CancellationToken.None);
            _clients.Add(client1);
            await Task.Delay(200);
            Assert.Equal(1, _server.ClientCount);

            // Déconnexion
            await client1.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect test", CancellationToken.None);
            await Task.Delay(300);
            Assert.Equal(0, _server.ClientCount);

            // Reconnexion
            var client2 = new ClientWebSocket();
            await client2.ConnectAsync(new Uri($"ws://localhost:{_testPort}/"), CancellationToken.None);
            _clients.Add(client2);
            await Task.Delay(200);
            Assert.Equal(1, _server.ClientCount);
        }

        public void Dispose()
        {
            _server.Stop();
            foreach (var client in _clients)
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                        client.Abort();
                    client.Dispose();
                }
                catch { }
            }
            _server.Dispose();
        }
    }
}
