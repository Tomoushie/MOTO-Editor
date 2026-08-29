// AI/MotoAutocompleteEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Moto.Editor.AI
{
    public class AutocompleteItem
    {
        public string DisplayText { get; set; } = string.Empty;
        public string InsertText { get; set; } = string.Empty;
        public string Kind { get; set; } = "keyword";
    }

    /// <summary>
    /// Moteur d'autocomplétion local.
    /// Complétion légère par mots-clés et contexte de fichier.
    /// </summary>
    public class MotoAutocompleteEngine
    {
        private static readonly string[] CSharpKeywords =
        {
            "public", "private", "protected", "internal", "static", "void",
            "string", "int", "bool", "double", "decimal", "class", "interface",
            "namespace", "using", "return", "if", "else", "for", "foreach",
            "while", "switch", "case", "break", "continue", "new", "var",
            "async", "await", "Task", "null", "true", "false", "this", "base"
        };

        private static readonly string[] PythonKeywords =
        {
            "def", "class", "return", "if", "elif", "else", "for", "while",
            "import", "from", "as", "with", "try", "except", "finally",
            "None", "True", "False", "lambda", "pass", "break", "continue"
        };

        private static readonly string[] JavaScriptKeywords =
        {
            "const", "let", "var", "function", "return", "if", "else",
            "for", "while", "switch", "case", "break", "continue", "new",
            "class", "extends", "import", "export", "from", "async", "await",
            "null", "true", "false", "this"
        };

        /// <summary>
        /// Retourne les complétions possibles pour la ligne courante.
        /// </summary>
        public IReadOnlyList<AutocompleteItem> GetCompletions(string filePath, string line, int caretIndex)
        {
            var word = GetWordBeforeCaret(line, caretIndex);

            if (word.Length < 2)
            {
                return Array.Empty<AutocompleteItem>();
            }

            var extension = Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();

            var keywords = extension switch
            {
                ".cs" => CSharpKeywords,
                ".py" => PythonKeywords,
                ".js" or ".ts" or ".jsx" or ".tsx" => JavaScriptKeywords,
                _ => CSharpKeywords
            };

            return keywords
                .Where(keyword => keyword.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                .Select(keyword => new AutocompleteItem
                {
                    DisplayText = keyword,
                    InsertText = keyword,
                    Kind = "keyword"
                })
                .ToList();
        }

        private string GetWordBeforeCaret(string line, int caretIndex)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            caretIndex = Math.Min(caretIndex, line.Length);

            int start = caretIndex;

            while (start > 0 && char.IsLetterOrDigit(line[start - 1]))
            {
                start--;
            }

            return line.Substring(start, caretIndex - start);
        }
    }
}
