// Moto.Core/Performance/IncrementalHighlighter.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Moto.Core.Performance
{
    /// <summary>Token produit par ligne, prêt pour un futur renderer riche.</summary>
    public readonly struct HighlightToken
    {
        public int Line { get; }
        public int Start { get; }
        public int Length { get; }
        public string Kind { get; } // keyword | comment | string | number

        public HighlightToken(int line, int start, int length, string kind)
        {
            Line = line;
            Start = start;
            Length = length;
            Kind = kind;
        }
    }

    /// <summary>
    /// 19. Syntax Highlighting incrémental.
    /// Au lieu de recolorer tout le fichier à chaque frappe :
    /// - détecte la plage de lignes modifiées (préfixe/suffixe commun) ;
    /// - étend la plage si un bloc multi-lignes (/* ... */) est ouvert ;
    /// - re-tokenise UNIQUEMENT cette plage ;
    /// - cache les tokens par ligne ;
    /// - peut tokeniser seulement la plage visible ("fichier visible").
    /// </summary>
    public class IncrementalHighlighter
    {
        private readonly Dictionary<int, List<HighlightToken>> _lineCache = new();
        private string[] _lines = Array.Empty<string>();

        private static readonly Regex KeywordRegex = new(
            @"\b(public|private|protected|internal|static|void|string|int|bool|double|float|decimal|object|class|interface|enum|namespace|using|return|if|else|for|foreach|while|switch|case|break|continue|new|var|async|await|null|true|false|this|base|record|init|required)\b",
            RegexOptions.Compiled);

        private static readonly Regex StringRegex = new(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
        private static readonly Regex CommentRegex = new(@"//.*$", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new(@"\b\d+(?:\.\d+)?\b", RegexOptions.Compiled);

        /// <summary>Charge le texte initial (aucune tokenisation immédiate).</summary>
        public void SetText(string text)
        {
            _lines = Split(text);
            _lineCache.Clear();
        }

        /// <summary>
        /// Signale un changement de texte.
        /// Retourne la plage [start, end] re-tokenisée (pour le renderer).
        /// </summary>
        public (int start, int end) OnTextChanged(string newText)
        {
            var newLines = Split(newText);
            var (start, end) = ChangedRange(_lines, newLines);

            _lines = newLines;

            // Étend la plage si un bloc multi-lignes est ouvert.
            end = ExtendForOpenBlocks(start, end);
            end = Math.Min(end, _lines.Length - 1);

            // Invalide uniquement le cache de la plage modifiée.
            for (int i = start; i <= end; i++)
            {
                _lineCache.Remove(i);
            }

            return (start, end);
        }

        /// <summary>Tokens d'une ligne, tokenisée à la demande puis cachée.</summary>
        public IReadOnlyList<HighlightToken> GetTokens(int line)
        {
            if (line < 0 || line >= _lines.Length)
            {
                return Array.Empty<HighlightToken>();
            }

            if (_lineCache.TryGetValue(line, out var cached))
            {
                return cached;
            }

            var tokens = TokenizeLine(line);
            _lineCache[line] = tokens;

            return tokens;
        }

        /// <summary>
        /// Tokens de la plage VISIBLE uniquement.
        /// C'est le mode "fichier visible" : le reste n'est pas calculé.
        /// </summary>
        public IReadOnlyList<HighlightToken> GetVisibleTokens(int startLine, int endLine)
        {
            var result = new List<HighlightToken>();

            for (int i = Math.Max(0, startLine); i <= Math.Min(_lines.Length - 1, endLine); i++)
            {
                result.AddRange(GetTokens(i));
            }

            return result;
        }

        /// <summary>Détecte la plage modifiée via préfixe/suffixe commun.</summary>
        private static (int start, int end) ChangedRange(string[] old, string[] neu)
        {
            int start = 0;

            while (start < old.Length && start < neu.Length && old[start] == neu[start])
            {
                start++;
            }

            int oldEnd = old.Length;
            int newEnd = neu.Length;

            while (oldEnd > start && newEnd > start && old[oldEnd - 1] == neu[newEnd - 1])
            {
                oldEnd--;
                newEnd--;
            }

            return (start, Math.Max(start, newEnd - 1));
        }

        /// <summary>
        /// Étend la plage vers le bas tant qu'un commentaire /* ... */ reste ouvert.
        /// Évite de casser la coloration d'un bloc multi-lignes.
        /// </summary>
        private int ExtendForOpenBlocks(int start, int end)
        {
            int open = 0;

            for (int i = start; i < _lines.Length; i++)
            {
                var line = _lines[i];

                open += Count(line, "/*");
                open -= Count(line, "*/");

                if (i > end && open <= 0)
                {
                    return i;
                }

                if (i <= end && open > 0)
                {
                    // Le bloc dépasse la plage modifiée : on continue.
                    end = Math.Min(_lines.Length - 1, end + 1);
                }
            }

            return end;
        }

        private List<HighlightToken> TokenizeLine(int line)
        {
            var text = _lines[line];
            var tokens = new List<HighlightToken>();
            var occupied = new List<(int s, int e)>();

            Add(line, text, CommentRegex, "comment", tokens, occupied);
            Add(line, text, StringRegex, "string", tokens, occupied);
            Add(line, text, KeywordRegex, "keyword", tokens, occupied);
            Add(line, text, NumberRegex, "number", tokens, occupied);

            return tokens;
        }

        private void Add(
            int line, string text, Regex regex, string kind,
            List<HighlightToken> tokens, List<(int s, int e)> occupied)
        {
            foreach (Match m in regex.Matches(text))
            {
                int s = m.Index;
                int e = s + m.Length;

                bool free = true;

                foreach (var o in occupied)
                {
                    if (s < o.e && o.s < e)
                    {
                        free = false;
                        break;
                    }
                }

                if (free && m.Length > 0)
                {
                    tokens.Add(new HighlightToken(line, s, m.Length, kind));
                    occupied.Add((s, e));
                }
            }
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            int idx = 0;

            while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }

            return count;
        }

        private static string[] Split(string text)
        {
            return (text ?? string.Empty).Split('\n');
        }
    }
}
