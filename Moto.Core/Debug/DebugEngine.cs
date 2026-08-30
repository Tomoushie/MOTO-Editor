// Moto.Core/Debug/DebugEngine.cs
// Debug Adapter Protocol (DAP) minimal pour .NET.
// Lance vscode-dbgshim ou netcoredbg en stdio, parle JSON-RPC.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Debug
{
    public sealed class StackFrameInfo
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? FilePath { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
    }

    public sealed class DebugSession
    {
        public string ProgramPath { get; init; } = string.Empty;
        public string WorkingDirectory { get; init; } = string.Empty;
        public string[] Args { get; init; } = Array.Empty<string>();
        public bool StopAtEntry { get; init; }
    }

    /// <summary>
    /// Moteur DAP minimal pour déboguer des projets .NET.
    /// Utilise netcoredbg en stdio (JSON-RPC).
    /// </summary>
    public sealed class DebugEngine : IDisposable
    {
        private Process? _debuggerProcess;
        private StreamWriter? _stdin;
        private CancellationTokenSource? _readerCts;
        private int _sequence = 1;
        private readonly object _seqLock = new();
        private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = new();

        public event Action<string>? OutputReceived;
        public event Action<BreakpointInfo>? BreakpointHit;
        public event Action? SessionEnded;

        /// <summary>
        /// Lance une session de debug avec netcoredbg.
        /// </summary>
        public async Task<bool> StartAsync(DebugSession session, string debuggerPath = "netcoredbg")
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = debuggerPath,
                    Arguments = "--interpreter=vscode",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = session.WorkingDirectory
                };

                _debuggerProcess = Process.Start(psi);
                if (_debuggerProcess == null) return false;

                _stdin = _debuggerProcess.StandardInput;
                _readerCts = new CancellationTokenSource();

                // Démarrer le reader en arrière-plan
                _ = Task.Run(() => ReadOutputLoop(_readerCts.Token));
                _ = Task.Run(() => ReadErrorLoop(_readerCts.Token));

                // Handshake DAP
                await SendRequestAsync("initialize", new { clientID = "moto", adapterID = "coreclr" });
                await SendRequestAsync("launch", new
                {
                    program = session.ProgramPath,
                    cwd = session.WorkingDirectory,
                    args = session.Args,
                    stopAtEntry = session.StopAtEntry
                });

                return true;
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"❌ Échec démarrage DAP : {ex.Message}");
                return false;
            }
        }

        /// <summary>Place un breakpoint à une ligne donnée.</summary>
        public async Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(string filePath, int[] lines)
        {
            var breakpoints = new List<object>();
            foreach (var line in lines)
                breakpoints.Add(new { line });

            var response = await SendRequestAsync("setBreakpoints", new
            {
                source = new { path = filePath },
                breakpoints
            });

            var result = new List<BreakpointInfo>();
            if (response.TryGetProperty("body", out var body) &&
                body.TryGetProperty("breakpoints", out var bps))
            {
                int id = 1;
                foreach (var bp in bps.EnumerateArray())
                {
                    result.Add(new BreakpointInfo
                    {
                        Id = id++,
                        FilePath = filePath,
                        Line = bp.TryGetProperty("line", out var l) ? l.GetInt32() : 0,
                        Verified = bp.TryGetProperty("verified", out var v) && v.GetBoolean()
                    });
                }
            }
            return result;
        }

        /// <summary>Continue après un breakpoint.</summary>
        public async Task ContinueAsync(int threadId = 1)
        {
            await SendRequestAsync("continue", new { threadId });
        }

        /// <summary>Step over.</summary>
        public async Task StepOverAsync(int threadId = 1)
        {
            await SendRequestAsync("next", new { threadId });
        }

        /// <summary>Step into.</summary>
        public async Task StepIntoAsync(int threadId = 1)
        {
            await SendRequestAsync("stepIn", new { threadId });
        }

        /// <summary>Récupère la stack trace courante.</summary>
        public async Task<IReadOnlyList<StackFrameInfo>> GetStackTraceAsync(int threadId = 1)
        {
            var response = await SendRequestAsync("stackTrace", new { threadId });
            var result = new List<StackFrameInfo>();

            if (response.TryGetProperty("body", out var body) &&
                body.TryGetProperty("stackFrames", out var frames))
            {
                foreach (var f in frames.EnumerateArray())
                {
                    result.Add(new StackFrameInfo
                    {
                        Id = f.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        Name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Line = f.TryGetProperty("line", out var l) ? l.GetInt32() : 0,
                        Column = f.TryGetProperty("column", out var c) ? c.GetInt32() : 0,
                        FilePath = f.TryGetProperty("source", out var src) &&
                                   src.TryGetProperty("path", out var p) ? p.GetString() : null
                    });
                }
            }
            return result;
        }

        /// <summary>Arrête la session de debug.</summary>
        public async Task StopAsync()
        {
            try
            {
                await SendRequestAsync("disconnect", new { terminateDebuggee = true });
            }
            catch { }

            _readerCts?.Cancel();
            if (_debuggerProcess != null && !_debuggerProcess.HasExited)
            {
                try { _debuggerProcess.Kill(); } catch { }
            }
            _debuggerProcess?.Dispose();
            _debuggerProcess = null;
        }

        private async Task<JsonElement> SendRequestAsync(string command, object args)
        {
            if (_stdin == null)
                throw new InvalidOperationException("Session de debug non démarrée.");

            int seq;
            lock (_seqLock) seq = _sequence++;

            var tcs = new TaskCompletionSource<JsonElement>();
            lock (_seqLock) _pending[seq] = tcs;

            var request = new
            {
                seq,
                type = "request",
                command,
                arguments = args
            };

            var json = JsonSerializer.Serialize(request);
            var header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";
            await _stdin.WriteAsync(header);
            await _stdin.WriteAsync(json);
            await _stdin.FlushAsync();

            // Timeout de 10s
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            cts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task;
        }

        private async Task ReadOutputLoop(CancellationToken ct)
        {
            if (_debuggerProcess == null) return;
            var reader = _debuggerProcess.StandardOutput;

            try
            {
                while (!ct.IsCancellationRequested && !_debuggerProcess.HasExited)
                {
                    var header = await reader.ReadLineAsync();
                    if (header == null) break;
                    if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var lengthStr = header.Substring("Content-Length:".Length).Trim();
                    if (!int.TryParse(lengthStr, out var length)) continue;

                    await reader.ReadLineAsync(); // ligne vide

                    var buffer = new char[length];
                    var read = 0;
                    while (read < length)
                    {
                        var chunk = await reader.ReadAsync(buffer, read, length - read);
                        if (chunk == 0) break;
                        read += chunk;
                    }

                    var json = new string(buffer, 0, read);
                    HandleMessage(json);
                }
            }
            catch { }

            MainThread.BeginInvokeOnMainThread(() => SessionEnded?.Invoke());
        }

        private async Task ReadErrorLoop(CancellationToken ct)
        {
            if (_debuggerProcess == null) return;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await _debuggerProcess.StandardError.ReadLineAsync();
                    if (line == null) break;
                    MainThread.BeginInvokeOnMainThread(() => OutputReceived?.Invoke($"[debug-err] {line}"));
                }
            }
            catch { }
        }

        private void HandleMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var type))
                {
                    var typeStr = type.GetString();
                    if (typeStr == "response" && root.TryGetProperty("request_seq", out var rseq))
                    {
                        var seq = rseq.GetInt32();
                        TaskCompletionSource<JsonElement>? tcs;
                        lock (_seqLock)
                        {
                            if (_pending.TryGetValue(seq, out tcs))
                                _pending.Remove(seq);
                        }
                        tcs?.TrySetResult(root);
                    }
                    else if (typeStr == "event" && root.TryGetProperty("event", out var evt))
                    {
                        HandleEvent(evt.GetString() ?? "", root);
                    }
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    OutputReceived?.Invoke($"[DAP] Parse error: {ex.Message}"));
            }
        }

        private void HandleEvent(string eventName, JsonElement root)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (eventName)
                {
                    case "output":
                        if (root.TryGetProperty("body", out var b) &&
                            b.TryGetProperty("output", out var o))
                            OutputReceived?.Invoke(o.GetString() ?? "");
                        break;

                    case "stopped":
                        // Pourrait déclencher BreakpointHit via stackTrace
                        OutputReceived?.Invoke("⏸ Debug arrêté (breakpoint ou exception)");
                        break;

                    case "terminated":
                    case "exited":
                        SessionEnded?.Invoke();
                        break;
                }
            });
        }

        public void Dispose()
        {
            _readerCts?.Cancel();
            _debuggerProcess?.Dispose();
            _readerCts?.Dispose();
        }
    }
}
