// Moto.Editor/Services/OutlineExtractor.cs
// ★ AJOUT (01/10, décision C item 3) : extrait des symboles RÉELS depuis le texte
// du fichier actif pour alimenter le panneau Outline. Ce n'est PAS un compilateur :
// c'est un extracteur par motifs (regex/string) qui lit VRAIMENT chaque ligne et ne
// renvoie QUE des noms/lignes/kinds réellement trouvés dans le texte — jamais une
// liste factice. Un faux positif occasionnel est accepté (ex. un `else if` lu comme
// une méthode, un commentaire mal isolé) ; un contenu inventé ne l'est pas : si rien
// n'est reconnu, la liste est VIDE.
//
// Le LANGAGE vient de CodeEditorView.LanguageOf(path) — la même classification que la
// coloration syntaxique — pour choisir les motifs : "cs" (C#), "py" (Python), "js"
// (JS/TS), "clike" (Java/Kotlin/C/C++/…, mêmes mots-clés class/interface/enum/struct).
// Les autres langages (json/xml/md/css/sql/…) n'ont pas de symboles extractibles de
// façon fiable → liste vide, jamais de contenu deviné.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Moto.Editor.Controls;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Un symbole réel extrait du fichier actif. <see cref="Level"/> (0..2) est la
    /// profondeur d'imbrication servant UNIQUEMENT à l'indentation d'affichage : elle
    /// provient d'une heuristique honnête (équilibre d'accolades pour les langages
    /// C-like, indentation pour Python) — voir <see cref="OutlineExtractor"/>.
    /// </summary>
    public sealed class OutlineSymbol : INotifyPropertyChanged
    {
        public OutlineSymbol(string name, string kind, int line)
        {
            Name = name;
            Kind = kind;
            Line = line;
        }

        public string Name { get; }
        public string Kind { get; }
        public int Line { get; }

        /// <summary>Profondeur d'imbrication 0..2 (indentation d'affichage uniquement).</summary>
        public int Level { get; init; }

        /// <summary>Largeur d'indentation dérivée du niveau (spacer du DataTemplate).</summary>
        public double IndentWidth => Level * 14;

        private bool _isCurrent;

        /// <summary>★ Vrai pour le symbole correspondant au curseur (réglage op_auto_reveal).</summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent == value) return;
                _isCurrent = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>Extracteur de symboles par motifs, sans dépendance externe.</summary>
    public static class OutlineExtractor
    {
        // ──────────────────────────────────────────────────────────────────────
        // C# (et clike : mêmes mots-clés class/interface/enum/struct)
        // ──────────────────────────────────────────────────────────────────────

        // using System.IO ; / using static System.Math ; / global using X ; / using A = B ;
        private static readonly Regex CsUsing = new(
            @"^\s*(?:global\s+)?using\s+(?:static\s+)?(?<name>[\w.]+)\s*(?:=.*)?;",
            RegexOptions.Compiled);

        // record Foo / record class Foo / record struct Foo → Class (ou Struct si `struct`)
        private static readonly Regex CsRecord = new(
            @"^\s*(?:(?:public|private|protected|internal|sealed|static|abstract|partial|readonly|unsafe|new)\s+)*record\s+(?:(?<mod>class|struct)\s+)?(?<name>[A-Za-z_]\w*)",
            RegexOptions.Compiled);

        // class Foo / interface IFoo / enum E / struct S
        private static readonly Regex CsType = new(
            @"^\s*(?:(?:public|private|protected|internal|sealed|static|abstract|partial|readonly|unsafe|new)\s+)*(?<kw>class|interface|enum|struct)\s+(?<name>[A-Za-z_]\w*)",
            RegexOptions.Compiled);

        // Membre C# : [type(s)] Nom ( → méthode ; Nom { → propriété ; Nom ;/= → champ.
        // Le tail (`(`, `{`, `;` ou `=`) décide du Kind ; un faux positif occasionnel est accepté.
        private static readonly Regex CsMember = new(
            @"^\s*(?<decl>(?:[A-Za-z_][\w<>,?\[\]\.]*\s+)+)(?<name>[A-Za-z_]\w*)\s*(?<tail>[({;=])",
            RegexOptions.Compiled);

        // Mots-clés C# à ne JAMAIS prendre pour un nom de symbole (réduit les faux positifs).
        private static readonly HashSet<string> CsKeywords = new(StringComparer.Ordinal)
        {
            "if", "else", "for", "foreach", "while", "switch", "case", "catch", "finally",
            "try", "do", "using", "namespace", "return", "throw", "new", "lock", "default",
            "checked", "unchecked", "fixed", "this", "base", "in", "out", "ref", "params",
            "is", "as", "await", "yield", "get", "set", "init", "add", "remove", "when",
            "where", "select", "from", "join", "let", "group", "orderby", "on", "equals",
            "typeof", "nameof", "sizeof", "static", "void"
        };

        // ──────────────────────────────────────────────────────────────────────
        // Python
        // ──────────────────────────────────────────────────────────────────────
        private static readonly Regex PyClass = new(@"^class\s+(?<name>[A-Za-z_]\w*)", RegexOptions.Compiled);
        private static readonly Regex PyDef = new(@"^(?:async\s+)?def\s+(?<name>[A-Za-z_]\w*)", RegexOptions.Compiled);
        private static readonly Regex PyFrom = new(@"^from\s+(?<mod>[\w.]+)\s+import\b", RegexOptions.Compiled);
        private static readonly Regex PyImport = new(@"^import\s+(?<mod>[\w.]+)", RegexOptions.Compiled);

        // ──────────────────────────────────────────────────────────────────────
        // JS / TS
        // ──────────────────────────────────────────────────────────────────────
        private static readonly Regex JsFunction = new(
            @"^\s*(?:export\s+)?(?:async\s+)?function\s+(?<name>[A-Za-z_$][\w$]*)",
            RegexOptions.Compiled);
        private static readonly Regex JsClass = new(
            @"^\s*(?:export\s+)?(?:abstract\s+)?class\s+(?<name>[A-Za-z_$][\w$]*)",
            RegexOptions.Compiled);
        private static readonly Regex JsArrow = new(
            @"^\s*(?:export\s+)?(?:const|let|var)\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z_$][\w$]*)\s*=>",
            RegexOptions.Compiled);
        private static readonly Regex JsImportLine = new(@"^\s*import\b", RegexOptions.Compiled);
        private static readonly Regex JsFromModule = new(@"from\s+(['""])(?<mod>.+?)\1", RegexOptions.Compiled);
        private static readonly Regex JsBareModule = new(@"^\s*import\s+(['""])(?<mod>.+?)\1", RegexOptions.Compiled);

        // import X; (Java/Kotlin) — pour le mode "clike".
        private static readonly Regex ClikeImport = new(
            @"^\s*import\s+(?:static\s+)?(?<name>[\w.]+)\s*;",
            RegexOptions.Compiled);

        /// <summary>
        /// Extrait les symboles réels du texte d'après son langage (résolu via
        /// <see cref="CodeEditorView.LanguageOf(string?)"/>). Les imports/using sont
        /// toujours placés EN FIN de liste, après les vrais symboles.
        /// </summary>
        public static IReadOnlyList<OutlineSymbol> Extract(string? path, string? text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<OutlineSymbol>();

            var language = CodeEditorView.LanguageOf(path);
            var symbols = new List<OutlineSymbol>();
            var imports = new List<OutlineSymbol>();

            // Normalise les fins de ligne (CRLF/CR → LF) pour un numéro de ligne exact
            // et un découpage fiable quel que soit le fichier d'origine.
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            int braceDepth = 0; // équilibre d'accolades — indentation des langages C-like
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (IsCommentOrBlank(line, language))
                {
                    braceDepth = UpdateBraceDepth(line, braceDepth);
                    continue;
                }

                int lineNo = i + 1;
                int level = Math.Clamp(braceDepth, 0, 2);

                switch (language)
                {
                    case "py":
                        ExtractPython(line, lineNo, symbols, imports);
                        break;
                    case "js":
                        ExtractJavaScript(line, lineNo, level, symbols, imports);
                        break;
                    case "clike":
                        ExtractCLike(line, lineNo, level, symbols, imports);
                        break;
                    case "cs":
                    default:
                        // "cs" et tout langage inconnu : seuls les motifs C# sont tentés ;
                        // pour un langage non reconnu (json/xml/…), aucun motif ne matchera
                        // et la liste restera vide — jamais de contenu deviné.
                        ExtractCSharp(line, lineNo, level, symbols, imports);
                        break;
                }

                braceDepth = UpdateBraceDepth(line, braceDepth);
            }

            // ★ Doctrine : les imports/using vont EN FIN de liste, après les vrais symboles.
            symbols.AddRange(imports);
            return symbols;
        }

        // ----------------------------------------------------------------------
        // Extraction par langage
        // ----------------------------------------------------------------------

        private static void ExtractCSharp(string line, int lineNo, int level, List<OutlineSymbol> symbols, List<OutlineSymbol> imports)
        {
            var usingMatch = CsUsing.Match(line);
            if (usingMatch.Success)
            {
                imports.Add(new OutlineSymbol(usingMatch.Groups["name"].Value, "Import", lineNo));
                return;
            }

            // record : traité avant class/struct pour `record class X` / `record struct X`.
            var record = CsRecord.Match(line);
            if (record.Success)
            {
                var kind = string.Equals(record.Groups["mod"].Value, "struct", StringComparison.Ordinal) ? "Struct" : "Class";
                symbols.Add(new OutlineSymbol(record.Groups["name"].Value, kind, lineNo) { Level = level });
                return;
            }

            var type = CsType.Match(line);
            if (type.Success)
            {
                symbols.Add(new OutlineSymbol(type.Groups["name"].Value, KindForTypeKeyword(type.Groups["kw"].Value), lineNo) { Level = level });
                return;
            }

            // `namespace X` n'est pas dans notre Kind : on l'ignore explicitement pour ne
            // pas le lire comme une propriété (il ferait sinon un faux positif).
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("namespace ", StringComparison.Ordinal))
                return;

            var member = CsMember.Match(line);
            if (member.Success && !CsKeywords.Contains(member.Groups["name"].Value))
            {
                var kind = member.Groups["tail"].Value switch
                {
                    "(" => "Method",
                    "{" => "Property",
                    _ => "Field"
                };
                symbols.Add(new OutlineSymbol(member.Groups["name"].Value, kind, lineNo) { Level = level });
            }
        }

        private static void ExtractPython(string line, int lineNo, List<OutlineSymbol> symbols, List<OutlineSymbol> imports)
        {
            var trimmed = line.TrimStart();
            int level = Math.Clamp(VisualIndent(line) / 4, 0, 2);

            var from = PyFrom.Match(trimmed);
            if (from.Success)
            {
                imports.Add(new OutlineSymbol(from.Groups["mod"].Value, "Import", lineNo));
                return;
            }
            var import = PyImport.Match(trimmed);
            if (import.Success)
            {
                imports.Add(new OutlineSymbol(import.Groups["mod"].Value, "Import", lineNo));
                return;
            }

            var cls = PyClass.Match(trimmed);
            if (cls.Success)
            {
                symbols.Add(new OutlineSymbol(cls.Groups["name"].Value, "Class", lineNo) { Level = level });
                return;
            }

            var fn = PyDef.Match(trimmed);
            if (fn.Success)
            {
                // Heuristique honnête : un `def` au niveau 0 est une fonction ; un `def`
                // indenté (sous une classe) est une méthode. Une fonction imbriquée
                // (def dans def) serait lue « Method » — faux positif accepté.
                var kind = VisualIndent(line) > 0 ? "Method" : "Function";
                symbols.Add(new OutlineSymbol(fn.Groups["name"].Value, kind, lineNo) { Level = level });
            }
        }

        private static void ExtractJavaScript(string line, int lineNo, int level, List<OutlineSymbol> symbols, List<OutlineSymbol> imports)
        {
            if (JsImportLine.IsMatch(line))
            {
                imports.Add(new OutlineSymbol(JsImportName(line), "Import", lineNo));
                return;
            }

            var fn = JsFunction.Match(line);
            if (fn.Success)
            {
                symbols.Add(new OutlineSymbol(fn.Groups["name"].Value, "Function", lineNo) { Level = level });
                return;
            }

            var cls = JsClass.Match(line);
            if (cls.Success)
            {
                symbols.Add(new OutlineSymbol(cls.Groups["name"].Value, "Class", lineNo) { Level = level });
                return;
            }

            var arrow = JsArrow.Match(line);
            if (arrow.Success)
            {
                symbols.Add(new OutlineSymbol(arrow.Groups["name"].Value, "Function", lineNo) { Level = level });
            }
        }

        private static void ExtractCLike(string line, int lineNo, int level, List<OutlineSymbol> symbols, List<OutlineSymbol> imports)
        {
            var import = ClikeImport.Match(line);
            if (import.Success)
            {
                imports.Add(new OutlineSymbol(import.Groups["name"].Value, "Import", lineNo));
                return;
            }

            // Java/Kotlin/C++ partagent les mots-clés class/interface/enum/struct.
            var type = CsType.Match(line);
            if (type.Success)
            {
                symbols.Add(new OutlineSymbol(type.Groups["name"].Value, KindForTypeKeyword(type.Groups["kw"].Value), lineNo) { Level = level });
            }
        }

        // ----------------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------------

        private static string KindForTypeKeyword(string kw) => kw switch
        {
            "interface" => "Interface",
            "enum" => "Enum",
            "struct" => "Struct",
            _ => "Class"
        };

        private static string JsImportName(string line)
        {
            var from = JsFromModule.Match(line);
            if (from.Success) return from.Groups["mod"].Value;
            var bare = JsBareModule.Match(line);
            if (bare.Success) return bare.Groups["mod"].Value;
            // Repli : texte brut tronqué (sans le `;` final), jamais de module deviné.
            return line.Trim().TrimEnd(';').Trim();
        }

        /// <summary>Indentation visuelle d'une ligne Python (tabulation = 4 espaces).</summary>
        private static int VisualIndent(string line)
        {
            int indent = 0;
            foreach (var c in line)
            {
                if (c == ' ') indent += 1;
                else if (c == '\t') indent += 4;
                else break;
            }
            return indent;
        }

        /// <summary>Équilibre d'accolades { } — sert de profondeur pour les langages C-like.</summary>
        private static int UpdateBraceDepth(string line, int depth)
        {
            foreach (var c in line)
            {
                if (c == '{') depth++;
                else if (c == '}') depth = Math.Max(0, depth - 1);
            }
            return depth;
        }

        /// <summary>
        /// Ignore les lignes vides et les lignes manifestement en commentaire (réduit les
        /// faux positifs). Ne prétend PAS gérer les commentaires multi-lignes ou les
        /// chaînes contenant un motif — limitation assumée d'un extracteur par motifs.
        /// </summary>
        private static bool IsCommentOrBlank(string line, string language)
        {
            var t = line.TrimStart();
            if (t.Length == 0) return true;
            if (language == "py") return t[0] == '#';
            return t.StartsWith("//", StringComparison.Ordinal) ||
                   t.StartsWith("/*", StringComparison.Ordinal) ||
                   t[0] == '*';
        }
    }
}
