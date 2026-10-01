// Moto.Editor/Services/OutlineExtractor.cs
// ★ AJOUT (01/10, décision C item 3) : extraction de symboles pour le panneau Outline.
// Lit RÉELLEMENT le texte du fichier actif et extrait des motifs réels (déclarations
// de classes/méthodes/fonctions/imports) — jamais de contenu inventé. Un faux positif
// occasionnel est acceptable (c'est un extracteur par motifs, pas un compilateur) ;
// une liste vide est le résultat honnête quand rien n'est reconnu.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Moto.Editor.Services
{
    /// <summary>Un symbole extrait du fichier actif, avec sa ligne (1-based).</summary>
    public sealed class OutlineSymbol
    {
        public string Name { get; }
        public string Kind { get; }
        public int Line { get; }
        /// <summary>Niveau d'indentation (0 = racine, 1 = membre, 2 = imbriqué) pour le retrait visuel.</summary>
        public int Depth { get; }
        public string Indent => new string(' ', Depth * 2);

        public OutlineSymbol(string name, string kind, int line, int depth)
        {
            Name = name;
            Kind = kind;
            Line = line;
            Depth = depth;
        }
    }

    /// <summary>Extracteur de symboles par motifs, sans dépendance externe.</summary>
    public static class OutlineExtractor
    {
        // Les motifs sont volontairement simples et honnêtes : ils capturent des
        // déclarations réellement présentes dans le texte, rien d'autre.
        private static readonly Regex CSharpType = new(
            @"^\s*(?:(?:public|private|protected|internal)\s+)?(?:(?:static|abstract|sealed|partial|readonly)\s+)*(?:class|interface|enum|struct|record)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);
        private static readonly Regex CSharpMember = new(
            @"^\s*(?:(?:public|private|protected|internal)\s+)?(?:(?:static|async|override|virtual|abstract|sealed|partial)\s+)*[A-Za-z_][A-Za-z0-9_<>,\.\[\]\?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(",
            RegexOptions.Compiled);
        private static readonly Regex PythonDef = new(
            @"^(\s*)(?:async\s+)?def\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(",
            RegexOptions.Compiled);
        private static readonly Regex PythonClass = new(
            @"^(\s*)class\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);
        private static readonly Regex JsFunction = new(
            @"^\s*(?:export\s+)?(?:async\s+)?function\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);
        private static readonly Regex JsClass = new(
            @"^\s*(?:export\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);
        private static readonly Regex Import = new(
            @"^\s*(?:using\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*;|import\s+.+|from\s+.+\s+import\s+.+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Extrait les symboles du texte selon le langage (mêmes codes que
        /// <c>CodeEditorView.LanguageOf</c> : cs/clike/js/py/…). Retourne une liste vide
        /// si le texte est vide ou qu'aucun motif n'est reconnu.
        /// </summary>
        public static IReadOnlyList<OutlineSymbol> Extract(string language, string text)
        {
            var result = new List<OutlineSymbol>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Profondeur par indentation : nombre d'espaces en tête / 4 (Python), sinon 0.
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                int depth = (line.Length - line.TrimStart().Length) / 4;
                if (depth > 2) depth = 2;

                switch (language)
                {
                    case "py":
                        var pc = PythonClass.Match(line);
                        if (pc.Success) { result.Add(new OutlineSymbol(pc.Groups[2].Value, "class", i + 1, depth)); continue; }
                        var pd = PythonDef.Match(line);
                        if (pd.Success) { result.Add(new OutlineSymbol(pd.Groups[2].Value, "def", i + 1, depth)); continue; }
                        break;
                    case "js":
                        var jc = JsClass.Match(line);
                        if (jc.Success) { result.Add(new OutlineSymbol(jc.Groups[1].Value, "class", i + 1, depth)); continue; }
                        var jf = JsFunction.Match(line);
                        if (jf.Success) { result.Add(new OutlineSymbol(jf.Groups[1].Value, "function", i + 1, depth)); continue; }
                        break;
                    default: // cs / clike / autres
                        var ct = CSharpType.Match(line);
                        if (ct.Success) { result.Add(new OutlineSymbol(ct.Groups[1].Value, "type", i + 1, depth)); continue; }
                        var cm = CSharpMember.Match(line);
                        if (cm.Success) { result.Add(new OutlineSymbol(cm.Groups[1].Value, "méthode", i + 1, depth)); continue; }
                        break;
                }

                var imp = Import.Match(line);
                if (imp.Success)
                {
                    var importName = imp.Groups[1].Success ? imp.Groups[1].Value : "import";
                    result.Add(new OutlineSymbol(importName, "import", i + 1, depth));
                }
            }
            return result;
        }
    }
}
