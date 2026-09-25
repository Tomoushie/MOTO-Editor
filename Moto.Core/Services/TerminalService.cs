// Services/TerminalService.cs
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
        public Task<TerminalCommandResult> ExecuteAsync(string command, string? workingDirectory = null)
            => ExecuteAsync(command, workingDirectory, CancellationToken.None);

        /// <summary>
        /// ★ AJOUT (03/09, fondation "agents autonomes en tâche de fond") : surcharge
        /// annulable. La version historique ci-dessus (déjà utilisée par GitService
        /// et consorts, comportement INCHANGÉ — elle délègue ici avec
        /// CancellationToken.None) n'acceptait aucun jeton d'annulation : une
        /// commande qui ne se termine jamais (ex. un programme qui attend une
        /// entrée) bloquerait indéfiniment n'importe quel appelant. Nécessaire pour
        /// qu'un futur agent autonome (RunCommandTool/BackgroundAgentLoop) puisse
        /// imposer sa propre garde "durée max" — sans ça, cette garde ne pourrait
        /// jamais interrompre une commande déjà en cours. Le process (arbre complet)
        /// est tué si le jeton est annulé, plutôt que laissé orphelin en arrière-plan.
        /// </summary>
        public async Task<TerminalCommandResult> ExecuteAsync(string command, string? workingDirectory, CancellationToken ct)
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
                CreateNoWindow = true,
                // ★ CORRECTIF (03/09, réveil de GitPanelView, trouvé par Tom) : sans
                // encodage explicite, .NET décode la sortie redirigée avec la page de
                // code OEM/ANSI du système (ex. CP1252 en français) — les octets UTF-8
                // réels de git (accents, ex. "Chaîne") ressortaient en charabia
                // ("ChaÃ®ne"). N'affecte QUE cette méthode one-shot (GitService et
                // consorts), pas Start() plus bas (terminal interactif, où cmd.exe émet
                // ses propres bannières en page de code OEM — il a son propre décodage
                // ligne par ligne, voir TerminalOutputDecoder).
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };

            try
            {
                process.Start();
                var stdOutTask = process.StandardOutput.ReadToEndAsync();
                var stdErrTask = process.StandardError.ReadToEndAsync();

                try
                {
                    await process.WaitForExitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* déjà terminé, ou pas assez de droits — sans conséquence ici */ }
                    return new TerminalCommandResult { ExitCode = -1, Output = string.Empty, Error = "Commande annulée (délai dépassé ou arrêt demandé)." };
                }

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
                var encoding = GetShellEncoding();

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
                    CreateNoWindow = true,
                    // ★ CORRECTIF (26/09, accents du Terminal) : sans encodage explicite, .NET
                    // parlait au shell dans la page ANSI (1252) alors que cmd.exe lit et écrit
                    // dans la page OEM de sa console (850) — « réservés » devenait « r,serv,s. »
                    // à l'écran, et un « é » tapé arrivait à cmd en « Ú » (cd Vidéos échouait).
                    // cmd lit son entrée octet par octet : il lui faut une page à un octet par
                    // caractère (OEM) — « chcp 65001 » + UTF-8, essayé le 26/09, rend chaque
                    // accent tapé illisible pour cmd (et supprime sa bannière).
                    StandardInputEncoding = encoding
                };

                _process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                _process.Exited += (s, e) =>
                {
                    OutputReceived?.Invoke("[terminal] shell exited.", false);
                };

                _process.Start();
                // ★ CORRECTIF (26/09) : lecture des octets bruts à la place de
                // BeginOutputReadLine/BeginErrorReadLine, qui décodaient tout le flux avec
                // UN seul encodage — voir TerminalOutputDecoder.
                _ = PumpAsync(_process.StandardOutput.BaseStream, encoding, isError: false);
                _ = PumpAsync(_process.StandardError.BaseStream, encoding, isError: true);

                OutputReceived?.Invoke($"[terminal] started {shell}", false);
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"[terminal] start error: {ex.Message}", true);
            }
        }

        /// <summary>
        /// Lit un flux du shell jusqu'à sa fin et publie chaque ligne non vide
        /// (même contrat qu'avant : les lignes vides ne sont pas publiées).
        /// </summary>
        private async Task PumpAsync(Stream stream, Encoding fallback, bool isError)
        {
            var decoder = new TerminalOutputDecoder(fallback);
            var buffer = new byte[4096];
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    foreach (var line in decoder.Push(buffer, read))
                        Emit(line, isError);
                }
                Emit(decoder.Flush(), isError);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                // Stop() a tué et libéré le shell pendant une lecture : fin normale du flux.
            }
        }

        private void Emit(string? line, bool isError)
        {
            if (!string.IsNullOrEmpty(line))
                OutputReceived?.Invoke(line, isError);
        }

        /// <summary>
        /// Encodage des échanges avec le shell interactif : page OEM du système sous Windows
        /// (celle de la console de cmd.exe — GetOEMCP plutôt que la culture de l'appli, qui
        /// peut différer du réglage système), UTF-8 sans BOM ailleurs (bash). Le fournisseur
        /// de pages de code est interrogé directement, sans enregistrement global : rien ne
        /// change pour le reste de l'appli.
        /// </summary>
        private static Encoding GetShellEncoding()
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            if (!OperatingSystem.IsWindows())
                return utf8;
            return CodePagesEncodingProvider.Instance.GetEncoding((int)GetOEMCP()) ?? utf8;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

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
