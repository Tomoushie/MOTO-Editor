// Terminal/TerminalHost.cs
using System;
using System.Diagnostics;

namespace Moto.Editor.Terminal
{
    /// <summary>
    /// Terminal intégré à MOTO Editor.
    /// Il lance un shell local et redirige entrée/sortie vers l'UI.
    /// </summary>
    public class TerminalHost
    {
        private Process _process;
        private readonly Action<string> _output;

        /// <summary>
        /// Indique si le shell est en cours d'exécution.
        /// </summary>
        public bool IsRunning => _process != null && !_process.HasExited;

        public TerminalHost(Action<string> output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// Démarre le shell intégré.
        /// cmd.exe sur Windows, bash ailleurs.
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            var shell = OperatingSystem.IsWindows()
                ? "cmd.exe"
                : "/bin/bash";

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _output(e.Data);
                }
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _output($"[stderr] {e.Data}");
                }
            };

            _process.Exited += (s, e) =>
            {
                _output("[terminal] shell exited.");
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _output($"[terminal] started {shell}");
        }

        /// <summary>
        /// Envoie une commande au shell.
        /// </summary>
        public void SendInput(string line)
        {
            if (!IsRunning)
            {
                return;
            }

            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }

        /// <summary>
        /// Arrête proprement le shell.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                _process?.Dispose();
                _process = null;
            }
            catch (Exception ex)
            {
                _output($"[terminal] stop error: {ex.Message}");
            }
        }
    }
}
