// Moto.Core/Services/RunEngine.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Moto.Editor.Services; // TerminalOutputDecoder (namespace historique)

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

            _process.Exited += (s, e) => Exited?.Invoke();

            _process.Start();
            // ★ CORRECTIF (26/09, option D choisie par Tom) : sortie lue ligne par ligne comme
            // dans le terminal (voir TerminalOutputDecoder) à la place de BeginOutputReadLine,
            // qui décodait tout avec la page de l'appli : les accents du programme lancé et les
            // messages de dotnet (UTF-8) ressortaient abîmés.
            var fallback = TerminalOutputDecoder.GetConsoleEncoding();
            _ = TerminalOutputDecoder.PumpLinesAsync(_process.StandardOutput.BaseStream, fallback, line => OutputReceived?.Invoke(line));
            _ = TerminalOutputDecoder.PumpLinesAsync(_process.StandardError.BaseStream, fallback, line => OutputReceived?.Invoke("[err] " + line));

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
