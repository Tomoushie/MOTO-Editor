// Services/TerminalService.cs
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// ★ AJOUT (01/10, famille « Terminal », clés <c>terminal_shell</c>,
    /// <c>terminal_env_vars</c>, <c>terminal_detect_venv</c>) : options de
    /// démarrage du shell INTERACTIF (<see cref="TerminalService.Start"/>).
    ///
    /// Le service reste volontairement ignorant des réglages (il vit dans
    /// Moto.Core et ne connaît pas <c>SettingsEngine</c>/le catalogue) : c'est
    /// l'appelant côté éditeur (<c>MainViewModel.StartTerminal</c>) qui résout
    /// les clés via <c>Moto.Editor.Settings.TerminalSettings</c> et passe les
    /// valeurs ici. Tous les champs sont facultatifs — ne rien passer =
    /// comportement historique inchangé (cmd.exe sur Windows, /bin/bash ailleurs,
    /// aucune variable ajoutée, aucune commande initiale).
    /// </summary>
    public sealed class TerminalStartOptions
    {
        /// <summary>Programme du shell (ex. <c>powershell.exe</c>, <c>bash.exe</c>). Null/vide = choix système du service.</summary>
        public string? ShellFileName { get; init; }

        /// <summary>Arguments du shell (ex. <c>-i</c> pour bash : sans lui, bash lit stdin sans aucun prompt — vérifié le 01/10). Null/vide = aucun.</summary>
        public string? ShellArguments { get; init; }

        /// <summary>Variables d'environnement ajoutées au process (source : <c>terminal_env_vars</c>, JSON clé-valeur).</summary>
        public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }

        /// <summary>Commande envoyée au shell juste après son démarrage (source : <c>terminal_detect_venv</c> — activation d'un venv Python).</summary>
        public string? InitialCommand { get; init; }
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

                var fallback = TerminalOutputDecoder.GetConsoleEncoding();
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
        /// ★ AJOUT (01/10) : répertoire RÉEL du shell interactif, renseigné
        /// uniquement quand <see cref="Start"/> a vraiment démarré un process
        /// (un Start ignoré parce qu'un shell tourne déjà laisse la valeur
        /// précédente : c'est la vérité, le shell n'a pas bougé). Sert au titre
        /// en breadcrumbs du dock (<c>terminal_breadcrumbs</c>).
        /// </summary>
        public string? CurrentWorkingDirectory { get; private set; }

        /// <summary>
        /// Démarre cmd.exe sur Windows, bash sinon.
        /// </summary>
        /// <param name="workingDirectory">Répertoire de départ (profil utilisateur si vide).</param>
        /// <param name="options">
        /// ★ AJOUT (01/10) : shell/variables/commande initiale décidés par
        /// l'appelant à partir des réglages — null = comportement historique.
        /// </param>
        public void Start(string? workingDirectory = null, TerminalStartOptions? options = null)
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
                var arguments = string.Empty;
                if (!string.IsNullOrWhiteSpace(options?.ShellFileName))
                {
                    shell = options.ShellFileName;
                }
                if (!string.IsNullOrWhiteSpace(options?.ShellArguments))
                {
                    arguments = options.ShellArguments;
                }
                var encoding = TerminalOutputDecoder.GetConsoleEncoding();

                var startDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : workingDirectory;

                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    WorkingDirectory = startDirectory,
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
                if (!string.IsNullOrEmpty(arguments))
                {
                    psi.Arguments = arguments;
                }
                if (options?.EnvironmentVariables != null)
                {
                    foreach (var pair in options.EnvironmentVariables)
                    {
                        if (!string.IsNullOrEmpty(pair.Key))
                        {
                            psi.Environment[pair.Key] = pair.Value ?? string.Empty;
                        }
                    }
                }
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
                // ★ AJOUT (01/10) : la vérité pour les breadcrumbs = le process
                // réellement démarré (si un catch plus bas échoue, la valeur reste
                // celle du dernier démarrage réussi).
                CurrentWorkingDirectory = startDirectory;
                // ★ CORRECTIF (26/09) : lecture des octets bruts à la place de
                // BeginOutputReadLine/BeginErrorReadLine, qui décodtaient tout le flux avec
                // UN seul encodage — voir TerminalOutputDecoder.
                _ = TerminalOutputDecoder.PumpLinesAsync(_process.StandardOutput.BaseStream, encoding, line => Emit(line, isError: false));
                _ = TerminalOutputDecoder.PumpLinesAsync(_process.StandardError.BaseStream, encoding, line => Emit(line, isError: true));

                OutputReceived?.Invoke($"[terminal] started {shell}", false);

                // ★ AJOUT (01/10) : commande initiale (activation venv) envoyée
                // APRÈS la mise en place des pompes de lecture, pour que la sortie de
                // l'activation remonte normalement. Le pipe tamponne : le shell la
                // exécute dès qu'il atteint son invite.
                if (!string.IsNullOrWhiteSpace(options?.InitialCommand))
                {
                    SendInput(options.InitialCommand);
                }
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"[terminal] start error: {ex.Message}", true);
            }
        }

        /// <summary>Publie une ligne du shell, sauf si elle est vide (même contrat qu'avant).</summary>
        private void Emit(string line, bool isError)
        {
            if (!string.IsNullOrEmpty(line))
                OutputReceived?.Invoke(line, isError);
        }

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
