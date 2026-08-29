// Moto.Core/AI/Internal/UserHistoryEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Historique utilisateur.
    /// MOTO AI se souvient des fichiers ouverts, commandes, erreurs, modules créés.
    /// </summary>
    public class UserHistoryEngine
    {
        private readonly List<HistoryEntry> _entries = new List<HistoryEntry>();
        private readonly string _storagePath;

        public UserHistoryEngine()
        {
            _storagePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor",
                "history.json"
            );

            Load();
        }

        public void RecordFileOpened(string path)
        {
            _entries.Add(new HistoryEntry
            {
                Type = "file_opened",
                Value = path,
                TimestampUtc = DateTime.UtcNow
            });

            TrimAndSave();
        }

        public void RecordCommand(string command)
        {
            _entries.Add(new HistoryEntry
            {
                Type = "command",
                Value = command,
                TimestampUtc = DateTime.UtcNow
            });

            TrimAndSave();
        }

        public void RecordError(string error)
        {
            _entries.Add(new HistoryEntry
            {
                Type = "error",
                Value = error,
                TimestampUtc = DateTime.UtcNow
            });

            TrimAndSave();
        }

        public void RecordModuleCreated(string moduleName)
        {
            _entries.Add(new HistoryEntry
            {
                Type = "module_created",
                Value = moduleName,
                TimestampUtc = DateTime.UtcNow
            });

            TrimAndSave();
        }

        public List<string> GetRecentFiles(int count = 20)
        {
            return _entries
                .Where(e => e.Type == "file_opened")
                .OrderByDescending(e => e.TimestampUtc)
                .Take(count)
                .Select(e => e.Value)
                .ToList();
        }

        public List<string> GetRecentCommands(int count = 20)
        {
            return _entries
                .Where(e => e.Type == "command")
                .OrderByDescending(e => e.TimestampUtc)
                .Take(count)
                .Select(e => e.Value)
                .ToList();
        }

        public List<string> GetRecentErrors(int count = 10)
        {
            return _entries
                .Where(e => e.Type == "error")
                .OrderByDescending(e => e.TimestampUtc)
                .Take(count)
                .Select(e => e.Value)
                .ToList();
        }

        public List<string> GetCreatedModules()
        {
            return _entries
                .Where(e => e.Type == "module_created")
                .Select(e => e.Value)
                .Distinct()
                .ToList();
        }

        private void TrimAndSave()
        {
            // Garder max 500 entrées.
            if (_entries.Count > 500)
            {
                var cutoff = _entries
                    .OrderByDescending(e => e.TimestampUtc)
                    .Take(500)
                    .ToList();

                _entries.Clear();
                _entries.AddRange(cutoff);
            }

            Save();
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // L'historique ne doit jamais bloquer l'éditeur.
            }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    var loaded = JsonSerializer.Deserialize<List<HistoryEntry>>(json);

                    if (loaded != null)
                    {
                        _entries.AddRange(loaded);
                    }
                }
            }
            catch
            {
                // Fichier corrompu : on repart de zéro.
            }
        }

        private class HistoryEntry
        {
            public string Type { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public DateTime TimestampUtc { get; set; }
        }
    }
}
