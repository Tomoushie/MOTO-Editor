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
                CreateNoWindow = true
            };
            UseUtf8ForPython(psi);

            using var process = new Process { StartInfo = psi };

            try
            {
                process.Start();
                // ★ CORRECTIF (26/09, option C choisie par Tom) : remplace l'UTF-8 imposé le
                // 03/09 (réveil de GitPanelView : « ChaÃ®ne » au lieu de « Chaîne »). Cet UTF-8
                // réparait git, mais les propres messages de cmd.exe, écrits dans la page OEM
                // de sa console (850), ressortaient abîmés (« ex�cutable »). Octets bruts
                // décodés ligne par ligne (TerminalOutputDecoder.DecodeAll) : la sortie de git
                // reste identique, celle de cmd devient lisible.
                var stdOutTask = ReadAllBytesAsync(process.StandardOutput.BaseStream);
                var stdErrTask = ReadAllBytesAsync(process.StandardError.BaseStream);

                try
                {
                    await process.WaitForExitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* déjà terminé, ou pas assez de droits — sans conséquence ici */ }
                    return new TerminalCommandResult { ExitCode = -1, Output = string.Empty, Error = "Commande annulée (délai dépassé ou arrêt demandé)." };
                }

                var fallback = GetShellEncoding();
                return new TerminalCommandResult
                {
                    ExitCode = process.ExitCode,
                    Output = TerminalOutputDecoder.DecodeAll(await stdOutTask, fallback),
                    Error = TerminalOutputDecoder.DecodeAll(await stdErrTask, fallback),
                };
            }
            catch (Exception ex)
            {
                return new TerminalCommandResult { ExitCode = -1, Output = string.Empty, Error = ex.Message };
            }
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer).ConfigureAwait(false);
            return buffer.ToArray();
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
                UseUtf8ForPython(psi);

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
        /// ★ CORRECTIF (26/09, trouvé en vérifiant l'option B) : remplacement par « ? » imposé.
        /// Par défaut, .NET remplace un symbole absent de la page OEM par un « approchant » :
        /// « → » devenait Ctrl+Z (0x1A), que cmd lit comme une fin de saisie, « ♪ » Entrée (0x0D),
        /// « ◙ » un saut de ligne, « ♥ » Ctrl+C. Mesuré le 26/09 : « echo a→b♪c◙d » affichait « a »
        /// puis faisait EXÉCUTER « d » comme une commande à part.
        /// </summary>
        private static Encoding GetShellEncoding()
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            if (!OperatingSystem.IsWindows())
                return utf8;
            return CodePagesEncodingProvider.Instance.GetEncoding(
                       (int)GetOEMCP(), EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback)
                   ?? utf8;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        /// <summary>
        /// ★ AJOUT (26/09, option B choisie par Tom) : Python écrit dans un tuyau (pipe) avec la
        /// page ANSI (1252) — ses accents ressortaient faux et un emoji faisait planter le script
        /// (UnicodeEncodeError, mesuré le 26/09). Forcé en UTF-8, que les deux décodages savent lire.
        /// « :replace » : ce qu'on TAPE pour un programme Python (input) part en page OEM, invalide
        /// en UTF-8 — remplacé par « � » plutôt que de faire planter le script (il le recevait déjà
        /// faux avant : « ‚t‚ » pour « été »). Windows seulement ; un réglage Python déjà présent
        /// (PYTHONIOENCODING ou PYTHONUTF8) est respecté.
        /// </summary>
        private static void UseUtf8ForPython(ProcessStartInfo psi)
        {
            if (!OperatingSystem.IsWindows()
                || psi.Environment.ContainsKey("PYTHONIOENCODING")
                || psi.Environment.ContainsKey("PYTHONUTF8"))
                return;
            psi.Environment["PYTHONIOENCODING"] = "utf-8:replace";
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
