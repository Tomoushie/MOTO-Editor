// Moto.Core/AI/Context/ContextEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Moto.Core.Settings;
using Moto.Core.Collab;
using Moto.Core.DevOps;
using Moto.Core.Logging;

namespace Moto.Core.AI.Context
{
    /// <summary>
    /// MOTO Context Engine : analyse le contexte et propose des suggestions proactives.
    /// Fonctionne en arrière-plan avec scan périodique configurable.
    /// </summary>
    public class ContextEngine : IDisposable
    {
        private readonly ContextAnalyzer _analyzer = new();
        private readonly Timer _scanTimer = new();
        private readonly List<ContextSuggestion> _dismissedSuggestions = new();
        private readonly string _historyPath;

        // ★ Item 71/74 : hook PresenceAware pour éviter l'IA lourde en forte présence.
        private readonly PresenceAwareSuggestionGate _presenceGate;

        // ★ CORRECTION 3 : hook FeatureFlag pour désactiver à distance
        private readonly FeatureFlagService _featureFlags;

        private string _lastAnalyzedFile;
        private long _lastFileHash;

        /// <summary>Déclenché quand de nouvelles suggestions sont prêtes.</summary>
        public event Action<ContextReport> SuggestionsReady;

        /// <summary>Suggestions actives (non ignorées).</summary>
        public IReadOnlyList<ContextSuggestion> ActiveSuggestions { get; private set; } = new List<ContextSuggestion>();

        // Constructeur étendu avec PresenceAwareSuggestionGate et FeatureFlagService
        public ContextEngine(string workspacePath,
                             PresenceAwareSuggestionGate? presenceGate = null,
                             FeatureFlagService? featureFlags = null)
        {
            _historyPath = Path.Combine(workspacePath, ".moto", "context_history.json");
            LoadDismissed();
            _scanTimer.Elapsed += OnScanTimer;

            _presenceGate = presenceGate ?? new PresenceAwareSuggestionGate(
                SettingsEngine.Shared,
                new StructuredLogCollector());

            _featureFlags = featureFlags ?? new FeatureFlagService(
                SettingsEngine.Shared,
                new StructuredLogCollector());
        }

        /// <summary>Démarre le scan automatique.</summary>
        public void Start(int intervalSeconds = 10)
        {
            if (!SettingsEngine.Shared.GetBool("context_engine_enabled")) return;
            _scanTimer.Interval = Math.Max(5, intervalSeconds) * 1000;
            _scanTimer.Start();
        }

        /// <summary>Arrête le scan automatique.</summary>
        public void Stop() => _scanTimer.Stop();

        /// <summary>Analyse manuelle d'un fichier.</summary>
        public ContextReport AnalyzeNow(string filePath, string workspace)
        {
            // ★ Hook PresenceAware : évite l'IA lourde si beaucoup de collaborateurs actifs
            if (!_presenceGate.ShouldRunHeavyAi())
            {
                return new ContextReport
                {
                    FilePath = filePath,
                    TotalIssues = 0,
                    AnalyzedAt = DateTime.UtcNow,
                    Suggestions = new List<ContextSuggestion>()
                };
            }

            // ★ CORRECTION 3 : Hook FeatureFlag : désactive le moteur si le flag est off
            if (!_featureFlags.IsEnabled("feature.context_engine"))
            {
                return new ContextReport
                {
                    FilePath = filePath,
                    TotalIssues = 0,
                    AnalyzedAt = DateTime.UtcNow,
                    Suggestions = new List<ContextSuggestion>()
                };
            }

            var report = _analyzer.Analyze(filePath, workspace);

            // Filtre les suggestions ignorées
            var active = report.Suggestions
                .Where(s => !_dismissedSuggestions.Any(d => d.Id == s.Id))
                .ToList();
            ActiveSuggestions = active;

            return new ContextReport
            {
                FilePath = report.FilePath,
                TotalIssues = active.Count,
                AnalyzedAt = report.AnalyzedAt,
                Suggestions = active
            };
        }

        /// <summary>Ignore une suggestion (ne plus la proposer).</summary>
        public void Dismiss(ContextSuggestion suggestion)
        {
            _dismissedSuggestions.Add(suggestion);
            ActiveSuggestions = ActiveSuggestions.Where(s => s.Id != suggestion.Id).ToList();
            SaveDismissed();
        }

        /// <summary>Applique une suggestion.</summary>
        public bool Apply(ContextSuggestion suggestion)
        {
            try
            {
                if (suggestion.IsInsertion)
                {
                    if (File.Exists(suggestion.FilePath))
                    {
                        var content = File.ReadAllText(suggestion.FilePath);
                        var lines = content.Split('\n').ToList();
                        if (suggestion.Line > 0 && suggestion.Line <= lines.Count)
                        {
                            lines.Insert(suggestion.Line - 1, suggestion.GeneratedContent);
                            File.WriteAllText(suggestion.FilePath, string.Join("\n", lines));
                            return true;
                        }
                    }
                }
                else
                {
                    var dir = Path.GetDirectoryName(suggestion.TargetPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(suggestion.TargetPath, suggestion.GeneratedContent);
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private void OnScanTimer(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lastAnalyzedFile)) return;
                var currentHash = ComputeFileHash(_lastAnalyzedFile);

                // Ne scanne que si le fichier a changé
                if (currentHash == _lastFileHash) return;
                _lastFileHash = currentHash;

                var workspace = Path.GetDirectoryName(_lastAnalyzedFile);
                var report = AnalyzeNow(_lastAnalyzedFile, workspace);

                if (report.Suggestions.Any())
                {
                    SuggestionsReady?.Invoke(report);
                }
            }
            catch
            {
                // Le scan ne doit jamais crasher l'éditeur.
            }
        }

        /// <summary>Définit le fichier actif à analyser.</summary>
        public void SetActiveFile(string filePath)
        {
            _lastAnalyzedFile = filePath;
            _lastFileHash = ComputeFileHash(filePath);
        }

        private long ComputeFileHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return 0;
                var info = new FileInfo(filePath);
                return info.Length * 31 + info.LastWriteTimeUtc.Ticks;
            }
            catch
            {
                return 0;
            }
        }

        private void LoadDismissed()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    var dismissed = System.Text.Json.JsonSerializer.Deserialize<List<ContextSuggestion>>(json);
                    if (dismissed != null)
                    {
                        _dismissedSuggestions.AddRange(dismissed);
                    }
                }
            }
            catch
            {
                // Historique corrompu : on repart vide.
            }
        }

        private void SaveDismissed()
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = System.Text.Json.JsonSerializer.Serialize(_dismissedSuggestions);
                File.WriteAllText(_historyPath, json);
            }
            catch
            {
                // Optionnel.
            }
        }

        public void Dispose()
        {
            _scanTimer?.Stop();
            _scanTimer?.Dispose();
        }
    }
}
