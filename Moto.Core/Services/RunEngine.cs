// Moto.Core/Services/RunEngine.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Moto.Core.Services
{
    /// <summary>
    /// Lance le projet compilé (bouton Play) via dotnet run.
    /// </summary>
    public class RunEngine
    {
        private Process _process;

        public event Action<string> OutputReceived;
        public event Action Exited;

        public bool IsRunning => _process != null && !_process.HasExited;

        public void Run(string projectPath)
        {
            if (IsRunning)
            {
                return;
            }

            var target = Directory.GetFiles(projectPath, "*.csproj").FirstOrDefault()
                         ?? Directory.GetFiles(projectPath, "*.sln").FirstOrDefault();

            if (target == null)
            {
                OutputReceived?.Invoke("[run] Aucun projet exécutable trouvé.");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{target}\"",
                WorkingDirectory = projectPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data);
            };

            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke("[err] " + e.Data);
            };

            _process.Exited += (s, e) => Exited?.Invoke();

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            OutputReceived?.Invoke($"[run] Démarrage de {Path.GetFileName(target)}...");
        }

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
            catch
            {
                // Silencieux.
            }
        }
    }
}
