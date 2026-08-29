// Moto.Core.Tests/AI/Orchestration/AgentOrchestratorV3Tests.cs
using System;
using System.IO;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Orchestration;
using Xunit;

namespace Moto.Core.Tests.AI.Orchestration
{
    public class AgentOrchestratorV3Tests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ProactiveAnalyticsEngine _analytics;

        public AgentOrchestratorV3Tests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _analytics = new ProactiveAnalyticsEngine(_tempDir);
        }

        private static ActionContext MakeContext() => new()
        {
            HasOpenDocument = true,
            IsTerminalVisible = false,
            IsMaximized = false,
            CurrentFilePath = "test.cs",
            HasErrors = false,
            OpenTabsCount = 1
        };

        [Fact]
        public void E2E_Pipeline_RunsWithoutCortex_AndReturnsRankedSuggestions()
        {
            // Cortex null → pipeline doit fonctionner via Actions + Analytics + Scorer
            var orchestrator = new AgentOrchestratorV3(
                actions: new ContextualActionsEngine(),
                analytics: _analytics,
                cortex: null);

            var result = orchestrator.GetCombinedSuggestionsV3(
                "test.cs", "public class Foo {}", MakeContext());

            Assert.NotNull(result);
            // Le pipeline doit produire des suggestions (actions contextuelles)
            Assert.NotEmpty(result);
        }

        [Fact]
        public void E2E_Pipeline_RespectsTopNLimit()
        {
            var orchestrator = new AgentOrchestratorV3(
                new ContextualActionsEngine(), _analytics, cortex: null);

            var result = orchestrator.GetCombinedSuggestionsV3(
                "test.cs", "public class Foo {}", MakeContext());

            Assert.True(result.Count <= 8);
        }

        [Fact]
        public void E2E_Pipeline_ResultsAreSortedByScore()
        {
            var orchestrator = new AgentOrchestratorV3(
                new ContextualActionsEngine(), _analytics, cortex: null);

            var result = orchestrator.GetCombinedSuggestionsV3(
                "test.cs", "public class Foo {}", MakeContext());

            for (int i = 1; i < result.Count; i++)
                Assert.True(result[i - 1].Score >= result[i].Score);
        }

        public void Dispose()
        {
            _analytics.Dispose();
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }
}
