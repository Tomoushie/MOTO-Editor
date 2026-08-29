// Moto.Core/Doc/DocEngine.cs (avec event SourceFileChanged ajouté)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Moto.Core.AI.Internal;
using Moto.Core.Settings;

namespace Moto.Core.Doc
{
    /// <summary>
    /// MOTO Doc Engine : génère et maintient automatiquement la documentation
    /// du projet (README, Structure, Arborescence, Modules, Systems, Architecture).
    /// </summary>
    public class DocEngine : IDisposable
    {
        private readonly string _workspace;
        private readonly ProjectUnderstandingEngine _understanding = new();
        private readonly PatternDetectorEngine _patterns = new();
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private DateTime _lastUpdate = DateTime.MinValue;

        /// <summary>Déclenché après chaque régénération de documentation.</summary>
        public event Action<DocReport> DocumentationUpdated;

        /// <summary>Déclenché quand un fichier source change (pour détection continue).</summary>
        public event Action<string> SourceFileChanged;

        /// <summary>Chemin du dossier de documentation (.moto/docs/).</summary>
        public string DocsFolder => Path.Combine(_workspace, ".moto", "docs");

        /// <summary>6 fichiers générés par défaut.</summary>
        public static readonly DocKind[] AllKinds =
        {
            DocKind.Readme, DocKind.Structure, DocKind.Arborescence,
            DocKind.Modules, DocKind.Systems, DocKind.Architecture
        };

        public DocEngine(string workspacePath)
        {
            _workspace = workspacePath;

            // Watcher : détecte les modifications dans le projet
            _watcher = new FileSystemWatcher(workspacePath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnProjectChanged;
            _watcher.Created += OnProjectChanged;
            _watcher.Deleted += OnProjectChanged;
            _watcher.Renamed += OnProjectChanged;

            // Debounce : attend 3 secondes sans modification avant de régénérer
            _debounceTimer = new Timer(3000) { AutoReset = false };
            _debounceTimer.Elapsed += (s, e) => RegenerateAsync().ConfigureAwait(false);
        }

        /// <summary>Génère toute la documentation manuellement.</summary>
        public async Task<DocReport> GenerateAsync()
        {
            return await Task.Run(() =>
            {
                var map = _understanding.BuildMap(_workspace);
                var projectName = Path.GetFileName(_workspace);

                var report = new DocReport
                {
                    ProjectName = projectName,
                    TotalSymbols = map.Symbols.Count,
                    TotalFiles = map.Files.Count
                };

                // Crée le dossier .moto/docs/
                Directory.CreateDirectory(DocsFolder);

                // 1. README.md
                var readme = new DocFile
                {
                    Kind = DocKind.Readme,
                    FileName = "README.md",
                    Content = DocGenerators.GenerateReadme(map, projectName)
                };
                WriteDoc(readme);
                report.Files.Add(readme);

                // 2. Structure.md
                var structure = new DocFile
                {
                    Kind = DocKind.Structure,
                    FileName = "Structure.md",
                    Content = DocGenerators.GenerateStructure(map, projectName)
                };
                WriteDoc(structure);
                report.Files.Add(structure);

                // 3. Arborescence.md
                var arbo = new DocFile
                {
                    Kind = DocKind.Arborescence,
                    FileName = "Arborescence.md",
                    Content = DocGenerators.GenerateArborescence(map, projectName, _workspace)
                };
                WriteDoc(arbo);
                report.Files.Add(arbo);

                // 4. Modules.md
                var modules = new DocFile
                {
                    Kind = DocKind.Modules,
                    FileName = "Modules.md",
                    Content = DocGenerators.GenerateModules(map, projectName)
                };
                WriteDoc(modules);
                report.Files.Add(modules);

                // 5. Systems.md
                var systems = new DocFile
                {
                    Kind = DocKind.Systems,
                    FileName = "Systems.md",
                    Content = DocGenerators.GenerateSystems(map, projectName)
                };
                WriteDoc(systems);
                report.Files.Add(systems);

                // 6. Architecture.md
                var archi = new DocFile
                {
                    Kind = DocKind.Architecture,
                    FileName = "Architecture.md",
                    Content = DocGenerators.GenerateArchitecture(map, projectName, _patterns)
                };
                WriteDoc(archi);
                report.Files.Add(archi);

                _lastUpdate = DateTime.UtcNow;
                DocumentationUpdated?.Invoke(report);

                return report;
            });
        }

        /// <summary>Génère un fichier spécifique uniquement.</summary>
        public async Task<DocFile> GenerateSingleAsync(DocKind kind)
        {
            return await Task.Run(() =>
            {
                var map = _understanding.BuildMap(_workspace);
                var projectName = Path.GetFileName(_workspace);

                var content = kind switch
                {
                    DocKind.Readme => DocGenerators.GenerateReadme(map, projectName),
                    DocKind.Structure => DocGenerators.GenerateStructure(map, projectName),
                    DocKind.Arborescence => DocGenerators.GenerateArborescence(map, projectName, _workspace),
                    DocKind.Modules => DocGenerators.GenerateModules(map, projectName),
                    DocKind.Systems => DocGenerators.GenerateSystems(map, projectName),
                    DocKind.Architecture => DocGenerators.GenerateArchitecture(map, projectName, _patterns),
                    _ => ""
                };

                var file = new DocFile
                {
                    Kind = kind,
                    FileName = $"{kind}.md",
                    Content = content
                };

                WriteDoc(file);
                return file;
            });
        }

        /// <summary>Vérifie si la documentation est à jour.</summary>
        public bool IsUpToDate()
        {
            if (!Directory.Exists(DocsFolder)) return false;

            var files = Directory.GetFiles(DocsFolder, "*.md");
            if (files.Length < AllKinds.Length) return false;

            var oldest = files.Min(f => new FileInfo(f).LastWriteTimeUtc);
            return oldest >= _lastUpdate;
        }

        // ------------------------------------------------------------------
        // Auto-update
        // ------------------------------------------------------------------

        private void OnProjectChanged(object sender, FileSystemEventArgs e)
        {
            // Ignore les modifications dans .moto/ (on modifierait nos propres fichiers)
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.moto{Path.DirectorySeparatorChar}"))
                return;

            // Ignore les fichiers non-source
            var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            var validExts = new HashSet<string> { ".cs", ".xaml", ".json", ".md", ".txt" };
            if (!validExts.Contains(ext)) return;

            if (!SettingsEngine.Shared.GetBool("doc_auto_update")) return;

            // Reset du timer debounce
            _debounceTimer.Stop();
            _debounceTimer.Start();

            // Notifie les autres moteurs (ex: PlatformEngine pour détection continue)
            SourceFileChanged?.Invoke(e.FullPath);
        }

        private async Task RegenerateAsync()
        {
            try
            {
                await GenerateAsync();
            }
            catch
            {
                // La doc ne doit jamais crasher l'éditeur.
            }
        }

        private void WriteDoc(DocFile file)
        {
            file.Path = Path.Combine(DocsFolder, file.FileName);
            file.LineCount = file.Content.Split('\n').Length;

            try
            {
                File.WriteAllText(file.Path, file.Content);
            }
            catch
            {
                // Écriture impossible : ignoré.
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
