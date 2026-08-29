// Moto.Core.Tests/AI/Orchestration/AgentScorerTests.cs
using System.Collections.Generic;
using Moto.Core.AI.Orchestration;
using Xunit;

namespace Moto.Core.Tests.AI.Orchestration
{
    public class AgentScorerTests
    {
        private static ScoringContext MakeContext(string content = "public class Test {}")
            => new()
            {
                FilePath = "test.cs",
                Content = content,
                RecentActions = new string[0],
                HistoricalStats = new Dictionary<string, int>()
            };

        [Fact]
        public void Score_MultiAgentSource_GetsBonus()
        {
            var scorer = new AgentScorer();
            var ctx = MakeContext();

            var single = new CombinedSuggestion
            { Id = "a", Title = "Rename", Source = "Cortex", Score = 0.5 };
            var multi = new CombinedSuggestion
            { Id = "b", Title = "Rename", Source = "Cortex + Actions", Score = 0.5 };

            Assert.True(scorer.Score(multi, ctx) > scorer.Score(single, ctx));
        }

        [Fact]
        public void Score_IsClampedBetween0And1()
        {
            var scorer = new AgentScorer();
            var ctx = MakeContext();

            var s = new CombinedSuggestion { Id = "x", Title = "t", Source = "Cortex", Score = 1.0 };
            var score = scorer.Score(s, ctx);

            Assert.InRange(score, 0.0, 1.0);
        }

        [Fact]
        public void ScoreAndRank_OrdersDescendingAndRespectsTopN()
        {
            var scorer = new AgentScorer();
            var ctx = MakeContext();

            var suggestions = new[]
            {
                new CombinedSuggestion { Id="low",  Title="zzz", Source="X", Score=0.1 },
                new CombinedSuggestion { Id="high", Title="zzz", Source="X", Score=0.9 },
                new CombinedSuggestion { Id="mid",  Title="zzz", Source="X", Score=0.5 }
            };

            var ranked = scorer.ScoreAndRank(suggestions, ctx, topN: 2);

            Assert.Equal(2, ranked.Count);
            Assert.True(ranked[0].Score >= ranked[1].Score);
        }

        [Fact]
        public void Score_HistoryBonus_IncreasesScore()
        {
            var scorer = new AgentScorer();
            var ctx = new ScoringContext
            {
                FilePath = "f.cs",
                Content = "",
                HistoricalStats = new Dictionary<string, int> { ["s.id"] = 10 }
            };

            var withHistory = new CombinedSuggestion { Id = "s.id", Title = "t", Source = "X", Score = 0.3 };
            var noHistory = new CombinedSuggestion { Id = "s.other", Title = "t", Source = "X", Score = 0.3 };

            Assert.True(scorer.Score(withHistory, ctx) > scorer.Score(noHistory, ctx));
        }
    }
}
