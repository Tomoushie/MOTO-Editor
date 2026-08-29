// Moto.Core/AI/Internal/GranularRestoreEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// 29. Time Machine granulaire : restaure UN fichier, UN module ou UN système
    /// à partir des snapshots existants (.moto/timemachine), sans Git.
    /// Lit les snapshots JSON produits par TimeMachineEngine : aucune duplication.
    /// </summary>
    public class GranularRestoreEngine
    {
        private readonly string _workspace;
        private readonly List<TimeSnapshot> _snapshots = new();

        public GranularRestoreEngine(string workspacePath)
        {
            _workspace = workspacePath;
            Load();
        }

        public IReadOnlyList<TimeSnapshot> Snapshots => _snapshots;

        /// <summary>Restaure un fichier (par nom ou chemin relatif) au dernier état connu.</summary>
        public bool RestoreFile(string nameOrPath)
        {
            for (int i = _snapshots.Count - 1; i >= 0; i--)
            {
                var state = Reconstruct(i);

                var match = state.FirstOrDefault(kv =>
                    kv.Key.EndsWith(nameOrPath, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(kv.Key).Equals(Path.GetFileName(nameOrPath), StringComparison.OrdinalIgnoreCase));

                if (match.Key != null)
                {
                    CaptureSafetySnapshot();
                    Write(match.Key, match.Value);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Restaure tous les fichiers d'un module. Retourne le nombre restauré.</summary>
        public int RestoreModule(string moduleName)
        {
            return RestoreWhere(path =>
                Path.GetFileName(path).StartsWith(moduleName, StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}{moduleName}{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Restaure tout ce qui porte le nom d'un système (System/Component/Interface).</summary>
        public int RestoreSystem(string systemName)
        {
            var baseName = systemName.Replace("System", "");

            return RestoreWhere(path =>
                Path.GetFileName(path).Contains(baseName, StringComparison.OrdinalIgnoreCase));
        }

        // ------------------------------------------------------------------

        private int RestoreWhere(Func<string, bool> predicate)
        {
            // Dernier snapshot où les fichiers ciblés différaient de l'état actuel.
            for (int i = _snapshots.Count - 1; i >= 0; i--)
            {
                var state = Reconstruct(i);
                var targets = state.Where(kv => predicate(kv.Key)).ToList();

                if (targets.Count == 0) continue;

                CaptureSafetySnapshot();

                foreach (var t in targets)
                {
                    Write(t.Key, t.Value);
                }

                return targets.Count;
            }

            return 0;
        }

        /// <summary>Reconstruit l'état complet jusqu'à un index (snapshots incrémentaux).</summary>
        private Dictionary<string, string> Reconstruct(int upTo)
        {
            var state = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i <= upTo && i < _snapshots.Count; i++)
            {
                foreach (var f in _snapshots[i].Files)
                {
                    state[f.Path] = f.Content;
                }

                foreach (var d in _snapshots[i].DeletedFiles)
                {
                    state.Remove(d);
                }
            }

            return state;
        }

        /// <summary>Sécurité : snapshot avant toute restauration (rollback possible).</summary>
        private void CaptureSafetySnapshot()
        {
            new TimeMachineEngine(_workspace)
                .Capture("Avant restauration granulaire", SnapshotReason.BeforeRestore);
        }

        private void Write(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content);
        }

        private void Load()
        {
            var dir = Path.Combine(_workspace, ".moto", "timemachine");

            try
            {
                if (!Directory.Exists(dir)) return;

                foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
                {
                    var snap = JsonSerializer.Deserialize<TimeSnapshot>(File.ReadAllText(file));

                    if (snap != null) _snapshots.Add(snap);
                }
            }
            catch
            {
                // Snapshots corrompus : restauration indisponible, sans crash.
            }
        }
    }
}
