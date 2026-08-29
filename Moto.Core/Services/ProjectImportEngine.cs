// Moto.Core/Services/ProjectImportEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Moto.Core.Services
{
    /// <summary>Résultat de l'analyse d'import d'un projet externe.</summary>
    public class ImportReport
    {
        public bool Success { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DetectedIde { get; set; } = "Inconnu";
        public string ProjectKind { get; set; } = "Inconnu";
        public List<string> DetectedFiles { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Importe des projets créés dans d'autres IDE
    /// (Visual Studio, VS Code, Node, etc.) en détectant leur type.
    /// </summary>
    public class ProjectImportEngine
    {
        public ImportReport Analyze(string path)
        {
            var report = new ImportReport { SourcePath = path };

            if (!Directory.Exists(path))
            {
                report.Success = false;
                report.Notes.Add("Chemin introuvable.");
                return report;
            }

            report.Success = true;

            var files = SafeEnumerate(path);

            // Détection par marqueurs.
            bool hasSln = files.Any(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
            bool hasCsproj = files.Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
            bool hasVscode = Directory.Exists(Path.Combine(path, ".vscode"));
            bool hasPackageJson = files.Any(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));

            if (hasSln)
            {
                report.DetectedIde = "Visual Studio";
                report.ProjectKind = "Solution .NET";
                report.Notes.Add("Solution détectée : MOTO compilera via dotnet CLI.");
            }
            else if (hasCsproj)
            {
                report.DetectedIde = "Visual Studio / .NET";
                report.ProjectKind = "Projet .NET";
            }
            else if (hasVscode)
            {
                report.DetectedIde = "Visual Studio Code";
                report.ProjectKind = hasPackageJson ? "Projet Node/JS" : "Workspace générique";
                report.Notes.Add("Configuration .vscode détectée : les paramètres seront adaptés.");
            }
            else if (hasPackageJson)
            {
                report.DetectedIde = "VS Code / Node";
                report.ProjectKind = "Projet Node/JS";
            }
            else
            {
                report.DetectedIde = "Générique";
                report.ProjectKind = "Dossier de fichiers";
            }

            report.DetectedFiles.AddRange(files.Take(200));

            return report;
        }

        private IEnumerable<string> SafeEnumerate(string root)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", ".git", "node_modules", ".vs"
            };

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                string[] sub;
                string[] files;

                try
                {
                    sub = Directory.GetDirectories(dir);
                    files = Directory.GetFiles(dir);
                }
                catch
                {
                    continue;
                }

                foreach (var s in sub)
                {
                    if (!excluded.Contains(Path.GetFileName(s)))
                    {
                        stack.Push(s);
                    }
                }

                foreach (var f in files)
                {
                    yield return f;
                }
            }
        }
    }
}
