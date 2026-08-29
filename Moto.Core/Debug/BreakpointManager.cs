// Moto.Core/Debug/BreakpointManager.cs
// Gestion centralisée des breakpoints avec persistance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.Debug
{
    public sealed class BreakpointInfo
    {
        public int Id { get; set; }
        public string FilePath { get; init; } = string.Empty;
        public int Line { get; init; }
        public string? Condition { get; set; }
        public int HitCount { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Verified { get; set; }
    }

    /// <summary>
    /// Gestionnaire de breakpoints avec persistance workspace.
    /// </summary>
    public sealed class BreakpointManager
    {
        private readonly string _breakpointsPath;
        private readonly List<BreakpointInfo> _breakpoints = new();
        private readonly object _lock = new();
        private int _nextId = 1;

        public event Action<IReadOnlyList<BreakpointInfo>>? BreakpointsChanged;

        public BreakpointManager(string workspaceRoot)
        {
            var motoDir = Path.Combine(workspaceRoot, ".moto");
            Directory.CreateDirectory(motoDir);
            _breakpointsPath = Path.Combine(motoDir, "breakpoints.json");
            Load();
        }

        /// <summary>Ajoute un breakpoint.</summary>
        public BreakpointInfo AddBreakpoint(string filePath, int line, string? condition = null)
        {
            lock (_lock)
            {
                var bp = new BreakpointInfo
                {
                    Id = _nextId++,
                    FilePath = filePath,
                    Line = line,
                    Condition = condition,
                    Enabled = true
                };
                _breakpoints.Add(bp);
                Save();
                BreakpointsChanged?.Invoke(_breakpoints);
                return bp;
            }
        }

        /// <summary>Supprime un breakpoint.</summary>
        public void RemoveBreakpoint(int id)
        {
            lock (_lock)
            {
                _breakpoints.RemoveAll(b => b.Id == id);
                Save();
                BreakpointsChanged?.Invoke(_breakpoints);
            }
        }

        /// <summary>Active/désactive un breakpoint.</summary>
        public void ToggleBreakpoint(int id)
        {
            lock (_lock)
            {
                var bp = _breakpoints.Find(b => b.Id == id);
                if (bp != null)
                {
                    bp.Enabled = !bp.Enabled;
                    Save();
                    BreakpointsChanged?.Invoke(_breakpoints);
                }
            }
        }

        /// <summary>Retourne les breakpoints pour un fichier.</summary>
        public IReadOnlyList<BreakpointInfo> GetBreakpointsForFile(string filePath)
        {
            lock (_lock)
            {
                return _breakpoints
                    .Where(b => b.FilePath == filePath)
                    .OrderBy(b => b.Line)
                    .ToList();
            }
        }

        /// <summary>Retourne tous les breakpoints.</summary>
        public IReadOnlyList<BreakpointInfo> GetAllBreakpoints()
        {
            lock (_lock)
            {
                return _breakpoints.ToList();
            }
        }

        /// <summary>Met à jour le statut vérifié d'un breakpoint.</summary>
        public void SetVerified(int id, bool verified)
        {
            lock (_lock)
            {
                var bp = _breakpoints.Find(b => b.Id == id);
                if (bp != null)
                {
                    bp.Verified = verified;
                    Save();
                }
            }
        }

        /// <summary>Incrémente le compteur de hits.</summary>
        public void IncrementHitCount(int id)
        {
            lock (_lock)
            {
                var bp = _breakpoints.Find(b => b.Id == id);
                if (bp != null)
                {
                    bp.HitCount++;
                    // Pas de sauvegarde pour éviter trop d'I/O
                }
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_breakpointsPath)) return;
                var json = File.ReadAllText(_breakpointsPath);
                var loaded = JsonSerializer.Deserialize<List<BreakpointInfo>>(json);
                if (loaded != null)
                {
                    _breakpoints.Clear();
                    _breakpoints.AddRange(loaded);
                    _nextId = _breakpoints.Count > 0 ? _breakpoints.Max(b => b.Id) + 1 : 1;
                }
            }
            catch { /* Fichier corrompu : repart vide */ }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_breakpoints, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_breakpointsPath, json);
            }
            catch { /* Best-effort */ }
        }
    }
}
