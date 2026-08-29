// Moto.Core.Tests/AI/Analytics/ProactiveAnalyticsEngineTests.cs
using System;
using System.IO;
using Moto.Core.AI.Analytics;
using Xunit;

namespace Moto.Core.Tests.AI.Analytics
{
    public class ProactiveAnalyticsEngineTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ProactiveAnalyticsEngine _engine;

        public ProactiveAnalyticsEngineTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _engine = new ProactiveAnalyticsEngine(_tempDir);
        }

        [Fact]
        public void Record_PaletteCommand_IncreasesExecutedCount()
        {
            _engine.Record(AnalyticsEventKind.PaletteCommandExecuted, "cmd.test");

            var top = _engine.GetTopPaletteCommands(5);
            Assert.NotEmpty(top);
            Assert.Contains(top, c => c.ItemId.Contains("cmd.test"));
        }

        [Fact]
        public void Record_Suggestion_TracksShownAndExecuted()
        {
            _engine.Record(AnalyticsEventKind.SuggestionShown, "sug.test");
            _engine.Record(AnalyticsEventKind.SuggestionExecuted, "sug.test");

            var suggestions = _engine.GetTopSuggestions(5);
            Assert.NotEmpty(suggestions);
        }

        [Fact]
        public void GetUnderperformingSuggestions_FiltersLowRate()
        {
            // 10 affichages, 0 exécutions → rate = 0
            for (int i = 0; i < 10; i++)
                _engine.Record(AnalyticsEventKind.SuggestionShown, "sug.bad");

            var under = _engine.GetUnderperformingSuggestions(5, maxRate: 0.1);
            Assert.Contains(under, s => s.ItemId.Contains("sug.bad"));
        }

        [Fact]
        public void IsDismissed_AfterRecordDismiss_ReturnsTrue()
        {
            _engine.RecordDismiss("sug.dismissed");
            Assert.True(_engine.IsDismissed("sug.dismissed"));
        }

        public void Dispose()
        {
            _engine.Dispose();
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { }
        }
    }
}
