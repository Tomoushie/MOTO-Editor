// Moto.Core/AI/Workspace/AIWorkspace.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Internal;
using Moto.Core.Doc;
using Moto.Core.Platform;

namespace Moto.Core.AI.Workspace
{
    /// <summary>
    /// MOTO Editor v0.4 — AI Workspace : orchestrateur intelligent.
    /// Connecte Cortex Engine + Neural Mode + tous les moteurs existants.
    /// </summary>
    public class AIWorkspace : IDisposable
    {
        private readonly string _workspace;
        private readonly CortexEngine _cortex;
        private readonly NeuralMode _neural;
        private readonly DocEngine _doc;
        private readonly PlatformEngine _platform;
        private readonly ProjectUnderstandingEngine _understanding = new();

        public event Action<WorkspaceSuggestion> SuggestionReady;
        public event Action<string> StatusUpdated;

        public AIWorkspace(string workspace)
        {
            _workspace = workspace;
            _cortex = new CortexEngine(workspace);
            _neural = new NeuralMode(workspace, _cortex.GetStats() != null ? new CortexMemory(workspace) : null);
            _doc = new DocEngine(workspace);
            _platform = new PlatformEngine();
        }

        /// <summary>Initialise le workspace (entraînement, indexation).</summary>
        public async Task InitializeAsync()
        {
            StatusUpdated?.Invoke("Initialisation du workspace IA…");

            // Entraîne le Neural Mode
            await Task.Run(() => _neural.Train());

            StatusUpdated?.Invoke("Workspace IA prêt.");
        }

        /// <summary>Analyse le workspace et propose des suggestions proactives.</summary>
        public async Task<List<WorkspaceSuggestion>> AnalyzeAsync()
        {
            var suggestions = new List<WorkspaceSuggestion>();

            await Task.Run(() =>
            {
                var map = _understanding.BuildMap(_workspace);

                // 1. Navigation intelligente : fichiers importants
                var importantFiles = FindImportantFiles(map);
                foreach (var file in importantFiles.Take(5))
                {
                    suggestions.Add(new WorkspaceSuggestion
                    {
                        Kind = SuggestionKind.Navigate,
                        Title = $"Fichier important : {Path.GetFileName(file)}",
                        Description = "Ce fichier contient des éléments clés du projet.",
                        FilePath = file,
                        Confidence = 0.9
                    });
                }

                // 2. Fichiers cassés : erreurs, incohérences
                var brokenFiles = FindBrokenFiles(map);
                foreach (var file in brokenFiles.Take(5))
                {
                    suggestions.Add(new WorkspaceSuggestion
                    {
                        Kind = SuggestionKind.Fix,
                        Title = $"Fichier à corriger : {Path.GetFileName(file.Key)}",
                        Description = file.Value,
                        FilePath = file.Key,
                        Confidence = 0.85
                    });
                }

                // 3. Suggestions Cortex (basées sur le style appris)
                var cortexSuggestions = _cortex.GetSuggestions("", "");
                suggestions.AddRange(cortexSuggestions.Select(s => new WorkspaceSuggestion
                {
                    Kind = MapSuggestionKind(s.Kind),
                    Title = s.Title,
                    Description = s.Description,
                    FilePath = s.FilePath,
                    Line = s.Line,
                    Confidence = s.Confidence
                }));
            });

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .Take(20)
                .ToList();
        }

        /// <summary>Apprend du code écrit par l'utilisateur.</summary>
        public void LearnFromCode(string filePath, string content)
        {
            _cortex.LearnFromCode(filePath, content);
        }

        /// <summary>Génère du code via Neural Mode.</summary>
        public string GenerateCode(string intent, string context = "")
        {
            return _neural.Generate(intent, context);
        }

        /// <summary>Complète du code via Neural Mode.</summary>
        public string CompleteCode(string code, string context = "")
        {
            return _neural.Complete(code, context);
        }

        /// <summary>Statistiques globales du workspace.</summary>
        public WorkspaceStats GetStats()
        {
            var cortexStats = _cortex.GetStats();

            return new WorkspaceStats
            {
                CortexHabits = cortexStats.TotalHabits,
                CortexPatterns = cortexStats.TotalPatterns,
                CortexCorrections = cortexStats.TotalCorrections,
                LastActivity = cortexStats.LastActivity
            };
        }

        private List<string> FindImportantFiles(ProjectMap map)
        {
            return map.Symbols
                .Where(s => s.Kind == SymbolKind.Class || s.Kind == SymbolKind.System)
                .Select(s => s.FilePath)
                .Distinct()
                .ToList();
        }

        private Dictionary<string, string> FindBrokenFiles(ProjectMap map)
        {
            var broken = new Dictionary<string, string>();

            foreach (var issue in map.Issues)
            {
                if (!broken.ContainsKey(issue.FilePath))
                    broken[issue.FilePath] = issue.Message;
            }

            return broken;
        }

        private SuggestionKind MapSuggestionKind(Moto.Core.AI.Cortex.SuggestionKind kind)
        {
            return kind switch
            {
                Moto.Core.AI.Cortex.SuggestionKind.Rename => SuggestionKind.Rename,
                Moto.Core.AI.Cortex.SuggestionKind.Fix => SuggestionKind.Fix,
                Moto.Core.AI.Cortex.SuggestionKind.Refactor => SuggestionKind.Refactor,
                Moto.Core.AI.Cortex.SuggestionKind.Generate => SuggestionKind.Generate,
                Moto.Core.AI.Cortex.SuggestionKind.Document => SuggestionKind.Document,
                _ => SuggestionKind.Navigate
            };
        }

        public void Dispose()
        {
            _cortex?.Dispose();
            _doc?.Dispose();
            _platform?.Dispose();
        }
    }

    public enum SuggestionKind
    {
        Navigate,
        Fix,
        Rename,
        Refactor,
        Generate,
        Document
    }

    public class WorkspaceSuggestion
    {
        public SuggestionKind Kind { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public double Confidence { get; set; }
    }

    public class WorkspaceStats
    {
        public int CortexHabits { get; set; }
        public int CortexPatterns { get; set; }
        public int CortexCorrections { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
