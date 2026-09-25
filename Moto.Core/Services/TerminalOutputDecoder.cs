// Services/TerminalOutputDecoder.cs
using System.Collections.Generic;
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

        private string Decode()
        {
            var bytes = _pending.ToArray();
            _pending.Clear();
            return Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : _fallback.GetString(bytes);
        }
    }
}
