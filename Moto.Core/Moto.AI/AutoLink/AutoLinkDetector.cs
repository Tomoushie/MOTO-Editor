// Moto.Core/AI/AutoLink/AutoLinkDetector.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal;

namespace Moto.Core.AI.AutoLink
{
    /// <summary>
    /// Détection regex-based des problèmes dans un fichier C#.
    /// Ultra léger : pas de Roslyn, juste des patterns textuels.
    /// </summary>
    public class AutoLinkDetector
    {
        private static readonly Regex NewClassRegex = new Regex(
            @"\bnew\s+([A-Z][a-zA-Z0-9_]+)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex InterfaceImplRegex = new Regex(
            @"\bclass\s+\w+\s*:\s*([A-Z][a-zA-Z0-9_,\s]+)",
            RegexOptions.Compiled);

        private static readonly Regex UsingRegex = new Regex(
            @"^\s*using\s+([a-zA-Z0-9_.]+)\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex MethodCallRegex = new Regex(
            @"\b(\w+)\.([A-Z][a-zA-Z0-9_]*)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex EmptyClassRegex = new Regex(
            @"\bclass\s+(\w+)\s*\{[\s\r\n]*\}",
            RegexOptions.Compiled);

        private static readonly Regex SystemCallRegex = new Regex(
            @"\b([A-Z][a-zA-Z0-9_]*System)\.(Update|Initialize)\s*\(",
            RegexOptions.Compiled);

        private readonly ProjectMap _map;

        public AutoLinkDetector(ProjectMap map)
        {
            _map = map;
        }

        public List<AutoLinkIssue> Detect(string filePath, string content)
        {
            var issues = new List<AutoLinkIssue>();

            if (string.IsNullOrWhiteSpace(content)) return issues;

            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                // 1. Classes manquantes : new ClassName()
                foreach (Match m in NewClassRegex.Matches(line))
                {
                    var className = m.Groups[1].Value;

                    if (!ClassExists(className))
                    {
                        issues.Add(new AutoLinkIssue
                        {
                            Kind = AutoLinkIssueKind.MissingClass,
                            SymbolName = className,
                            Line = lineNumber,
                            Code = line.Trim(),
                            Message = $"{className} n'existe pas.",
                            FilePath = filePath
                        });
                    }
                }

                // 2. Interfaces manquantes : class X : IInterface
                foreach (Match m in InterfaceImplRegex.Matches(line))
                {
                    var interfaces = m.Groups[1].Value.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => s.StartsWith("I"));

                    foreach (var iface in interfaces)
                    {
                        if (!InterfaceExists(iface))
                        {
                            issues.Add(new AutoLinkIssue
                            {
                                Kind = AutoLinkIssueKind.MissingInterface,
                                SymbolName = iface,
                                Line = lineNumber,
                                Code = line.Trim(),
                                Message = $"{iface} n'existe pas.",
                                FilePath = filePath
                            });
                        }
                    }
                }

                // 3. Systèmes non connectés : XSystem.Update()
                foreach (Match m in SystemCallRegex.Matches(line))
                {
                    var systemName = m.Groups[1].Value;

                    if (ClassExists(systemName) && !IsSystemRegistered(systemName))
                    {
                        issues.Add(new AutoLinkIssue
                        {
                            Kind = AutoLinkIssueKind.MissingSystem,
                            SymbolName = systemName,
                            Line = lineNumber,
                            Code = line.Trim(),
                            Message = $"{systemName} n'est pas enregistré dans le pipeline.",
                            FilePath = filePath
                        });
                    }
                }

                // 4. Méthodes manquantes : obj.Method()
                foreach (Match m in MethodCallRegex.Matches(line))
                {
                    var methodName = m.Groups[2].Value;
                    var objName = m.Groups[1].Value;

                    // Ignore les méthodes standard (ToString, Equals, etc.)
                    if (IsStandardMethod(methodName)) continue;

                    // TODO : vérifier si la méthode existe dans la classe de l'objet
                    // (nécessite une analyse plus poussée, skip pour l'instant)
                }
            }

            // 5. Classes vides
            foreach (Match m in EmptyClassRegex.Matches(content))
            {
                var className = m.Groups[1].Value;

                issues.Add(new AutoLinkIssue
                {
                    Kind = AutoLinkIssueKind.IncompleteClass,
                    SymbolName = className,
                    Line = GetLineNumber(content, m.Index),
                    Code = m.Value,
                    Message = $"{className} est vide.",
                    FilePath = filePath
                });
            }

            return issues;
        }

        private bool ClassExists(string name)
        {
            return _map.Symbols.Any(s =>
                s.Kind == SymbolKind.Class &&
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private bool InterfaceExists(string name)
        {
            return _map.Symbols.Any(s =>
                s.Kind == SymbolKind.Interface &&
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsSystemRegistered(string systemName)
        {
            // TODO : vérifier si le système est dans XenoPipeline
            // Pour l'instant, on suppose qu'il ne l'est pas
            return false;
        }

        private bool IsStandardMethod(string name)
        {
            var standard = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ToString", "Equals", "GetHashCode", "GetType",
                "Add", "Remove", "Clear", "Contains", "Count"
            };

            return standard.Contains(name);
        }

        private int GetLineNumber(string content, int index)
        {
            int line = 1;

            for (int i = 0; i < index && i < content.Length; i++)
            {
                if (content[i] == '\n') line++;
            }

            return line;
        }
    }
}
