// Moto.Core/AI/Cortex/StyleLearner.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Cortex
{
    /// <summary>
    /// Apprentissage du style de l'utilisateur : analyse le code écrit
    /// et extrait les patterns, conventions, habitudes.
    /// </summary>
    public class StyleLearner
    {
        private readonly CortexMemory _memory;

        public StyleLearner(CortexMemory memory)
        {
            _memory = memory;
        }

        /// <summary>Nombre de corrections utilisateur enregistrées (pour CortexEngine.GetStats()).</summary>
        public int CorrectionsApplied => _memory.GetCorrections(int.MaxValue).Count;

        /// <summary>Confiance moyenne des patterns appris (pour CortexEngine.GetStats()).</summary>
        public double AverageConfidence => _memory.Patterns.Count > 0
            ? _memory.Patterns.Average(p => p.Confidence)
            : 0.0;

        /// <summary>Analyse un fichier et apprend le style de l'utilisateur.</summary>
        public void LearnFromFile(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            LearnNamingConventions(content);
            LearnCodePatterns(content);
            LearnCommentStyle(content);
            LearnIndentationStyle(content);
            LearnTypeUsage(content);
        }

        private void LearnNamingConventions(string content)
        {
            // Classes : PascalCase
            var classMatches = Regex.Matches(content, @"\bclass\s+([A-Z][a-zA-Z0-9]*)");
            foreach (Match m in classMatches)
            {
                var name = m.Groups[1].Value;
                if (IsPascalCase(name))
                    _memory.RecordNamingConvention("class", "PascalCase");
            }

            // Méthodes : PascalCase
            var methodMatches = Regex.Matches(content, @"\b(public|private|protected|internal)\s+\w+\s+([A-Z][a-zA-Z0-9]*)\s*\(");
            foreach (Match m in methodMatches)
            {
                var name = m.Groups[2].Value;
                if (IsPascalCase(name))
                    _memory.RecordNamingConvention("method", "PascalCase");
            }

            // Variables locales : camelCase
            var varMatches = Regex.Matches(content, @"\b(var|[A-Z]\w*)\s+([a-z][a-zA-Z0-9]*)\s*=");
            foreach (Match m in varMatches)
            {
                var name = m.Groups[2].Value;
                if (IsCamelCase(name))
                    _memory.RecordNamingConvention("variable", "camelCase");
            }
        }

        private void LearnCodePatterns(string content)
        {
            // Pattern : utilisation de var vs types explicites
            var varUsage = Regex.Matches(content, @"\bvar\s+\w+\s*=").Count;
            var explicitUsage = Regex.Matches(content, @"\b(int|string|bool|double|float|List<|Dictionary<)\s+\w+\s*=").Count;

            if (varUsage > explicitUsage)
                _memory.RecordHabit("type_usage", "prefer_var", varUsage - explicitUsage);
            else
                _memory.RecordHabit("type_usage", "prefer_explicit", explicitUsage - varUsage);

            // Pattern : utilisation de LINQ
            var linqUsage = Regex.Matches(content, @"\.(Where|Select|FirstOrDefault|Any|All|Count)\(").Count;
            if (linqUsage > 0)
                _memory.RecordHabit("linq_usage", "uses_linq", linqUsage);

            // Pattern : async/await
            var asyncUsage = Regex.Matches(content, @"\basync\s+\w+\s+\w+\s*\(|await\s+").Count;
            if (asyncUsage > 0)
                _memory.RecordHabit("async_usage", "uses_async", asyncUsage);

            // Pattern : propriétés auto vs full
            var autoProps = Regex.Matches(content, @"\{\s*get;\s*set;\s*\}").Count;
            var fullProps = Regex.Matches(content, @"\{\s*get\s*\{[^}]+\}\s*set\s*\{[^}]+\}\s*\}").Count;

            if (autoProps > fullProps)
                _memory.RecordHabit("property_style", "auto_properties", autoProps - fullProps);
        }

        private void LearnCommentStyle(string content)
        {
            var lines = content.Split('\n');
            int xmlComments = 0;
            int singleLineComments = 0;
            int blockComments = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("///"))
                    xmlComments++;
                else if (trimmed.StartsWith("//"))
                    singleLineComments++;
                else if (trimmed.StartsWith("/*"))
                    blockComments++;
            }

            if (xmlComments > singleLineComments && xmlComments > blockComments)
                _memory.RecordHabit("comment_style", "xml_documentation", xmlComments);
            else if (singleLineComments > blockComments)
                _memory.RecordHabit("comment_style", "single_line", singleLineComments);
        }

        private void LearnIndentationStyle(string content)
        {
            var lines = content.Split('\n');
            int spaces = 0;
            int tabs = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("    ")) spaces++;
                else if (line.StartsWith("\t")) tabs++;
            }

            if (spaces > tabs)
                _memory.RecordHabit("indentation", "spaces", spaces);
            else
                _memory.RecordHabit("indentation", "tabs", tabs);
        }

        private void LearnTypeUsage(string content)
        {
            // Pattern : classes partielles
            var partialClasses = Regex.Matches(content, @"\bpartial\s+class").Count;
            if (partialClasses > 0)
                _memory.RecordHabit("class_style", "partial_classes", partialClasses);

            // Pattern : records vs classes
            var records = Regex.Matches(content, @"\brecord\s+\w+").Count;
            if (records > 0)
                _memory.RecordHabit("type_style", "uses_records", records);

            // Pattern : interfaces
            var interfaces = Regex.Matches(content, @"\binterface\s+I[A-Z]").Count;
            if (interfaces > 0)
                _memory.RecordHabit("interface_style", "i_prefix", interfaces);
        }

        private bool IsPascalCase(string name) =>
            !string.IsNullOrEmpty(name) && char.IsUpper(name[0]) && !name.Contains("_");

        private bool IsCamelCase(string name) =>
            !string.IsNullOrEmpty(name) && char.IsLower(name[0]) && !name.Contains("_");
    }
}
