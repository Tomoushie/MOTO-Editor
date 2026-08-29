// Language/MotoLanguageEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Moto.Editor.Language
{
    /// <summary>
    /// Document texte suivi par le moteur de langage.
    /// </summary>
    public class TextDocument
    {
        public string Path { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Version { get; private set; }

        public void Update(string newText)
        {
            Text = newText;
            Version++;
        }
    }

    /// <summary>
    /// Nœud de parsing minimal.
    /// </summary>
    public class ParseNode
    {
        public string Kind { get; set; } = string.Empty;
        public int Start { get; set; }
        public int End { get; set; }
        public List<ParseNode> Children { get; } = new List<ParseNode>();
    }

    /// <summary>
    /// Parsing incrémental léger.
    /// Pour l'instant basé sur des regex structurelles.
    /// </summary>
    public class IncrementalParser
    {
        private static readonly Regex NamespaceRegex = new Regex(
            @"namespace\\s+([\\w\\.]+)",
            RegexOptions.Compiled
        );

        private static readonly Regex ClassRegex = new Regex(
            @"class\\s+(\\w+)",
            RegexOptions.Compiled
        );

        private static readonly Regex MethodRegex = new Regex(
            @"(?:public|private|protected|internal)\\s+[\\w<>\\[\\],\\s]+\\s+(\\w+)\\s*\\(",
            RegexOptions.Compiled
        );

        public ParseNode Parse(TextDocument document)
        {
            var root = new ParseNode
            {
                Kind = "root",
                Start = 0,
                End = document.Text.Length
            };

            AddMatches(root, document.Text, NamespaceRegex, "namespace");
            AddMatches(root, document.Text, ClassRegex, "class");
            AddMatches(root, document.Text, MethodRegex, "method");

            return root;
        }

        private void AddMatches(ParseNode root, string text, Regex regex, string kind)
        {
            foreach (Match match in regex.Matches(text))
            {
                root.Children.Add(new ParseNode
                {
                    Kind = kind,
                    Start = match.Index,
                    End = match.Index + match.Length
                });
            }
        }
    }

    /// <summary>
    /// Diagnostic local produit par le moteur de langage.
    /// </summary>
    public class LanguageDiagnostic
    {
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public int Start { get; set; }
        public int End { get; set; }
    }

    /// <summary>
    /// Diagnostics locaux rapides.
    /// </summary>
    public class DiagnosticEngine
    {
        public IReadOnlyList<LanguageDiagnostic> Analyze(TextDocument document, ParseNode root)
        {
            var diagnostics = new List<LanguageDiagnostic>();

            int openBraces = document.Text.Count(c => c == '{');
            int closeBraces = document.Text.Count(c => c == '}');

            if (openBraces != closeBraces)
            {
                diagnostics.Add(new LanguageDiagnostic
                {
                    Severity = "warning",
                    Message = "Braces appear unbalanced.",
                    Start = 0,
                    End = document.Text.Length
                });
            }

            if (document.Text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new LanguageDiagnostic
                {
                    Severity = "info",
                    Message = "TODO detected.",
                    Start = 0,
                    End = 0
                });
            }

            return diagnostics;
        }
    }

    /// <summary>
    /// Symbole indexé pour navigation et quick-open symbolique.
    /// </summary>
    public class SymbolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int Start { get; set; }
        public int End { get; set; }
    }

    /// <summary>
    /// Index symbolique local.
    /// </summary>
    public class SymbolIndex
    {
        private readonly List<SymbolInfo> _symbols = new List<SymbolInfo>();

        public void Index(TextDocument document, ParseNode root)
        {
            _symbols.RemoveAll(s => s.FilePath == document.Path);

            foreach (var node in root.Children)
            {
                int length = Math.Min(80, node.End - node.Start);

                if (length <= 0)
                {
                    continue;
                }

                var raw = document.Text.Substring(node.Start, length);

                _symbols.Add(new SymbolInfo
                {
                    Name = raw,
                    Kind = node.Kind,
                    FilePath = document.Path,
                    Start = node.Start,
                    End = node.End
                });
            }
        }

        public IEnumerable<SymbolInfo> Search(string query)
        {
            return _symbols
                .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(50);
        }

        public SymbolInfo FindDefinition(string name)
        {
            return _symbols.FirstOrDefault(s =>
                s.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
            );
        }
    }

    /// <summary>
    /// Auto-import léger.
    /// </summary>
    public class AutoImportEngine
    {
        public IEnumerable<string> SuggestMissingImports(TextDocument document)
        {
            if (document.Path != null &&
                document.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (document.Text.Contains("List<") &&
                    !document.Text.Contains("using System.Collections.Generic;"))
                {
                    yield return "using System.Collections.Generic;";
                }

                if (document.Text.Contains("HttpClient") &&
                    !document.Text.Contains("using System.Net.Http;"))
                {
                    yield return "using System.Net.Http;";
                }

                if (document.Text.Contains("JsonSerializer") &&
                    !document.Text.Contains("using System.Text.Json;"))
                {
                    yield return "using System.Text.Json;";
                }
            }
        }
    }

    /// <summary>
    /// Rapport produit par le moteur de langage.
    /// </summary>
    public class LanguageReport
    {
        public IReadOnlyList<LanguageDiagnostic> Diagnostics { get; set; } =
            Array.Empty<LanguageDiagnostic>();

        public IReadOnlyList<SymbolInfo> Symbols { get; set; } =
            Array.Empty<SymbolInfo>();

        public IReadOnlyList<string> MissingImports { get; set; } =
            Array.Empty<string>();
    }

    /// <summary>
    /// MOTO LSP : moteur de langage maison.
    /// </summary>
    public class MotoLanguageEngine
    {
        private readonly IncrementalParser _parser = new IncrementalParser();
        private readonly DiagnosticEngine _diagnostics = new DiagnosticEngine();
        private readonly SymbolIndex _symbols = new SymbolIndex();
        private readonly AutoImportEngine _imports = new AutoImportEngine();

        /// <summary>
        /// Met à jour un document et produit diagnostics + symboles + imports.
        /// </summary>
        public LanguageReport UpdateDocument(TextDocument document)
        {
            var root = _parser.Parse(document);

            _symbols.Index(document, root);

            return new LanguageReport
            {
                Diagnostics = _diagnostics.Analyze(document, root),
                Symbols = _symbols.Search(string.Empty).ToList(),
                MissingImports = _imports.SuggestMissingImports(document).ToList()
            };
        }

        /// <summary>
        /// Navigation symbolique.
        /// </summary>
        public SymbolInfo GoToDefinition(string symbolName)
        {
            return _symbols.FindDefinition(symbolName);
        }
    }
}
