// Moto.Editor/Indexing/ProjectIndexer.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Editor.Indexing
{
    /// <summary>
    /// Moteur d'indexation ultra-léger.
    /// Conçu pour 100 000+ lignes sans bloquer l'UI.
    ///
    /// Stratégie :
    /// - Regex compilées pour chaque type de symbole.
    /// - Scan parallèle limité pour ne pas saturer le CPU.
    /// - Exclusion des dossiers inutiles (bin, obj, etc.).
    /// - Incrémental : ne re-parse que les fichiers modifiés.
    /// </summary>
    public class ProjectIndexer
    {
        private readonly ProjectIndex _index;

        // Regex compilées : une seule fois, réutilisées des milliers de fois.
        private static readonly Regex NamespaceRegex = new Regex(
            @"namespace\s+([\w\.]+)",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private static readonly Regex TypeRegex = new Regex(
            @"\b(class|interface|struct|enum|record)\s+(\w+)",
            RegexOptions.Compiled
        );

        private static readonly Regex MethodRegex = new Regex(
            @"\b(?:public|private|protected|internal|static|async|virtual|override|sealed)\s+" +
            @"[\w<>\[\],\s]+\s+(\w+)\s*\(",
            RegexOptions.Compiled
        );

        private static readonly Regex PropertyRegex = new Regex(
            @"\b(?:public|private|protected|internal|static)\s+" +
            @"[\w<>\[\],\s]+\s+(\w+)\s*\{",
            RegexOptions.Compiled
        );

        // Dossiers à exclure pour la performance.
        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "node_modules", ".git", ".vs", ".idea", "packages", ".vscode"
        };

        // Extensions indexées.
        private static readonly HashSet<string> IndexedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h", ".go", ".rs"
        };

        /// <summary>Taille maximale d'un fichier indexé (500 Ko).</summary>
        private const long MaxFileSizeBytes = 500 * 1024;

        /// <summary>Déclenché à la fin de l'indexation complète.</summary>
        public event Action<ProjectIndex> IndexingCompleted;

        /// <summary>Déclenché à chaque fichier indexé (pour la progress bar).</summary>
        public event Action<string, int, int> FileIndexed;

        public ProjectIndexer(ProjectIndex index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        /// <summary>
        /// Indexe un workspace complet en arrière-plan.
        /// </summary>
        public async Task IndexWorkspaceAsync(string rootPath, CancellationToken cancellation = default)
        {
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            var files = CollectFiles(rootPath).ToList();
            int total = files.Count;
            int processed = 0;

            // Parallélisme limité pour rester réactif.
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                CancellationToken = cancellation
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(files, options, filePath =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);

                        // Ne re-indexe que si nécessaire.
                        if (!_index.NeedsReindex(filePath, fileInfo.LastWriteTimeUtc))
                        {
                            return;
                        }

                        // Retire les anciennes entrées du fichier.
                        _index.RemoveFile(filePath);

                        var content = File.ReadAllText(filePath);
                        var entries = ExtractSymbols(content, filePath);

                        foreach (var entry in entries)
                        {
                            _index.Add(entry);
                        }

                        _index.MarkFileIndexed(filePath, fileInfo.LastWriteTimeUtc);

                        var count = Interlocked.Increment(ref processed);
                        FileIndexed?.Invoke(filePath, count, total);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // Fichier illisible : on l'ignore silencieusement.
                    }
                });
            }, cancellation);

            IndexingCompleted?.Invoke(_index);
        }

        /// <summary>
        /// Ré-indexe un seul fichier. Utilisé par FileSystemWatcher.
        /// </summary>
        public void ReindexFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _index.RemoveFile(filePath);
                    return;
                }

                var fileInfo = new FileInfo(filePath);

                if (!IndexedExtensions.Contains(fileInfo.Extension) || fileInfo.Length > MaxFileSizeBytes)
                {
                    return;
                }

                _index.RemoveFile(filePath);

                var content = File.ReadAllText(filePath);
                var entries = ExtractSymbols(content, filePath);

                foreach (var entry in entries)
                {
                    _index.Add(entry);
                }

                _index.MarkFileIndexed(filePath, fileInfo.LastWriteTimeUtc);
            }
            catch
            {
                // Silencieux : un fichier verrouillé ne doit pas crasher l'index.
            }
        }

        /// <summary>
        /// Extrait les symboles d'un fichier.
        /// </summary>
        private IEnumerable<SymbolIndexEntry> ExtractSymbols(string content, string filePath)
        {
            var entries = new List<SymbolIndexEntry>();

            // 1. Namespace courant.
            string currentNamespace = string.Empty;
            var nsMatch = NamespaceRegex.Match(content);
            if (nsMatch.Success)
            {
                currentNamespace = nsMatch.Groups[1].Value;
            }

            // 2. Types (class, interface, struct, enum, record).
            foreach (Match match in TypeRegex.Matches(content))
            {
                var kindStr = match.Groups[1].Value;
                var name = match.Groups[2].Value;
                var line = GetLineNumber(content, match.Index);

                var kind = kindStr.ToLowerInvariant() switch
                {
                    "class" => SymbolKind.Class,
                    "interface" => SymbolKind.Interface,
                    "struct" => SymbolKind.Struct,
                    "enum" => SymbolKind.Enum,
                    "record" => SymbolKind.Record,
                    _ => SymbolKind.Unknown
                };

                // Convention Snake2000 : classe finissant par "System".
                if (kind == SymbolKind.Class && name.EndsWith("System", StringComparison.Ordinal))
                {
                    kind = SymbolKind.System;
                }

                entries.Add(new SymbolIndexEntry(name, filePath, currentNamespace, kind, line));
            }

            // 3. Méthodes.
            foreach (Match match in MethodRegex.Matches(content))
            {
                var name = match.Groups[1].Value;
                var line = GetLineNumber(content, match.Index);
                entries.Add(new SymbolIndexEntry(name, filePath, currentNamespace, SymbolKind.Method, line));
            }

            // 4. Propriétés.
            foreach (Match match in PropertyRegex.Matches(content))
            {
                var name = match.Groups[1].Value;
                var line = GetLineNumber(content, match.Index);
                entries.Add(new SymbolIndexEntry(name, filePath, currentNamespace, SymbolKind.Property, line));
            }

            return entries;
        }

        /// <summary>
        /// Calcule le numéro de ligne à partir d'un offset.
        /// </summary>
        private static int GetLineNumber(string content, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    line++;
                }
            }
            return line;
        }

        /// <summary>
        /// Collecte les fichiers à indexer, en excluant les dossiers inutiles.
        /// </summary>
        private IEnumerable<string> CollectFiles(string rootPath)
        {
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();

                string[] subDirs;
                string[] files;

                try
                {
                    subDirs = Directory.GetDirectories(dir);
                    files = Directory.GetFiles(dir);
                }
                catch
                {
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    var name = Path.GetFileName(subDir);
                    if (!ExcludedDirectories.Contains(name))
                    {
                        queue.Enqueue(subDir);
                    }
                }

                foreach (var file in files)
                {
                    var info = new FileInfo(file);

                    if (IndexedExtensions.Contains(info.Extension) && info.Length <= MaxFileSizeBytes)
                    {
                        yield return file;
                    }
                }
            }
        }
    }
}
