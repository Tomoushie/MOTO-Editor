// Moto.Core/AI/Internal/TimeMachineEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Internal
{
    public enum SnapshotReason
    {
        Manual, BeforeAutoFix, BeforeApplyAi, BeforeGenerate,
        ProjectGeneration, BeforeRestore, CriticalDetected
    }

    public class SnapshotFile
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class TimeSnapshot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Label { get; set; } = string.Empty;
        public SnapshotReason Reason { get; set; }
        public bool IsFull { get; set; }
        public List<SnapshotFile> Files { get; set; } = new List<SnapshotFile>();
        public List<string> DeletedFiles { get; set; } = new List<string>();
    }

    /// <summary>
    /// AI Time Machine : versions automatiques SANS Git.
    /// Snapshots incrémentaux locaux, détection de moments critiques,
    /// restauration par langage naturel ("reviens à il y a 10 minutes").
    /// </summary>
    public class TimeMachineEngine
    {
        private const int MaxSnapshots = 50;
        private const long MaxFileSize = 500 * 1024;

        private readonly string _workspace;
        private readonly string _storageDir;
        private readonly List<TimeSnapshot> _snapshots = new List<TimeSnapshot>();

        public TimeMachineEngine(string workspacePath)
        {
            _workspace = workspacePath;
            _storageDir = Path.Combine(workspacePath, ".moto", "timemachine");
            Load();
        }

        public IReadOnlyList<TimeSnapshot> Snapshots => _snapshots;

        /// <summary>
        /// Capture un snapshot. Incrémental : seuls les fichiers changés sont stockés.
        /// Retourne null si rien n'a changé.
        /// </summary>
        public TimeSnapshot Capture(string label, SnapshotReason reason)
        {
            var current = ReadDiskState();
            var previous = ReconstructState(_snapshots.Count - 1);

            var snapshot = new TimeSnapshot
            {
                Label = label,
                Reason = reason,
                IsFull = _snapshots.Count == 0
            };

            if (snapshot.IsFull)
            {
                foreach (var kv in current)
                {
                    snapshot.Files.Add(new SnapshotFile { Path = kv.Key, Content = kv.Value });
                }
            }
            else
            {
                foreach (var kv in current)
                {
                    if (!previous.TryGetValue(kv.Key, out var old) || old != kv.Value)
                    {
                        snapshot.Files.Add(new SnapshotFile { Path = kv.Key, Content = kv.Value });
                    }
                }

                foreach (var key in previous.Keys)
                {
                    if (!current.ContainsKey(key))
                    {
                        snapshot.DeletedFiles.Add(key);
                    }
                }

                if (snapshot.Files.Count == 0 && snapshot.DeletedFiles.Count == 0)
                {
                    return null;
                }
            }

            _snapshots.Add(snapshot);
            Prune();
            Save(snapshot);

            return snapshot;
        }

        /// <summary>
        /// Restaure l'état du projet à un snapshot donné.
        /// Sécurisé : capture un snapshot "avant restauration" d'abord.
        /// </summary>
        public TimeSnapshot Restore(Guid snapshotId)
        {
            var index = _snapshots.FindIndex(s => s.Id == snapshotId);

            if (index < 0)
            {
                return null;
            }

            Capture("Avant restauration", SnapshotReason.BeforeRestore);

            var state = ReconstructState(index);

            foreach (var kv in state)
            {
                var dir = Path.GetDirectoryName(kv.Key);

                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(kv.Key, kv.Value);
            }

            // Supprime les fichiers suivis qui n'existaient pas à ce moment.
            foreach (var file in EnumerateTracked(_workspace))
            {
                if (!state.ContainsKey(file) && File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            return _snapshots[index];
        }

        /// <summary>
        /// Résout une requête naturelle en snapshot cible.
        /// "Reviens à l'état d'il y a 10 minutes."
        /// "Reviens avant que j'aie cassé EnemyAI."
        /// </summary>
        public TimeSnapshot ResolveQuery(string query)
        {
            if (_snapshots.Count == 0)
            {
                return null;
            }

            var lower = query?.ToLowerInvariant() ?? string.Empty;

            // 1. Temps relatif : "il y a X minutes/heures/jours"
            var timeMatch = Regex.Match(lower, @"il y a\s+(\d+)\s+(minute|heure|jour)");

            if (timeMatch.Success)
            {
                var value = int.Parse(timeMatch.Groups[1].Value);
                var unit = timeMatch.Groups[2].Value;

                var span = unit == "minute" ? TimeSpan.FromMinutes(value)
                         : unit == "heure" ? TimeSpan.FromHours(value)
                         : TimeSpan.FromDays(value);

                var target = DateTime.UtcNow - span;

                return _snapshots.LastOrDefault(s => s.TimestampUtc <= target)
                       ?? _snapshots.First();
            }

            // 2. Fichier cassé : dernier snapshot où le fichier différait de l'état actuel.
            var fileKeyword = ExtractPascalKeyword(query);

            if (fileKeyword != null)
            {
                var current = ReadDiskState();

                for (int i = _snapshots.Count - 1; i >= 0; i--)
                {
                    var state = ReconstructState(i);

                    var pastEntry = state.FirstOrDefault(kv =>
                        kv.Key.Contains(fileKeyword, StringComparison.OrdinalIgnoreCase));

                    var currentEntry = current.FirstOrDefault(kv =>
                        kv.Key.Contains(fileKeyword, StringComparison.OrdinalIgnoreCase));

                    if (pastEntry.Key != null &&
                        currentEntry.Key != null &&
                        pastEntry.Value != currentEntry.Value)
                    {
                        return _snapshots[i];
                    }
                }
            }

            // 3. Par label.
            return _snapshots.LastOrDefault(s =>
                       s.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
                   ?? _snapshots.Last();
        }

        /// <summary>Reconstruit l'état complet du projet jusqu'à un index.</summary>
        private Dictionary<string, string> ReconstructState(int upToIndex)
        {
            var state = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i <= upToIndex && i < _snapshots.Count; i++)
            {
                foreach (var file in _snapshots[i].Files)
                {
                    state[file.Path] = file.Content;
                }

                foreach (var deleted in _snapshots[i].DeletedFiles)
                {
                    state.Remove(deleted);
                }
            }

            return state;
        }

        private Dictionary<string, string> ReadDiskState()
        {
            var state = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in EnumerateTracked(_workspace))
            {
                try
                {
                    if (new FileInfo(file).Length <= MaxFileSize)
                    {
                        state[file] = File.ReadAllText(file);
                    }
                }
                catch
                {
                    // Fichier illisible : ignoré.
                }
            }

            return state;
        }

        private IEnumerable<string> EnumerateTracked(string root)
        {
            if (!Directory.Exists(root))
            {
                yield break;
            }

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", ".git", ".vs", "node_modules", ".moto"
            };

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

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

                foreach (var sub in subDirs)
                {
                    if (!excluded.Contains(Path.GetFileName(sub)))
                    {
                        stack.Push(sub);
                    }
                }

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);

                    if (ext == ".cs" || ext == ".xaml")
                    {
                        yield return file;
                    }
                }
            }
        }

        private string ExtractPascalKeyword(string query)
        {
            var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MOTO", "AI", "IA", "XENO", "Editor", "Project"
            };

            var match = Regex.Match(query ?? string.Empty, @"\b([A-Z][a-zA-Z0-9]{2,})\b");

            if (match.Success && !stop.Contains(match.Groups[1].Value))
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Garde max N snapshots. Le premier reste toujours un snapshot complet.
        /// </summary>
        private void Prune()
        {
            while (_snapshots.Count > MaxSnapshots)
            {
                var removeCount = _snapshots.Count - MaxSnapshots;

                // Rend complet le snapshot qui deviendra le premier.
                var newFirst = _snapshots[removeCount];
                var state = ReconstructState(removeCount);

                newFirst.Files = state
                    .Select(kv => new SnapshotFile { Path = kv.Key, Content = kv.Value })
                    .ToList();
                newFirst.IsFull = true;
                newFirst.DeletedFiles = new List<string>();

                for (int i = 0; i < removeCount; i++)
                {
                    DeleteFile(_snapshots[i]);
                }

                _snapshots.RemoveRange(0, removeCount);
            }
        }

        private void Save(TimeSnapshot snapshot)
        {
            try
            {
                Directory.CreateDirectory(_storageDir);

                var path = Path.Combine(_storageDir, $"{snapshot.TimestampUtc:yyyyMMddHHmmssfff}_{snapshot.Id:N}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
            }
            catch
            {
                // La Time Machine ne doit jamais bloquer l'éditeur.
            }
        }

        private void Load()
        {
            try
            {
                if (!Directory.Exists(_storageDir))
                {
                    return;
                }

                var files = Directory.GetFiles(_storageDir, "*.json").OrderBy(f => f).ToList();

                foreach (var file in files)
                {
                    var snapshot = JsonSerializer.Deserialize<TimeSnapshot>(File.ReadAllText(file));

                    if (snapshot != null)
                    {
                        _snapshots.Add(snapshot);
                    }
                }
            }
            catch
            {
                // Stockage corrompu : on repart de zéro.
            }
        }

        private void DeleteFile(TimeSnapshot snapshot)
        {
            try
            {
                var path = Directory.GetFiles(_storageDir)
                    .FirstOrDefault(f => Path.GetFileName(f).Contains(snapshot.Id.ToString("N")));

                if (path != null)
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Silencieux.
            }
        }
    }
}
