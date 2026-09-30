// Moto.Core.Tests/Analytics/HomeStatsTests.cs
using System.Globalization;
using System.Linq;
using System.Threading;
using Moto.Core.Analytics;
using Xunit;

namespace Moto.Core.Tests.Analytics;

/// <summary>★ AJOUT (26/09) : rangée de chiffres de l'Accueil — vrais compteurs, rien à montrer = liste vide (rangée masquée).</summary>
public class HomeStatsTests
{
    [Fact]
    public void Build_NothingCounted_IsEmpty()
    {
        Assert.Empty(HomeStats.Build(new UsageStats(), learnedPatterns: 0));
        Assert.Empty(HomeStats.Build(null, learnedPatterns: 0));
    }

    [Fact]
    public void Build_LessThanAMinuteOpen_StillEmpty()
    {
        Assert.Empty(HomeStats.Build(new UsageStats { TotalWorkSeconds = 59 }, learnedPatterns: 0));
    }

    [Fact]
    public void Build_RealCounters_InOrder_ZerosLeftOut()
    {
        var usage = new UsageStats { TotalWorkSeconds = 55212, AiCallsTotal = 60, TokensConsumed = 4099, BuildsLaunched = 0 };

        var tiles = HomeStats.Build(usage, learnedPatterns: 0);

        Assert.Equal(new[] { "Temps dans MOTO", "Demandes à l'IA", "Tokens (estimés)" }, tiles.Select(t => t.Title));
        Assert.Equal(new[] { "15 h 20", "60", "≈ 4,1 k" }, tiles.Select(t => t.Value));
    }

    [Fact]
    public void Build_OnlyLearnedPatterns_OneTile()
    {
        var tile = Assert.Single(HomeStats.Build(new UsageStats(), learnedPatterns: 7));
        Assert.Equal(new HomeStatTile("7", "Motifs appris"), tile);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1_000, "1 k")]
    [InlineData(4_099, "4,1 k")]
    [InlineData(2_500_000, "2,5 M")]
    public void Compact_UsesFrenchDecimalComma(long n, string expected) => Assert.Equal(expected, HomeStats.Compact(n));

    [Fact]
    public void Compact_IgnoresWindowsLanguage()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("4,1 k", HomeStats.Compact(4_099));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    [Theory]
    [InlineData(60, "1 min")]
    [InlineData(3_599, "59 min")]
    [InlineData(3_600, "1 h 00")]
    [InlineData(90_000, "25 h 00")]
    public void FormatDuration_HoursThenMinutes(long seconds, string expected) => Assert.Equal(expected, HomeStats.FormatDuration(seconds));
}
