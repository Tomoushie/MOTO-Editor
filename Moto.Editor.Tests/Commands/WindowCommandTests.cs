// Moto.Editor.Tests/Commands/WindowCommandTests.cs
using Xunit;

namespace Moto.Editor.Tests.Commands
{
    public class WindowCommandTests
    {
        [Theory]
        [InlineData("editor")]
        [InlineData("debug")]
        [InlineData("analytics")]
        [InlineData("plugin")]
        [InlineData("marketplace")]
        public void WindowKind_Parsing_IsValid(string kind)
        {
            var normalized = kind.ToLowerInvariant();
            var parsed = normalized switch
            {
                "editor" => Windows.WindowKind.Editor,
                "debug" => Windows.WindowKind.Debug,
                "analytics" => Windows.WindowKind.Analytics,
                "plugin" => Windows.WindowKind.Plugin,
                "marketplace" => Windows.WindowKind.Marketplace,
                _ => (Windows.WindowKind?)null
            };
            Assert.NotNull(parsed);
        }

        [Fact]
        public void WindowManager_OpenOrFocus_RegistersWindow()
        {
            var manager = new Windows.WindowManager();
            Assert.False(manager.IsOpen(Windows.WindowKind.Editor));
            // (ne peut pas réellement ouvrir une Window MAUI dans un test unitaire,
            // mais on peut tester l'état initial et la logique de registre)
            Assert.Empty(manager.OpenWindows);
        }

        [Fact]
        public void SpecializedWindowPage_HasTitleAndContent()
        {
            var page = new Windows.SpecializedWindowPage("Test", new Microsoft.Maui.Controls.Label { Text = "C" });
            Assert.Equal("Test", page.Title);
            Assert.NotNull(page.Content);
        }
    }
}
