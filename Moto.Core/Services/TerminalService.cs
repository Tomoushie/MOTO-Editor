// Services/TerminalService.cs
using System.Diagnostics;
using System.Threading.Tasks;

namespace Moto.Editor.Services
{
    /// <summary>Résultat d'une commande one-shot exécutée via <see cref="TerminalService.ExecuteAsync"/>.</summary>
    public sealed class TerminalCommandResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }

    /// <summary>
    /// Service terminal portable.
    /// La logique Process n'est pas liée à MAUI.
    /// L'affichage est ensuite consommé par TerminalView.
    /// </summary>
    public class TerminalService
    {
        private Process _process;

        /// <summary>
        /// Exécute une commande unique (one-shot, hors du shell interactif Start/Stop)
        /// et attend sa terminaison. Utilisé par GitService et consorts.
        /// </summary>
        public async Task<TerminalCommandResult> ExecuteAsync(string command, string? workingDirectory = null)
        {
            var shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
            var args = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"";

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };

            try
            {
                process.Start();
                var stdOutTask = process.StandardOutput.ReadToEndAsync();
                var stdErrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return new TerminalCommandResult
                {
                    ExitCode = process.ExitCode,
                    Output = await stdOutTask,
                    Error = await stdErrTask,
                };
            }
            catch (Exception ex)
            {
                return new TerminalCommandResult { ExitCode = -1, Output = string.Empty, Error = ex.Message };
            }
        }

        /// <summary>
        /// Déclenché quand une ligne sort du shell.
        /// Item1 = ligne, Item2 = erreur.
        /// </summary>
        public event Action<string, bool> OutputReceived;

        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>
        /// Démarre cmd.exe sur Windows, bash sinon.
        /// </summary>
        public void Start(string workingDirectory = null)
        {
            if (IsRunning)
            {
                return;
            }

            try
            {
                var shell = OperatingSystem.IsWindows()
                    ? "cmd.exe"
                    : "/bin/bash";

                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                        : workingDirectory,
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
                        OutputReceived?.Invoke(e.Data, false);
                    }
                };

                _process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OutputReceived?.Invoke(e.Data, true);
                    }
                };

                _process.Exited += (s, e) =>
                {
                    OutputReceived?.Invoke("[terminal] shell exited.", false);
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                OutputReceived?.Invoke($"[terminal] started {shell}", false);
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"[terminal] start error: {ex.Message}", true);
            }
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
        /// Arrête le shell.
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
            catch
            {
                // Silencieux : l'arrêt du terminal ne doit pas bloquer l'éditeur.
            }
        }
    }
}
