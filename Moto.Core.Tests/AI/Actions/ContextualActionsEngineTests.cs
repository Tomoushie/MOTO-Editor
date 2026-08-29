// Moto.Core.Tests/AI/Actions/ContextualActionsEngineTests.cs
using Moto.Core.AI.Actions;
using Xunit;

namespace Moto.Core.Tests.AI.Actions
{
    public class ContextualActionsEngineTests
    {
        private readonly ContextualActionsEngine _engine = new();

        [Fact]
        public void GetActions_WithOpenDocument_IncludesEditorActions()
        {
            var context = new ActionContext
            {
                HasOpenDocument = true,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 1
            };

            var actions = _engine.GetActions(context);

            Assert.NotEmpty(actions);
            Assert.Contains(actions, a => a.Id == "editor.format" || a.Id == "ai.explain");
        }

        [Fact]
        public void GetActions_WithoutOpenDocument_ExcludesFormatAction()
        {
            var context = new ActionContext
            {
                HasOpenDocument = false,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 0
            };

            var actions = _engine.GetActions(context);

            Assert.DoesNotContain(actions, a => a.Id == "editor.format");
        }

        [Fact]
        public void GetActions_NotMaximized_ExcludesLayoutRestore()
        {
            var context = new ActionContext
            {
                HasOpenDocument = false,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 0
            };

            var actions = _engine.GetActions(context);

            Assert.DoesNotContain(actions, a => a.Id == "layout.restore");
        }

        [Fact]
        public void GetActions_Maximized_IncludesLayoutRestore()
        {
            var context = new ActionContext
            {
                HasOpenDocument = false,
                IsTerminalVisible = false,
                IsMaximized = true,
                OpenTabsCount = 0
            };

            var actions = _engine.GetActions(context);

            Assert.Contains(actions, a => a.Id == "layout.restore");
        }

        [Fact]
        public void GetActions_ReturnsAtMostSixActions()
        {
            var context = new ActionContext
            {
                HasOpenDocument = true,
                IsTerminalVisible = false,
                IsMaximized = true,
                HasErrors = true,
                OpenTabsCount = 5
            };

            var actions = _engine.GetActions(context);

            Assert.True(actions.Count <= 6);
        }

        [Fact]
        public void GetActions_TerminalHidden_IncludesTerminalOpen()
        {
            var context = new ActionContext
            {
                HasOpenDocument = false,
                IsTerminalVisible = false,
                IsMaximized = false,
                OpenTabsCount = 0
            };

            var actions = _engine.GetActions(context);

            Assert.Contains(actions, a => a.Id == "terminal.open" || a.Id == "terminal.test");
        }

        [Fact]
        public void GetActions_TerminalVisible_ExcludesTerminalOpen()
        {
            var context = new ActionContext
            {
                HasOpenDocument = false,
                IsTerminalVisible = true,
                IsMaximized = false,
                OpenTabsCount = 0
            };

            var actions = _engine.GetActions(context);

            Assert.DoesNotContain(actions, a => a.Id == "terminal.open");
        }
    }
}
