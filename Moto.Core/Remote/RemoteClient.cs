// Moto.Core/Remote/RemoteClient.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Core.Remote
{
    public enum RemoteKind { WebSocket, Ssh, RagServer, VsCodeServer }

    /// <summary>
    /// 4. CLIENT DISTANT (abstraction).
    /// Connecte MOTO Editor à un PC, serveur, RAG ou serveur VS Code distant.
    /// </summary>
    public abstract class RemoteClient : IDisposable
    {
        public string Host { get; protected set; } = string.Empty;
        public int Port { get; protected set; }
        public bool IsConnected { get; protected set; }
        public RemoteKind Kind { get; protected set; }

        public event Action<string> MessageReceived;
        public event Action<string> Disconnected;

        public abstract Task<bool> ConnectAsync(string host, int port, string user = null, string token = null);
        public abstract Task DisconnectAsync();
        public abstract Task SendAsync(string payload);

        /// <summary>Liste les fichiers distants (implémenté selon le protocole).</summary>
        public abstract Task<IReadOnlyList<string>> ListFilesAsync(string root = "/");

        /// <summary>Lit un fichier distant.</summary>
        public abstract Task<string> ReadFileAsync(string path);

        /// <summary>Écrit un fichier distant.</summary>
        public abstract Task WriteFileAsync(string path, string content);

        protected void RaiseMessage(string msg) => MessageReceived?.Invoke(msg);
        protected void RaiseDisconnected(string reason) => Disconnected?.Invoke(reason);

        public virtual void Dispose() { }
    }
}
