// Snake2000.Engine/AgentIntegrated/Learning/UserFeedbackEngine.cs
// Capture le feedback utilisateur pour entraîner les agents spécialisés.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Snake2000.Engine.AgentIntegrated.Learning
{
    public enum FeedbackKind { Accepted, Rejected, Modified, Ignored }

    public sealed class AgentFeedback
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string AgentName { get; init; } = string.Empty;
        public string Finding { get; init; } = string.Empty;
        public FeedbackKind Kind { get; init; }
        public string? UserModification { get; init; }
        public double Confidence { get; init; }
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }

    public sealed class AgentLearningState
    {
        public string AgentName { get; init; } = string.Empty;
        public int TotalAccepted { get; set; }
        public int TotalRejected { get; set; }
        public int TotalModified { get; set; }
        public double ConfidenceBoost { get; set; } = 1.0;
        public List<string> LearnedPatterns { get; set; } = new();
        public List<string> AntiPatterns { get; set; } = new();
    }

    /// <summary>
    /// Moteur d'apprentissage par feedback utilisateur pour XENO-SSS∞ v5.
    /// Chaque agent ajuste sa confiance et ses patterns selon l'acceptation.
    /// </summary>
    public sealed class UserFeedbackEngine
    {
        private readonly string _statePath;
        private readonly Dictionary<string, AgentLearningState> _states = new();
        private readonly List<AgentFeedback> _history = new();
        private readonly object _lock = new();

        public event Action<AgentLearningState>? AgentLearned;

        public UserFeedbackEngine(string workspaceRoot)
        {
            var dir = Path.Combine(workspaceRoot, ".moto", "xeno");
            Directory.CreateDirectory(dir);
            _statePath = Path.Combine(dir, "feedback-state.json");
            Load();
        }

        public void RecordFeedback(AgentFeedback feedback)
        {
            lock (_lock)
            {
                _history.Add(feedback);

                if (!_states.TryGetValue(feedback.AgentName, out var state))
                {
                    state = new AgentLearningState { AgentName = feedback.AgentName };
                    _states[feedback.AgentName] = state;
                }

                switch (feedback.Kind)
                {
                    case FeedbackKind.Accepted:
                        state.TotalAccepted++;
                        state.ConfidenceBoost = Math.Min(2.0, state.ConfidenceBoost * 1.02);
                        if (!state.LearnedPatterns.Contains(feedback.Finding))
                            state.LearnedPatterns.Add(feedback.Finding);
                        break;

                    case FeedbackKind.Rejected:
                        state.TotalRejected++;
                        state.ConfidenceBoost = Math.Max(0.3, state.ConfidenceBoost * 0.95);
                        if (!state.AntiPatterns.Contains(feedback.Finding))
                            state.AntiPatterns.Add(feedback.Finding);
                        break;

                    case FeedbackKind.Modified:
                        state.TotalModified++;
                        // Le pattern est partiellement correct
                        state.ConfidenceBoost *= 0.98;
                        break;
                }

                Save();
                AgentLearned?.Invoke(state);
            }
        }

        public AgentLearningState? GetAgentState(string agentName)
        {
            lock (_lock)
            {
                return _states.TryGetValue(agentName, out var state) ? state : null;
            }
        }

        public IReadOnlyList<AgentLearningState> GetAllStates()
        {
            lock (_lock)
            {
                return _states.Values.ToList();
            }
        }

        public double GetConfidenceMultiplier(string agentName)
        {
            var state = GetAgentState(agentName);
            return state?.ConfidenceBoost ?? 1.0;
        }

        public bool ShouldSuppress(string agentName, string finding)
        {
            var state = GetAgentState(agentName);
            return state?.AntiPatterns.Contains(finding) ?? false;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    var json = File.ReadAllText(_statePath);
                    var data = JsonSerializer.Deserialize<FeedbackSnapshot>(json);
                    if (data != null)
                    {
                        foreach (var state in data.States)
                            _states[state.AgentName] = state;
                        _history.AddRange(data.History);
                    }
                }
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                var snapshot = new FeedbackSnapshot
                {
                    States = _states.Values.ToList(),
                    History = _history.TakeLast(1000).ToList()
                };
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statePath, json);
            }
            catch { }
        }

        private sealed class FeedbackSnapshot
        {
            public List<AgentLearningState> States { get; set; } = new();
            public List<AgentFeedback> History { get; set; } = new();
        }
    }
}
