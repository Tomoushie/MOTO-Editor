// Syntax/SyntaxEngine.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Color = System.Drawing.Color;

namespace Moto.Editor.Syntax
{
    /// <summary>
    /// Type de token produit par le moteur syntaxique.
    /// </summary>
    public enum TokenKind
    {
        Text,
        Keyword,
        Comment,
        String,
        Number
    }

    /// <summary>
    /// Token syntaxique.
    /// </summary>
    public readonly struct SyntaxToken
    {
        public int Start { get; }
        public int Length { get; }
        public TokenKind Kind { get; }

        public SyntaxToken(int start, int length, TokenKind kind)
        {
            Start = start;
            Length = length;
            Kind = kind;
        }
    }

    /// <summary>
    /// Contrat pour un langage supporté par MOTO Syntax Engine.
    /// </summary>
    public interface ISyntaxLanguage
    {
        string Extension { get; }
        IEnumerable<SyntaxToken> Tokenize(string text);
    }

    /// <summary>
    /// Coloration C# maison.
    /// Volontairement légère, sans dépendance externe.
    /// </summary>
    public class CSharpSyntaxLanguage : ISyntaxLanguage
    {
        public string Extension => ".cs";

        private static readonly Regex CommentRegex = new Regex(
            @"(//.*?$|/\\*.*?\\*/)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline
        );

        private static readonly Regex StringRegex = new Regex(
            @""".*?""",
            RegexOptions.Compiled
        );

        private static readonly Regex KeywordRegex = new Regex(
            @"\\b(public|private|protected|internal|static|void|string|int|bool|double|float|decimal|object|class|interface|enum|namespace|using|return|if|else|for|foreach|while|switch|case|break|continue|new|var|async|await|null|true|false|this|base|record|init|required)\\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex NumberRegex = new Regex(
            @"\\b\\d+\\b",
            RegexOptions.Compiled
        );

        public IEnumerable<SyntaxToken> Tokenize(string text)
        {
            var tokens = new List<SyntaxToken>();
            var occupied = new List<(int Start, int End)>();

            AddMatches(tokens, occupied, text, CommentRegex, TokenKind.Comment);
            AddMatches(tokens, occupied, text, StringRegex, TokenKind.String);
            AddMatches(tokens, occupied, text, KeywordRegex, TokenKind.Keyword);
            AddMatches(tokens, occupied, text, NumberRegex, TokenKind.Number);

            return tokens;
        }

        private void AddMatches(
            List<SyntaxToken> tokens,
            List<(int Start, int End)> occupied,
            string text,
            Regex regex,
            TokenKind kind)
        {
            foreach (Match match in regex.Matches(text))
            {
                if (match.Length == 0)
                {
                    continue;
                }

                int start = match.Index;
                int end = start + match.Length;

                if (IsFree(occupied, start, end))
                {
                    tokens.Add(new SyntaxToken(start, match.Length, kind));
                    occupied.Add((start, end));
                }
            }
        }

        private bool IsFree(List<(int Start, int End)> occupied, int start, int end)
        {
            foreach (var range in occupied)
            {
                if (start < range.End && range.Start < end)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Moteur de coloration syntaxique de MOTO Editor.
    /// </summary>
    public class SyntaxEngine
    {
        private readonly Dictionary<string, ISyntaxLanguage> _languages =
            new Dictionary<string, ISyntaxLanguage>(StringComparer.OrdinalIgnoreCase);

        public SyntaxEngine()
        {
            Register(new CSharpSyntaxLanguage());
        }

        /// <summary>
        /// Enregistre un langage.
        /// </summary>
        public void Register(ISyntaxLanguage language)
        {
            _languages[language.Extension] = language;
        }

        /// <summary>
        /// Applique la coloration syntaxique à un RichTextBox.
        /// </summary>
        public void Colorize(RichTextBox box, string filePath)
        {
            if (box == null)
            {
                return;
            }

            var extension = Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();

            if (!_languages.TryGetValue(extension, out var language))
            {
                return;
            }

            int selectionStart = box.SelectionStart;
            int selectionLength = box.SelectionLength;

            box.SuspendLayout();

            box.SelectAll();
            box.SelectionColor = Color.FromArgb(220, 222, 226);

            var tokens = language.Tokenize(box.Text);

            foreach (var token in tokens)
            {
                box.Select(token.Start, token.Length);
                box.SelectionColor = GetColor(token.Kind);
            }

            box.Select(selectionStart, selectionLength);
            box.ResumeLayout();
        }

        private Color GetColor(TokenKind kind)
        {
            return kind switch
            {
                TokenKind.Keyword => Color.FromArgb(86, 156, 214),
                TokenKind.Comment => Color.FromArgb(87, 166, 74),
                TokenKind.String => Color.FromArgb(214, 157, 133),
                TokenKind.Number => Color.FromArgb(181, 206, 168),
                _ => Color.FromArgb(220, 222, 226)
            };
        }
    }
}
