// Moto.Core/Remote/SshRemoteClient.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Core.Remote
{
    /// <summary>
    /// 4. Client SSH (VS Code-style Remote-SSH).
    /// Nécessite le package NuGet `SSH.NET` :
    ///   dotnet add package SSH.NET
    /// Sans le package, l'implémentation reste abstraite et fonctionnelle en stub.
    /// </summary>
    public class SshRemoteClient : RemoteClient
    {
        private object _client; // Renci.SshNet.SshClient si disponible
        private string _user;
        private string _keyOrPassword;

        public SshRemoteClient()
        {
            Kind = RemoteKind.Ssh;
        }

        public override async Task<bool> ConnectAsync(string host, int port, string user = null, string token = null)
        {
            Host = host;
            Port = port == 0 ? 22 : port;
            _user = user ?? "root";
            _keyOrPassword = token ?? "";

            // Tentative via réflexion (si SSH.NET est présent).
            try
            {
                var asm = System.Reflection.Assembly.Load("Renci.SshNet");
                var t = asm.GetType("Renci.SshNet.SshClient");
                _client = Activator.CreateInstance(t, host, Port, _user, _keyOrPassword);
                t.GetMethod("Connect")?.Invoke(_client, null);

                IsConnected = true;
                return await Task.FromResult(true);
            }
            catch
            {
                // Package absent : mode stub.
                IsConnected = false;
                return await Task.FromResult(false);
            }
        }

        public override Task DisconnectAsync()
        {
            try
            {
                _client?.GetType().GetMethod("Disconnect")?.Invoke(_client, null);
                (_client as IDisposable)?.Dispose();
            }
            catch { }

            IsConnected = false;
            return Task.CompletedTask;
        }

        public override async Task SendAsync(string payload)
        {
            if (!IsConnected) return;
            await RunCommandAsync(payload);
        }

        public override async Task<IReadOnlyList<string>> ListFilesAsync(string root = "/")
        {
            var output = await RunCommandAsync($"ls -1 {Escape(root)}");
            return (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        public override async Task<string> ReadFileAsync(string path)
        {
            return await RunCommandAsync($"cat {Escape(path)}");
        }

        public override async Task WriteFileAsync(string path, string content)
        {
            var escaped = content.Replace("'", "'\\''");
            await RunCommandAsync($"printf '%s' '{escaped}' > {Escape(path)}");
        }

        private Task<string> RunCommandAsync(string cmd)
        {
            try
            {
                var method = _client?.GetType().GetMethod("RunCommand", new[] { typeof(string) });
                var result = method?.Invoke(_client, new object[] { cmd });
                var prop = result?.GetType().GetProperty("Result");
                var text = prop?.GetValue(result) as string ?? "";
                return Task.FromResult(text);
            }
            catch (Exception ex)
            {
                return Task.FromResult("Erreur SSH : " + ex.Message);
            }
        }

        private static string Escape(string path) =>
            $"'{path.Replace("'", "'\\''")}'";

        public override void Dispose()
        {
            (_client as IDisposable)?.Dispose();
        }
    }
}
