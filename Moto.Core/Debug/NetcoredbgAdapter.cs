// Moto.Core/Debug/NetcoredbgAdapter.cs
// Adaptateur DAP (Debug Adapter Protocol) via netcoredbg en stdio.
// Implémente : launch, breakpoints, stepping, variables, call stack, watch.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Debug
{
    /// <summary>Événement DAP reçu du debugger.</summary>
    public sealed class DapEvent
    {
        public string Event { get; init; } = string.Empty;
        public JsonElement? Body { get; init; }
    }

    /// <summary>
    /// Adaptateur DAP qui pilote netcoredbg via stdio.
    /// Protocole : JSON-RPC avec header Content-Length.
    /// </summary>
    public sealed class NetcoredbgAdapter : IAsyncDisposable
    {
        private readonly ILogger<NetcoredbgAdapter> _logger;
        private Process? _process;
        private StreamWriter? _stdin;
        private int _sequence = 1;
        private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
        private readonly object _seqLock = new();
        private CancellationTokenSource? _readerCts;

        /// <summary>Déclenché quand le debugger atteint un breakpoint.</summary>
        public event Action<int, string, int>? Stopped;

        /// <summary>Déclenché quand la session se termine.</summary>
        public event Action? Terminated;

        /// <summary>Déclenché pour la sortie du programme debuggé.</summary>
        public event Action<string>? OutputReceived;

        public NetcoredbgAdapter(ILogger<NetcoredbgAdapter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Lance une session de debug avec netcoredbg.
        /// </summary>
        public async Task<bool> LaunchAsync(DebugSession session, CancellationToken ct = default)
        {
            var debuggerPath = FindNetcoredbg();
            if (debuggerPath == null)
            {
                _logger.LogError("[DAP] netcoredbg non trouvé.");
                return false;
            }

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

                _process = Process.Start(psi);
                if (_process == null) return false;

                _stdin = _process.StandardInput;
                _readerCts = new CancellationTokenSource();

                _ = Task.Run(() => ReadLoopAsync(_readerCts.Token));
                _ = Task.Run(() => ReadErrorLoopAsync(_readerCts.Token));

                // Handshake DAP
                await SendRequestAsync("initialize", new
                {
                    adapterID = "coreclr",
                    clientID = "moto-editor",
                    clientName = "MOTO Editor",
                    linesStartAt1 = true,
                    columnsStartAt1 = true
                }, ct).ConfigureAwait(false);

                // Launch
                await SendRequestAsync("launch", new
                {
                    program = session.ProgramPath,
                    cwd = session.WorkingDirectory,
                    args = session.Args,
                    stopAtEntry = session.StopAtEntry,
                    console = "internalConsole"
                }, ct).ConfigureAwait(false);

                _logger.LogInformation("[DAP] Session lancée : {Program}", session.ProgramPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DAP] Échec du lancement.");
                return false;
            }
        }

        /// <summary>Place des breakpoints dans un fichier.</summary>
        public async Task<IReadOnlyList<BreakpointInfo>> SetBreakpointsAsync(
            string filePath, int[] lines, CancellationToken ct = default)
        {
            var breakpoints = new List<object>();
            foreach (var line in lines)
                breakpoints.Add(new { line });

            var response = await SendRequestAsync("setBreakpoints", new
            {
                source = new { path = filePath },
                breakpoints
            }, ct).ConfigureAwait(false);

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

        /// <summary>Continue après un arrêt.</summary>
        public async Task ContinueAsync(int threadId = 1, CancellationToken ct = default)
        {
            await SendRequestAsync("continue", new { threadId }, ct).ConfigureAwait(false);
        }

        /// <summary>Step over (F10).</summary>
        public async Task NextAsync(int threadId = 1, CancellationToken ct = default)
        {
            await SendRequestAsync("next", new { threadId }, ct).ConfigureAwait(false);
        }

        /// <summary>Step into (F11).</summary>
        public async Task StepInAsync(int threadId = 1, CancellationToken ct = default)
        {
            await SendRequestAsync("stepIn", new { threadId }, ct).ConfigureAwait(false);
        }

        /// <summary>Step out (Shift+F11).</summary>
        public async Task StepOutAsync(int threadId = 1, CancellationToken ct = default)
        {
            await SendRequestAsync("stepOut", new { threadId }, ct).ConfigureAwait(false);
        }

        /// <summary>Récupère les threads actifs.</summary>
        public async Task<IReadOnlyList<StackFramePro>> GetStackTraceAsync(
            int threadId = 1, CancellationToken ct = default)
        {
            var response = await SendRequestAsync("stackTrace", new
            {
                threadId,
                startFrame = 0,
                levels = 20
            }, ct).ConfigureAwait(false);

            var result = new List<StackFramePro>();
            if (response.TryGetProperty("body", out var body) &&
                body.TryGetProperty("stackFrames", out var frames))
            {
                foreach (var f in frames.EnumerateArray())
                {
                    result.Add(new StackFramePro
                    {
                        Id = f.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        Name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        FilePath = f.TryGetProperty("source", out var src) &&
                                   src.TryGetProperty("path", out var p) ? p.GetString() : null,
                        Line = f.TryGetProperty("line", out var l) ? l.GetInt32() : 0,
                        Column = f.TryGetProperty("column", out var c) ? c.GetInt32() : 0
                    });
                }
            }
            return result;
        }

        /// <summary>Récupère les variables d'un scope.</summary>
        public async Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(
            int variablesReference, CancellationToken ct = default)
        {
            var response = await SendRequestAsync("variables", new
            {
                variablesReference
            }, ct).ConfigureAwait(false);

            var result = new List<VariableInfo>();
            if (response.TryGetProperty("body", out var body) &&
                body.TryGetProperty("variables", out var vars))
            {
                foreach (var v in vars.EnumerateArray())
                {
                    result.Add(new VariableInfo
                    {
                        Name = v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Value = v.TryGetProperty("value", out var val) ? val.GetString() ?? "" : "",
                        Type = v.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                        IsExpandable = v.TryGetProperty("variablesReference", out var vr) && vr.GetInt32() > 0
                    });
                }
            }
            return result;
        }

        /// <summary>Évalue une expression (watch).</summary>
        public async Task<string?> EvaluateAsync(
            string expression, int frameId = 0, CancellationToken ct = default)
        {
            var response = await SendRequestAsync("evaluate", new
            {
                expression,
                frameId,
                context = "watch"
            }, ct).ConfigureAwait(false);

            if (response.TryGetProperty("body", out var body) &&
                body.TryGetProperty("result", out var result))
            {
                return result.GetString();
            }
            return null;
        }

        /// <summary>Arrête la session de debug.</summary>
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            try
            {
                await SendRequestAsync("disconnect", new
                {
                    terminateDebuggee = true
                }, ct).ConfigureAwait(false);
            }
            catch { }

            _readerCts?.Cancel();
            if (_process != null && !_process.HasExited)
            {
                try { _process.Kill(); } catch { }
            }
        }

        // ── Boucle de lecture ──
        private async Task ReadLoopAsync(CancellationToken ct)
        {
            if (_process == null) return;
            var reader = _process.StandardOutput;

            try
            {
                while (!ct.IsCancellationRequested && !_process.HasExited)
                {
                    var header = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (header == null) break;
                    if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var lengthStr = header.Substring("Content-Length:".Length).Trim();
                    if (!int.TryParse(lengthStr, out var length)) continue;

                    await reader.ReadLineAsync().ConfigureAwait(false); // ligne vide

                    var buffer = new char[length];
                    var read = 0;
                    while (read < length)
                    {
                        var chunk = await reader.ReadAsync(buffer, read, length - read).ConfigureAwait(false);
                        if (chunk == 0) break;
                        read += chunk;
                    }

                    var json = new string(buffer, 0, read);
                    HandleDapMessage(json);
                }
            }
            catch { }
        }

        private async Task ReadErrorLoopAsync(CancellationToken ct)
        {
            if (_process == null) return;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    OutputReceived?.Invoke($"[stderr] {line}");
                }
            }
            catch { }
        }

        private void HandleDapMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var type)) return;
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
                    var eventName = evt.GetString();
                    switch (eventName)
                    {
                        case "stopped":
                            if (root.TryGetProperty("body", out var stoppedBody))
                            {
                                var threadId = stoppedBody.TryGetProperty("threadId", out var tid) ? tid.GetInt32() : 1;
                                var reason = stoppedBody.TryGetProperty("reason", out var r) ? r.GetString() : "";
                                Stopped?.Invoke(threadId, reason ?? "", 0);
                            }
                            break;
                        case "terminated":
                        case "exited":
                            Terminated?.Invoke();
                            break;
                        case "output":
                            if (root.TryGetProperty("body", out var outBody) &&
                                outBody.TryGetProperty("output", out var output))
                            {
                                OutputReceived?.Invoke(output.GetString() ?? "");
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DAP] Erreur de parsing du message.");
            }
        }

        private async Task<JsonElement> SendRequestAsync(string command, object args, CancellationToken ct = default)
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
            await _stdin.WriteAsync(header).ConfigureAwait(false);
            await _stdin.WriteAsync(json).ConfigureAwait(false);
            await _stdin.FlushAsync().ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task.ConfigureAwait(false);
        }

        private static string? FindNetcoredbg()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".dotnet", "tools", "netcoredbg"),
                "netcoredbg" // dans le PATH
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            // Vérifier dans le PATH
            try
            {
                var psi = new ProcessStartInfo("netcoredbg", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
                if (proc?.ExitCode == 0) return "netcoredbg";
            }
            catch { }

            return null;
        }

        public async ValueTask DisposeAsync()
        {
            _readerCts?.Cancel();
            if (_process != null && !_process.HasExited)
            {
                try { _process.Kill(); } catch { }
            }
            _process?.Dispose();
            _readerCts?.Dispose();
            await Task.CompletedTask;
        }
    }
}
