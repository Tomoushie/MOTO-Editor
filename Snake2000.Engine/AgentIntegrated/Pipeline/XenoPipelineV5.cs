// Snake2000.Engine/AgentIntegrated/Pipeline/XenoPipelineV5.cs
// Pipeline XENO-SSS∞ v5 avec boucle de feedback utilisateur.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Snake2000.Engine.AgentIntegrated.Learning;
using Snake2000.Engine.AgentIntegrated.Specialized;

namespace Snake2000.Engine.AgentIntegrated.Pipeline
{
    public sealed class XenoPipelineV5
    {
        private readonly ArchitectureAgent _architecture = new();
        private readonly PerformanceAgent _performance = new();
        private readonly SecurityAgent _security = new();
        private readonly TestingAgent _testing = new();
        private readonly UserFeedbackEngine _feedback;

        public XenoPipelineV5(UserFeedbackEngine feedback)
        {
            _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        }

        public async Task<PipelineResult> ExecuteAsync(AgentContext context)
        {
            var tasks = new[]
            {
                RunWithFeedback(_architecture, context),
                RunWithFeedback(_performance, context),
                RunWithFeedback(_security, context),
                RunWithFeedback(_testing, context)
            };

            await Task.WhenAll(tasks);

            var results = tasks.Select(t => t.Result)
                .Where(r => r.Findings.Count > 0)
                .OrderByDescending(r => r.Confidence * _feedback.GetConfidenceMultiplier(r.AgentName))
                .ToList();

            return new PipelineResult
            {
                Results = results,
                TotalFindings = results.Sum(r => r.Findings.Count),
                AverageConfidence = results.Count > 0 ? results.Average(r => r.Confidence) : 0
            };
        }

        private async Task<AgentResult> RunWithFeedback(ISpecializedAgent agent, AgentContext context)
        {
            var result = await agent.AnalyzeAsync(context);

            // Filtrer les findings déjà rejetés
            var filtered = result.Findings
                .Where(f => !_feedback.ShouldSuppress(result.AgentName, f))
                .ToList();

            // Appliquer le boost de confiance
            var boost = _feedback.GetConfidenceMultiplier(result.AgentName);

            return new AgentResult
            {
                AgentName = result.AgentName,
                Findings = filtered,
                Confidence = Math.Min(1.0, result.Confidence * boost)
            };
        }

        /// <summary>
        /// Enregistre le feedback utilisateur sur un finding.
        /// </summary>
        public void RecordFeedback(string agentName, string finding, FeedbackKind kind, string? modification = null)
        {
            _feedback.RecordFeedback(new AgentFeedback
            {
                AgentName = agentName,
                Finding = finding,
                Kind = kind,
                UserModification = modification
            });
        }
    }
}
