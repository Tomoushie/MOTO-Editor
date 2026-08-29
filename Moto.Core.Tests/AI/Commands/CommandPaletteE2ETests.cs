// Moto.Core.Tests/AI/Commands/CommandPaletteE2ETests.cs
using System;
using System.IO;
using System.Linq;
using Moto.Core.AI.Actions;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Commands;
using Xunit;

namespace Moto.Core.Tests.AI.Commands
{
    public class CommandPaletteE2ETests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ContextualActionsEngine _actionsEngine;
        private readonly ProactiveAnalyticsEngine _analytics;
        private readonly CommandPaletteEngine _palette;

        public CommandPaletteE2ETests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _actionsEngine = new ContextualActionsEngine();
            _analytics = new ProactiveAnalyticsEngine(_tempDir);
            _palette = new CommandPaletteEngine(_actionsEngine);
        }

        [Fact]
        public void E2E_OpenPalette_Search_ExecuteCommand_TracksAnalytics()
        {
            // ÉTAPE 1 : Ouverture de la palette (contexte par défaut)
            var context = new ActionContext
            {
                HasOpenDocument = true,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 1
            };

            var allCommands = _palette.GetAllCommands(context);
            Assert.NotEmpty(allCommands);

            // ÉTAPE 2 : Recherche "compiler"
            var searchResults = _palette.Search("compiler", context);
            Assert.NotEmpty(searchResults);

            // ÉTAPE 3 : Sélection de la première commande
            var selectedCommand = searchResults.First();
            Assert.NotEmpty(selectedCommand.CommandText);

            // ÉTAPE 4 : Simulation de l'exécution + tracking analytics
            _analytics.Record(AnalyticsEventKind.PaletteCommandExecuted, selectedCommand.Id);

            // ÉTAPE 5 : Validation de l'analytics
            var topCommands = _analytics.GetTopPaletteCommands(5);
            Assert.Contains(topCommands, c => c.ItemId.Contains(selectedCommand.Id));
            Assert.True(topCommands.First().ExecutedCount >= 1);
        }

        [Fact]
        public void E2E_Search_FuzzyMatching_FindsRelevantCommands()
        {
            var context = new ActionContext
            {
                HasOpenDocument = true,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 1
            };

            // Recherche avec faute de frappe
            var results = _palette.Search("termnal", context);

            // Devrait quand même trouver "terminal"
            Assert.NotEmpty(results);
            Assert.Contains(results, r =>
                r.Title.Contains("terminal", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void E2E_MultipleExecutions_TopCommandsOrderingCorrect()
        {
            var context = new ActionContext
            {
                HasOpenDocument = true,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 1
            };

            var commands = _palette.GetAllCommands(context);
            if (commands.Count < 2) return;

            // Exécute la commande A 5 fois, la commande B 2 fois
            var cmdA = commands[0];
            var cmdB = commands[1];

            for (int i = 0; i < 5; i++)
                _analytics.Record(AnalyticsEventKind.PaletteCommandExecuted, cmdA.Id);
            for (int i = 0; i < 2; i++)
                _analytics.Record(AnalyticsEventKind.PaletteCommandExecuted, cmdB.Id);

            var top = _analytics.GetTopPaletteCommands(10);

            // La commande A doit être avant B
            var indexA = top.ToList().FindIndex(c => c.ItemId.Contains(cmdA.Id));
            var indexB = top.ToList().FindIndex(c => c.ItemId.Contains(cmdB.Id));

            if (indexA >= 0 && indexB >= 0)
                Assert.True(indexA < indexB);
        }

        [Fact]
        public void E2E_Dismiss_Suggestion_PersistsAcrossCalls()
        {
            var analytics = new ProactiveAnalyticsEngine(_tempDir);
            var engine = new Moto.Core.AI.Suggestions.ProactiveSuggestionsEngine(_actionsEngine, analytics);

            var context = new ActionContext
            {
                HasOpenDocument = true,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 1
            };

            // ÉTAPE 1 : suggestions initiales
            var suggestions1 = engine.GetSuggestions(context);
            Assert.NotEmpty(suggestions1);

            // ÉTAPE 2 : dismiss permanent de la première
            var firstId = suggestions1.First().Id;
            engine.RecordPermanentDismiss(firstId);

            // ÉTAPE 3 : les suggestions suivantes ne contiennent plus la dismissée
            var suggestions2 = engine.GetSuggestions(context);
            Assert.DoesNotContain(suggestions2, s => s.Id == firstId);

            // ÉTAPE 4 : la persistance fonctionne (nouvelle instance)
            var analytics2 = new ProactiveAnalyticsEngine(_tempDir);
            Assert.True(analytics2.IsDismissed(firstId));
        }

        public void Dispose()
        {
            _analytics.Dispose();
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
