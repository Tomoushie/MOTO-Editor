// Moto.Core/AI/Evolution/ProjectEvolutionEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Timers;
using Moto.Core.AI.Internal;

namespace Moto.Core.AI.Evolution
{
    public enum EvolutionKind { Improvement, Optimization, Refactor, Module, System }

    public class EvolutionSuggestion
    {
        public string Id { get; set; } = string.Empty;
        public EvolutionKind Kind { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    /// <summary>
    /// 27. AI Project Evolution : MOTO AI propose des améliorations,
    /// optimisations, refactors, modules et systèmes SANS que l'utilisateur
    /// ne demande rien. Analyse en arrière-plan, uniquement si le projet a changé.
    /// </summary>
    public class ProjectEvolutionEngine
    {
        private class History
        {
            public long LastHash { get; set; }
            public List<string> Accepted { get; set; } = new();
            public List<string> Rejected { get; set; } = new();
        }

        private readonly string _workspace;
        private readonly string _historyPath;
        private readonly ProjectUnderstandingEngine _understanding = new();
        private readonly CodeImprovementEngine _improvement = new();
        private readonly PatternDetectorEngine _patterns = new();
        private readonly HealthMetricsEngine _metrics = new();
        private readonly Timer _timer = new();
        private History _history = new();

        /// <summary>Déclenché quand de nouvelles évolutions sont prêtes.</summary>
        public event Action<List<EvolutionSuggestion>> EvolutionReady;

        public ProjectEvolutionEngine(string workspacePath)
        {
            _workspace = workspacePath;
            _historyPath = Path.Combine(workspacePath, ".moto", "evolution.json");
            LoadHistory();

            _timer.Elapsed += (s, e) => Tick();
        }

        public void Start(int intervalMs)
        {
            _timer.Interval = Math.Max(30_000, intervalMs);
            _timer.Start();
        }

        public void Stop() => _timer.Stop();

        public void Accept(EvolutionSuggestion s)
        {
            _history.Accepted.Add(s.Id);
            SaveHistory();
        }

        public void Reject(EvolutionSuggestion s)
        {
            _history.Rejected.Add(s.Id);
            SaveHistory();
        }

        // ------------------------------------------------------------------

        private void Tick()
        {
            try
            {
                var hash = ComputeHash();

                // Ne travaille que si le projet a changé : zéro CPU inutile.
                if (hash == _history.LastHash) return;

                _history.LastHash = hash;
                SaveHistory();

                var suggestions = AnalyzeNow();

                if (suggestions.Count > 0)
                {
                    EvolutionReady?.Invoke(suggestions);
                }
            }
            catch
            {
                // L'évolution ne doit jamais crasher l'éditeur.
            }
        }

        /// <summary>Analyse proactive complète.</summary>
        public List<EvolutionSuggestion> AnalyzeNow()
        {
            var map = _understanding.BuildMap(_workspace);
            var suggestions = new List<EvolutionSuggestion>();

            // 1. Améliorations / refactors classiques.
            foreach (var s in _improvement.Suggest(map).Take(5))
            {
                Add(suggestions, s.Title.Contains("Découper") ? EvolutionKind.Refactor : EvolutionKind.Improvement,
                    s.Title, s.Detail, 0.5);
            }

            // 2. Patterns manquants.
            foreach (var s in _patterns.Analyze(map).Suggestions.Take(5))
            {
                Add(suggestions, s.Title.Contains("Factory") ? EvolutionKind.Refactor : EvolutionKind.System,
                    s.Title, s.Detail, 0.6);
            }

            // 3. Systèmes/composants orphelins → propositions de complétion ECS.
            var systems = map.Symbols.Where(x => x.Kind == SymbolKind.System).Select(x => x.Name).ToList();
            var components = map.Symbols.Where(x => x.Kind == SymbolKind.Component).Select(x => x.Name).ToList();

            foreach (var sys in systems)
            {
                var baseName = sys.Replace("System", "");

                if (!components.Any(c => c.Contains(baseName)))
                {
                    Add(suggestions, EvolutionKind.System, $"Créer {baseName}Component",
                        $"Le système {sys} n'a pas de composant de données.", 0.7);
                }
            }

            foreach (var comp in components)
            {
                var baseName = comp.Replace("Component", "");

                if (!systems.Any(s => s.Contains(baseName)))
                {
                    Add(suggestions, EvolutionKind.System, $"Créer {baseName}System",
                        $"Le composant {comp} n'a pas de système logique.", 0.7);
                }
            }

            // 4. Optimisations via complexité.
            var metrics = _metrics.Analyze(map);

            if (metrics.ComplexityScore < 60)
            {
                Add(suggestions, EvolutionKind.Optimization, "Simplifier le code complexe",
                    $"Complexité moyenne élevée ({metrics.AvgCyclomatic:0.0} branches/méthode).", 0.8);
            }

            // Filtre l'historique (déjà accepté/refusé) + tri par pertinence.
            return suggestions
                .Where(s => !_history.Accepted.Contains(s.Id) && !_history.Rejected.Contains(s.Id))
                .OrderByDescending(s => s.Score)
                .Take(10)
                .ToList();
        }

        private void Add(List<EvolutionSuggestion> list, EvolutionKind kind, string title, string detail, double score)
        {
            var s = new EvolutionSuggestion
            {
                Id = $"{kind}:{title}",
                Kind = kind,
                Title = title,
                Detail = detail,
                Score = score
            };

            if (!list.Any(x => x.Id == s.Id))
            {
                list.Add(s);
            }
        }

        /// <summary>Empreinte légère du projet (tailles + dates de modification).</summary>
        private long ComputeHash()
        {
            long hash = 17;

            try
            {
                foreach (var file in Directory.GetFiles(_workspace, "*.cs", SearchOption.AllDirectories).Take(500))
                {
                    var info = new FileInfo(file);
                    hash = hash * 31 + info.Length;
                    hash = hash * 31 + info.LastWriteTimeUtc.Ticks;
                }
            }
            catch
            {
                // Lecture partielle acceptable.
            }

            return hash;
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    _history = JsonSerializer.Deserialize<History>(File.ReadAllText(_historyPath)) ?? new History();
                }
            }
            catch
            {
                _history = new History();
            }
        }

        private void SaveHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(_historyPath, JsonSerializer.Serialize(_history));
            }
            catch
            {
                // Optionnel.
            }
        }
    }
}
