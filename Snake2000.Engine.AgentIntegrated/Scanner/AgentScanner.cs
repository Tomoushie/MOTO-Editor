// Snake2000.Engine.AgentIntegrated/Scanner/AgentScanner.cs
using System.Collections.Generic;
using System.IO;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Snake2000.Engine.AgentIntegrated.Scanner
{
    /// <summary>
    /// Scanner : première étape du pipeline XENO-SSS∞.
    /// Il scanne le workspace et produit la liste des fichiers utiles.
    /// </summary>
    public class AgentScanner
    {
        public AgentResult ScanProject(AgentContext context)
        {
            var result = new AgentResult
            {
                ModuleName = "Scanner",
                Status = "success",
                Summary = "Scan completed."
            };

            var files = new List<string>();

            if (!string.IsNullOrWhiteSpace(context.RootPath) && Directory.Exists(context.RootPath))
            {
                files.AddRange(CollectFiles(context.RootPath));
            }

            result.Payload["Files"] = files;
            result.Payload["ProjectMap"] = new Dictionary<string, object>();

            result.Details.Add($"Scanned {files.Count} files.");

            return result;
        }

        private IEnumerable<string> CollectFiles(string rootPath)
        {
            var excluded = new HashSet<string>
            {
                "bin", "obj", ".git", ".vs", "node_modules", ".idea"
            };

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

                    if (excluded.Contains(name))
                    {
                        continue;
                    }

                    directories.Push(dir);
                }

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);

                    if (ext == ".cs" || ext == ".md" || ext == ".json")
                    {
                        yield return file;
                    }
                }
            }
        }
    }
}
