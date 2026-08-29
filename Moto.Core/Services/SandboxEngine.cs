// Moto.Core/Services/SandboxEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Moto.Core.Services
{
    /// <summary>
    /// Zone d'isolement : copie le projet dans un dossier séparé
    /// pour que l'IA et l'utilisateur testent sans toucher au projet réel.
    /// </summary>
    public class SandboxEngine
    {
        private readonly string _sandboxRoot;

        public SandboxEngine()
        {
            _sandboxRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "Sandbox");
        }

        /// <summary>Crée une sandbox et retourne son chemin.</summary>
        public string Create(string sourcePath, string name)
        {
            var sandboxPath = Path.Combine(_sandboxRoot, $"{name}_{DateTime.Now:yyyyMMddHHmmss}");

            CopyDirectory(sourcePath, sandboxPath);

            return sandboxPath;
        }

        /// <summary>Liste les fichiers modifiés dans la sandbox par rapport à la source.</summary>
        public List<string> ListChangedFiles(string sandboxPath, string sourcePath)
        {
            var changed = new List<string>();

            foreach (var sandboxFile in EnumerateFiles(sandboxPath))
            {
                var relative = Path.GetRelativePath(sandboxPath, sandboxFile);
                var sourceFile = Path.Combine(sourcePath, relative);

                if (!File.Exists(sourceFile) ||
                    !File.ReadAllText(sourceFile).Equals(File.ReadAllText(sandboxFile)))
                {
                    changed.Add(relative);
                }
            }

            return changed;
        }

        /// <summary>Applique les modifications de la sandbox au projet réel.</summary>
        public void ApplyToSource(string sandboxPath, string sourcePath)
        {
            foreach (var sandboxFile in EnumerateFiles(sandboxPath))
            {
                var relative = Path.GetRelativePath(sandboxPath, sandboxFile);
                var target = Path.Combine(sourcePath, relative);

                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                File.Copy(sandboxFile, target, overwrite: true);
            }
        }

        /// <summary>Supprime la sandbox.</summary>
        public void Discard(string sandboxPath)
        {
            try
            {
                if (Directory.Exists(sandboxPath))
                {
                    Directory.Delete(sandboxPath, recursive: true);
                }
            }
            catch
            {
                // Silencieux.
            }
        }

        private void CopyDirectory(string source, string target)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", ".git", ".vs", "node_modules"
            };

            Directory.CreateDirectory(target);

            foreach (var dir in Directory.GetDirectories(source))
            {
                var name = Path.GetFileName(dir);
                if (!excluded.Contains(name))
                {
                    CopyDirectory(dir, Path.Combine(target, name));
                }
            }

            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            }
        }

        private IEnumerable<string> EnumerateFiles(string root)
        {
            return Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        }
    }
}
