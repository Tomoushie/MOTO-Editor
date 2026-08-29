// Moto.Core/Analytics/GlobalUsageEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Moto.Core.Analytics
{
    public sealed class UsageStats
    {
        public DateTime FirstLaunchUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

        // Fichiers
        public long FilesCreated { get; set; }
        public long FilesModified { get; set; }
        public long FilesDeleted { get; set; }

        // Lignes
        public long LinesCreated { get; set; }
        public long LinesDeleted { get; set; }

        // Temps
        public long TotalWorkSeconds { get; set; }

        // IA
        public long AiCallsTotal { get; set; }
        public Dictionary<string, long> AiCallsByModel { get; set; } = new();
        public long TokensConsumed { get; set; }

        // Exports
        public Dictionary<string, long> ExportsByFormat { get; set; } = new();
        public Dictionary<string, long> ExportsByExtension { get; set; } = new();

        // Divers
        public long BuildsLaunched { get; set; }
        public long DebugSessionsStarted { get; set; }
    }

    /// <summary>
    /// Moteur d'usage global persistant : statistiques depuis l'installation.
    /// Thread-safe, sauvegarde debouncée.
    /// </summary>
    public sealed class GlobalUsageEngine : IDisposable
    {
        private readonly string _path;
        private readonly UsageStats _stats;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _saveGate = new(1, 1);
        private DateTime? _sessionStart;
        private CancellationTokenSource? _sessionCts;

        public GlobalUsageEngine(string appDataRoot)
        {
            var dir = Path.Combine(appDataRoot, "MotoEditor");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "global-usage.json");
            _stats = Load() ?? new UsageStats { FirstLaunchUtc = DateTime.UtcNow };
        }

        public UsageStats Snapshot()
        {
            lock (_lock) { return JsonSerializer.Deserialize<UsageStats>(
                JsonSerializer.Serialize(_stats))!; }
        }

        // ── Fichiers ──
        public void RecordFileCreated() { Mutate(s => s.FilesCreated++); }
        public void RecordFileModified() { Mutate(s => s.FilesModified++); }
        public void RecordFileDeleted() { Mutate(s => s.FilesDeleted++); }

        // ── Lignes ──
        public void RecordLines(long created, long deleted)
            => Mutate(s => { s.LinesCreated += created; s.LinesDeleted += deleted; });

        // ── IA ──
        public void RecordAiCall(string model, int tokens)
        {
            Mutate(s =>
            {
                s.AiCallsTotal++;
                if (!s.AiCallsByModel.ContainsKey(model)) s.AiCallsByModel[model] = 0;
                s.AiCallsByModel[model]++;
                s.TokensConsumed += tokens;
            });
        }

        // ── Exports ──
        public void RecordExport(string format, string extension)
        {
            Mutate(s =>
            {
                if (!s.ExportsByFormat.ContainsKey(format)) s.ExportsByFormat[format] = 0;
                s.ExportsByFormat[format]++;
                if (!s.ExportsByExtension.ContainsKey(extension)) s.ExportsByExtension[extension] = 0;
                s.ExportsByExtension[extension]++;
            });
        }

        // ── Build / Debug ──
        public void RecordBuild() => Mutate(s => s.BuildsLaunched++);
        public void RecordDebugSession() => Mutate(s => s.DebugSessionsStarted++);

        // ── Session (temps de travail) ──
        public void StartSession()
        {
            _sessionStart = DateTime.UtcNow;
            _sessionCts = new CancellationTokenSource();
        }

        public void StopSession()
        {
            if (_sessionStart == null) return;
            var elapsed = (long)(DateTime.UtcNow - _sessionStart.Value).TotalSeconds;
            Mutate(s => s.TotalWorkSeconds += elapsed);
            _sessionStart = null;
            _sessionCts?.Cancel();
        }

        // ── Rapport formaté ──
        public string GetReport()
        {
            var s = Snapshot();
            var topModel = s.AiCallsByModel.OrderByDescending(kv => kv.Value).FirstOrDefault();
            var topFormat = s.ExportsByFormat.OrderByDescending(kv => kv.Value).FirstOrDefault();
            return $"📊 Depuis {s.FirstLaunchUtc:yyyy-MM-dd} · " +
                   $"Fichiers : +{s.FilesCreated} ~{s.FilesModified} -{s.FilesDeleted} · " +
                   $"Lignes : +{s.LinesCreated} -{s.LinesDeleted} · " +
                   $"Travail : {FormatDuration(s.TotalWorkSeconds)} · " +
                   $"IA : {s.AiCallsTotal} appels · {s.TokensConsumed:N0} tokens · " +
                   $"Top modèle : {topModel.Key ?? "n/a"} ({topModel.Value}) · " +
                   $"Top export : {topFormat.Key ?? "n/a"} ({topFormat.Value})";
        }

        private static string FormatDuration(long seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h{ts.Minutes:D2}m"
                : $"{ts.Minutes}m{ts.Seconds:D2}s";
        }

        private void Mutate(Action<UsageStats> mutate)
        {
            lock (_lock)
            {
                mutate(_stats);
                _stats.LastActivityUtc = DateTime.UtcNow;
            }
            _ = SaveAsync();
        }

        private UsageStats? Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                return JsonSerializer.Deserialize<UsageStats>(File.ReadAllText(_path));
            }
            catch { return null; }
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            await _saveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                UsageStats snapshot;
                lock (_lock)
                {
                    snapshot = JsonSerializer.Deserialize<UsageStats>(JsonSerializer.Serialize(_stats))!;
                }
                await File.WriteAllTextAsync(_path,
                    JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }))
                    .ConfigureAwait(false);
            }
            catch { /* best-effort */ }
            finally { _saveGate.Release(); }
        }

        public void Dispose()
        {
            StopSession();
            _sessionCts?.Dispose();
            _saveGate.Dispose();
        }
    }
}
