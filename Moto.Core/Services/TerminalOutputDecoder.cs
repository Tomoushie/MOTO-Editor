// Services/TerminalOutputDecoder.cs
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace Moto.Editor.Services
{
    /// <summary>
    /// ★ AJOUT (26/09, accents du Terminal — « Tous droits r,serv,s. » sur la capture de Tom) :
    /// découpe la sortie brute (octets) du shell en lignes, puis décode CHAQUE ligne à part :
    /// en UTF-8 si ses octets forment de l'UTF-8 valide (git, dotnet/MSBuild écrivent en UTF-8),
    /// sinon avec l'encodage de repli (cmd.exe écrit ses propres messages dans la page OEM de la
    /// console — 850 sur un Windows français, où « é » est l'octet 0x82, jamais valide seul en UTF-8).
    /// Un encodage unique pour tout le flux ne convient pas — mesuré le 26/09 hors de l'appli :
    /// OEM seul abîme « git log » et « dotnet build » (« fen├¬tre »), UTF-8 seul abîme la bannière
    /// et les messages de cmd.
    /// Décoder une ligne COMPLÈTE recolle aussi un caractère UTF-8 coupé entre deux lectures (0xC3 | 0xA9).
    /// Fins de ligne : \n, \r ou \r\n — même règle que Process.BeginOutputReadLine, que ce décodeur
    /// remplace dans TerminalService.Start() — y compris un \r\n coupé entre deux lectures.
    /// </summary>
    public sealed class TerminalOutputDecoder
    {
        private readonly Encoding _fallback;
        private readonly List<byte> _pending = new();
        private bool _skipLineFeed;

        public TerminalOutputDecoder(Encoding fallback)
        {
            _fallback = fallback;
        }

        /// <summary>
        /// Encodage de la console des programmes lancés en arrière-plan : page OEM du système sous
        /// Windows (celle de la console de cmd.exe — GetOEMCP plutôt que la culture de l'appli, qui
        /// peut différer du réglage système), UTF-8 sans BOM ailleurs (bash). Sert d'encodage de
        /// repli au décodage, et d'encodage de ce qu'on tape dans le terminal. Le fournisseur de
        /// pages de code est interrogé directement, sans enregistrement global : rien ne change
        /// pour le reste de l'appli.
        /// ★ CORRECTIF (26/09, trouvé en vérifiant l'option B) : remplacement par « ? » imposé.
        /// Par défaut, .NET remplace un symbole absent de la page OEM par un « approchant » :
        /// « → » devenait Ctrl+Z (0x1A), que cmd lit comme une fin de saisie, « ♪ » Entrée (0x0D),
        /// « ◙ » un saut de ligne, « ♥ » Ctrl+C. Mesuré le 26/09 : « echo a→b♪c◙d » affichait « a »
        /// puis faisait EXÉCUTER « d » comme une commande à part.
        /// (Déplacé de TerminalService le 26/09, option D : Build et Exécuter s'en servent aussi.)
        /// </summary>
        public static Encoding GetConsoleEncoding()
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
        /// Lit un flux (sortie d'un programme lancé) jusqu'à sa fin et passe chaque ligne décodée à
        /// <paramref name="onLine"/>, lignes vides comprises. Remplace Process.BeginOutputReadLine,
        /// qui décodait tout le flux avec UN seul encodage. La tâche se termine quand le flux est
        /// fermé — à attendre avant de conclure si toute la sortie compte (BuildEngine).
        /// (Déplacé de TerminalService le 26/09, option D.)
        /// </summary>
        public static async Task PumpLinesAsync(Stream stream, Encoding fallback, Action<string> onLine)
        {
            var decoder = new TerminalOutputDecoder(fallback);
            var buffer = new byte[4096];
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    foreach (var line in decoder.Push(buffer, read))
                        onLine(line);
                }
                var last = decoder.Flush();
                if (last != null)
                    onLine(last);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                // Programme tué et libéré pendant une lecture (bouton Stop, arrêt du terminal) : fin normale du flux.
            }
        }

        /// <summary>
        /// Ajoute les <paramref name="count"/> premiers octets de <paramref name="buffer"/> et renvoie
        /// les lignes désormais complètes (lignes vides comprises — à l'appelant de les filtrer).
        /// </summary>
        public IReadOnlyList<string> Push(byte[] buffer, int count)
        {
            var lines = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var b = buffer[i];
                if (_skipLineFeed)
                {
                    _skipLineFeed = false;
                    if (b == (byte)'\n')
                        continue; // second octet d'un \r\n : la ligne est déjà partie sur le \r
                }

                if (b == (byte)'\r' || b == (byte)'\n')
                {
                    lines.Add(Decode());
                    _skipLineFeed = b == (byte)'\r';
                }
                else
                {
                    _pending.Add(b);
                }
            }
            return lines;
        }

        /// <summary>Fin du flux : renvoie la dernière ligne restée sans fin de ligne, ou null s'il n'y en a pas.</summary>
        public string? Flush() => _pending.Count == 0 ? null : Decode();

        /// <summary>
        /// ★ AJOUT (26/09, option C choisie par Tom — commandes « en coulisses » : panneau Git, agents) :
        /// décode d'un bloc la sortie complète d'une commande (TerminalService.ExecuteAsync), même règle
        /// par ligne que Push, mais fins de ligne gardées telles quelles — GitService et les agents
        /// analysent ce texte. Une sortie entièrement UTF-8 (git) donne exactement le texte d'avant.
        /// Sortie marquée d'un BOM (l'UTF-16 de « wmic », par ex.) : lue comme avant par StreamReader,
        /// qui reconnaît ces marques.
        /// </summary>
        public static string DecodeAll(byte[] bytes, Encoding fallback)
        {
            if (StartsWithByteOrderMark(bytes))
            {
                using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }

            var text = new StringBuilder(bytes.Length);
            var start = 0;
            while (start < bytes.Length)
            {
                var end = Array.IndexOf(bytes, (byte)'\n', start);
                end = end < 0 ? bytes.Length : end + 1; // le \n (et un \r avant lui) reste dans sa ligne
                text.Append(DecodeLine(bytes.AsSpan(start, end - start), fallback));
                start = end;
            }
            return text.ToString();
        }

        private static bool StartsWithByteOrderMark(byte[] b) =>
            (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)                  // UTF-8
            || (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF))) // UTF-16 (et UTF-32 LE)
            || (b.Length >= 4 && b[0] == 0 && b[1] == 0 && b[2] == 0xFE && b[3] == 0xFF);    // UTF-32 BE

        private string Decode()
        {
            var bytes = _pending.ToArray();
            _pending.Clear();
            return DecodeLine(bytes, _fallback);
        }

        private static string DecodeLine(ReadOnlySpan<byte> bytes, Encoding fallback) =>
            Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : fallback.GetString(bytes);
    }
}
