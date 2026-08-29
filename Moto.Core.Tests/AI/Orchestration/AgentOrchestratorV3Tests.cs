// Moto.Core.Tests/AI/Orchestration/AgentOrchestratorV3Tests.cs
using Moto.Core.AI.Actions;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Orchestration;
using Moto.Core.AI.Workspace;
using Xunit;

namespace Moto.Core.Tests.AI.Orchestration
{
    public class AgentOrchestratorV3Tests
    {
        [Fact]
        public void Scorer_MultiAgentSource_GetsBonus()
        {
            var scorer = new AgentScorer();
            var context = new ScoringContext
            {
                FilePath = "test.cs",
                Content = "public class Test {}",
                RecentActions = new string[0],
                HistoricalStats = new System.Collections.Generic.Dictionary<string, int>()
            };

            var singleSource = new CombinedSuggestion
            {
                Id = "a", Title = "Rename", Description = "", Source = "Cortex", Score = 0.5, Command = ""
            };
            var multiSource = new CombinedSuggestion
            {
                Id = "b", Title = "Rename", Description = "", Source = "Cortex + Actions", Score = 0.5, Command = ""
            };

            var scoreSingle = scorer.Score(singleSource, context);
            var scoreMulti = scorer.Score(multiSource, context);

            Assert.True(scoreMulti > scoreSingle, "Multi-agent source should get a bonus");
        }

        [Fact]
        public void Scorer_RanksByScoreDescending()
        {
            var scorer = new AgentScorer();
            var context = new ScoringContext
            {
                FilePath = "test.cs",
                Content = "public class Test {}",
                RecentActions = new string[0],
                HistoricalStats = new System.Collections.Generic.Dictionary<string, int>()
            };

            var suggestions = new[]
            {
                new CombinedSuggestion { Id = "a", Title = "low", Score = 0.1, Source = "X" },
                new CombinedSuggestion { Id = "b", Title = "high", Score = 0.9, Source = "X" },
                new CombinedSuggestion { Id = "c", Title = "mid", Score = 0.5, Source = "X" }
            };

            var ranked = scorer.ScoreAndRank(suggestions, context, topN: 3);

            Assert.Equal(3, ranked.Count);
            Assert.True(ranked[0].Score >= ranked[1].Score);
            Assert.True(ranked[1].Score >= ranked[2].Score);
        }
    }
}
