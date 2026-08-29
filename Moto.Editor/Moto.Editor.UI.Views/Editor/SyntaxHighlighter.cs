// UI/Editor/SyntaxHighlighter.cs
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Moto.Editor.UI.Editor
{
    /// <summary>
    /// Coloration syntaxique légère, sans dépendance externe.
    /// Version volontairement simple : regex par langage.
    /// Pour une version production, passer à un parser incrémental.
    /// </summary>
    public class SyntaxHighlighter
    {
        private static readonly Regex CSharpKeywordRegex = new Regex(
            @"\\b(public|private|protected|internal|static|void|string|int|bool|double|float|decimal|object|class|interface|enum|namespace|using|return|if|else|for|foreach|while|switch|case|break|continue|new|var|async|await|null|true|false|this|base|sealed|virtual|override|readonly|const|record|init|required)\\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex CommentRegex = new Regex(
            @"(//.*?$|/\\*.*?\\*/)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline
        );

        private static readonly Regex StringRegex = new Regex(
            "\"(?:[^\"\\\\]|\\\\.)*\"",
            RegexOptions.Compiled
        );

        public void Apply(RichTextBox box, string filePath)
        {
            if (box == null)
            {
                return;
            }

            box.SuspendLayout();

            int selectionStart = box.SelectionStart;
            int selectionLength = box.SelectionLength;

            box.SelectAll();
            box.SelectionColor = Color.FromArgb(220, 222, 226);

            var extension = Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();

            if (extension == ".cs" || extension == ".js" || extension == ".ts" || extension == ".py" || extension == ".json")
            {
                HighlightMatches(box, CommentRegex, Color.FromArgb(87, 166, 74));
                HighlightMatches(box, StringRegex, Color.FromArgb(214, 157, 133));
            }

            if (extension == ".cs")
            {
                HighlightMatches(box, CSharpKeywordRegex, Color.FromArgb(86, 156, 214));
            }

            box.SelectionStart = selectionStart;
            box.SelectionLength = selectionLength;

            box.ResumeLayout();
        }

        private void HighlightMatches(RichTextBox box, Regex regex, Color color)
        {
            foreach (Match match in regex.Matches(box.Text))
            {
                box.Select(match.Index, match.Length);
                box.SelectionColor = color;
            }
        }
    }
}
