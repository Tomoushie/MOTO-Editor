// Moto.Core/AI/Internal/ProjectUnderstandingEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur de compréhension profonde du projet.
    /// Construit une carte mentale locale : fichiers, symboles, modules, relations, problèmes.
    /// </summary>
    public class ProjectUnderstandingEngine
    {
        private static readonly Regex NamespaceRegex = new Regex(
            @"namespace\s+([\w\.]+)",
            RegexOptions.Compiled
        );

        private static readonly Regex TypeRegex = new Regex(
            @"\b(class|interface|struct|enum|record)\s+(\w+)",
            RegexOptions.Compiled
        );

        private static readonly Regex MethodRegex = new Regex(
            @"\b(?:public|private|protected|internal|static|async|virtual|override|sealed)\s+[\w<>\[\],\s]+\s+(\w+)\s*\(",
            RegexOptions.Compiled
        );

        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "node_modules", ".idea"
        };

        /// <summary>
        /// Construit la carte mentale complète du projet.
        /// </summary>
        public ProjectMap BuildMap(string rootPath)
        {
            var map = new ProjectMap
            {
                RootPath = rootPath
            };

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return map;
            }

            var files = CollectCSharpFiles(rootPath).Take(2000).ToList();
            var fileTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                try
                {
                    var text = File.ReadAllText(file);

                    map.Files.Add(file);
                    fileTexts[file] = text;
                    map.FileLineCounts[file] = text.Split('\n').Length;

                    ParseFile(map, file, text);
                    DetectLocalIssues(map, file, text);
                    DetectModule(map, file);
                }
                catch
                {
                    // Fichier illisible : on continue sans bloquer l'analyse.
                }
            }

            DetectInterfaceImplementationIssues(map);
            DetectSystemInterfaceIssues(map);
            BuildRelations(map, fileTexts);

            return map;
        }

        private IEnumerable<string> CollectCSharpFiles(string rootPath)
        {
            var directories = new Stack<string>();
            directories.Push(rootPath);

            while (directories.Count > 0)
            {
                var current = directories.Pop();

                string[] subDirs;
                string[] files;

                try
                {
                    subDirs = Directory.GetDirectories(current);
                    files = Directory.GetFiles(current);
                }
                catch
                {
                    continue;
                }

                foreach (var dir in subDirs)
                {
                    var name = Path.GetFileName(dir);

                    if (!ExcludedDirectories.Contains(name))
                    {
                        directories.Push(dir);
                    }
                }

                foreach (var file in files)
                {
                    if (Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return file;
                    }
                }
            }
        }

        private void ParseFile(ProjectMap map, string filePath, string text)
        {
            string currentNamespace = string.Empty;

            var namespaceMatch = NamespaceRegex.Match(text);
            if (namespaceMatch.Success)
            {
                currentNamespace = namespaceMatch.Groups[1].Value;
                map.Namespaces.Add(currentNamespace);

                map.Symbols.Add(new ProjectSymbol
                {
                    Name = currentNamespace,
                    Kind = SymbolKind.Namespace,
                    FilePath = filePath,
                    Namespace = currentNamespace,
                    Line = GetLine(text, namespaceMatch.Index)
                });
            }

            foreach (Match match in TypeRegex.Matches(text))
            {
                var kindText = match.Groups[1].Value;
                var name = match.Groups[2].Value;

                var kind = kindText.ToLowerInvariant() switch
                {
                    "class" => SymbolKind.Class,
                    "interface" => SymbolKind.Interface,
                    "struct" => SymbolKind.Struct,
                    "enum" => SymbolKind.Enum,
                    "record" => SymbolKind.Class,
                    _ => SymbolKind.Unknown
                };

                if (kind == SymbolKind.Class && name.EndsWith("System", StringComparison.Ordinal))
                {
                    kind = SymbolKind.System;
                }

                if (kind == SymbolKind.Class && name.EndsWith("Component", StringComparison.Ordinal))
                {
                    kind = SymbolKind.Component;
                }

                map.Symbols.Add(new ProjectSymbol
                {
                    Name = name,
                    Kind = kind,
                    FilePath = filePath,
                    Namespace = currentNamespace,
                    Line = GetLine(text, match.Index)
                });
            }

            foreach (Match match in MethodRegex.Matches(text))
            {
                var name = match.Groups[1].Value;

                map.Symbols.Add(new ProjectSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Method,
                    FilePath = filePath,
                    Namespace = currentNamespace,
                    Line = GetLine(text, match.Index)
                });
            }
        }

        private void DetectLocalIssues(ProjectMap map, string filePath, string text)
        {
            if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            {
                map.Issues.Add(new ProjectIssue
                {
                    Kind = IssueKind.Todo,
                    Severity = IssueSeverity.Info,
                    Message = "TODO détecté.",
                    FilePath = filePath
                });
            }

            if (text.Contains("NotImplementedException"))
            {
                map.Issues.Add(new ProjectIssue
                {
                    Kind = IssueKind.NotImplementedException,
                    Severity = IssueSeverity.Warning,
                    Message = "Méthode non implémentée détectée.",
                    FilePath = filePath
                });
            }

            int open = Count(text, '{');
            int close = Count(text, '}');

            if (Math.Abs(open - close) > 2)
            {
                map.Issues.Add(new ProjectIssue
                {
                    Kind = IssueKind.UnbalancedBraces,
                    Severity = IssueSeverity.Error,
                    Message = $"Accolades potentiellement déséquilibrées : {open} ouvertes, {close} fermées.",
                    FilePath = filePath
                });
            }
        }

        private void DetectModule(ProjectMap map, string filePath)
        {
            var relative = Path.GetRelativePath(map.RootPath, filePath);

            if (relative.Contains(Path.DirectorySeparatorChar))
            {
                var topFolder = relative.Split(Path.DirectorySeparatorChar)[0];

                if (!ExcludedDirectories.Contains(topFolder))
                {
                    map.Modules.Add(topFolder);
                }
            }
        }

        private void DetectInterfaceImplementationIssues(ProjectMap map)
        {
            var classNames = map.Symbols
                .Where(s => s.Kind == SymbolKind.Class || s.Kind == SymbolKind.System || s.Kind == SymbolKind.Component)
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var interfaces = map.Symbols
                .Where(s => s.Kind == SymbolKind.Interface)
                .ToList();

            foreach (var iface in interfaces)
            {
                if (iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1]))
                {
                    var expected = iface.Name.Substring(1);

                    if (!classNames.Contains(expected))
                    {
                        map.Issues.Add(new ProjectIssue
                        {
                            Kind = IssueKind.MissingImplementation,
                            Severity = IssueSeverity.Warning,
                            Message = $"Interface '{iface.Name}' sans implémentation claire '{expected}'.",
                            FilePath = iface.FilePath,
                            SymbolName = iface.Name,
                            Namespace = iface.Namespace
                        });
                    }
                }
            }
        }

        private void DetectSystemInterfaceIssues(ProjectMap map)
        {
            var interfaceNames = map.Symbols
                .Where(s => s.Kind == SymbolKind.Interface)
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var systems = map.Symbols
                .Where(s => s.Kind == SymbolKind.System)
                .ToList();

            foreach (var system in systems)
            {
                var expectedInterface = "I" + system.Name;

                if (!interfaceNames.Contains(expectedInterface))
                {
                    map.Issues.Add(new ProjectIssue
                    {
                        Kind = IssueKind.MissingInterfaceForSystem,
                        Severity = IssueSeverity.Warning,
                        Message = $"Système '{system.Name}' sans interface '{expectedInterface}'.",
                        FilePath = system.FilePath,
                        SymbolName = system.Name,
                        Namespace = system.Namespace
                    });
                }
            }
        }

        private void BuildRelations(ProjectMap map, Dictionary<string, string> fileTexts)
        {
            var importantSymbols = map.Symbols
                .Where(s => s.Kind == SymbolKind.Class ||
                            s.Kind == SymbolKind.Interface ||
                            s.Kind == SymbolKind.System ||
                            s.Kind == SymbolKind.Component)
                .Take(800)
                .ToList();

            foreach (var symbol in importantSymbols)
            {
                foreach (var kv in fileTexts)
                {
                    if (string.Equals(kv.Key, symbol.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (kv.Value.Contains(symbol.Name, StringComparison.Ordinal))
                    {
                        if (!map.Relations.TryGetValue(kv.Key, out var list))
                        {
                            list = new List<string>();
                            map.Relations[kv.Key] = list;
                        }

                        var relation = $"{symbol.Name} défini dans {Path.GetRelativePath(map.RootPath, symbol.FilePath)}";

                        if (!list.Contains(relation))
                        {
                            list.Add(relation);
                        }
                    }
                }
            }
        }

        private int GetLine(string text, int index)
        {
            int line = 1;

            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        private int Count(string text, char c)
        {
            int count = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == c)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
