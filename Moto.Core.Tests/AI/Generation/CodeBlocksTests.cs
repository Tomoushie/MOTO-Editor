// Moto.Core.Tests/AI/Generation/CodeBlocksTests.cs
// L'extraction des blocs de code d'une réponse de modèle : ce qui décide si une réponse est APPLICABLE (bloc complet) ou seulement
// une réponse coupée, et quel nom de fichier le modèle a annoncé.
using Moto.Core.AI.Generation;
using Xunit;

namespace Moto.Core.Tests.AI.Generation;

public class CodeBlocksTests
{
    [Fact]
    public void Plain_text_has_no_block()
    {
        Assert.Empty(CodeBlocks.Extract("Je ne peux pas faire ça, désolé."));
        Assert.Empty(CodeBlocks.Extract(""));
        Assert.Empty(CodeBlocks.Extract(null));
        Assert.Null(CodeBlocks.First("rien"));
    }

    [Fact]
    public void A_fenced_block_gives_its_language_and_its_code()
    {
        var block = CodeBlocks.First("Voici :\n```csharp\nint x = 1;\nint y = 2;\n```\nVoilà.")!;

        Assert.Equal("csharp", block.Language);
        Assert.Equal("int x = 1;\nint y = 2;", block.Code);
        Assert.True(block.IsComplete);
        Assert.Null(block.PathHint);
    }

    [Fact]
    public void The_indentation_of_the_first_line_is_kept_and_blank_edges_are_dropped()
    {
        var block = CodeBlocks.First("```\n\n    if (a)\n        b();\n\n\n```")!;

        Assert.Equal("    if (a)\n        b();", block.Code);
        Assert.Equal(string.Empty, block.Language);
    }

    [Fact]
    public void A_block_that_is_never_closed_is_reported_incomplete()
    {
        // Une réponse coupée par la limite de jetons : le code s'arrête à mi-instruction.
        var block = CodeBlocks.First("```csharp\npublic void Run()\n{\n    Console.Wri")!;

        Assert.False(block.IsComplete);
        Assert.StartsWith("public void Run()", block.Code);
    }

    [Fact]
    public void Several_blocks_come_back_in_order()
    {
        var blocks = CodeBlocks.Extract("```python\nprint(1)\n```\ntexte\n```json\n{\"a\": 1}\n```");

        Assert.Equal(new[] { "python", "json" }, blocks.Select(b => b.Language));
        Assert.All(blocks, b => Assert.True(b.IsComplete));
    }

    [Fact]
    public void Triple_backticks_inside_a_line_are_inline_code_not_a_block()
        => Assert.Empty(CodeBlocks.Extract("Écris ```x``` pour voir, puis ```y``` aussi."));

    [Fact]
    public void A_longer_fence_can_contain_shorter_ones()
    {
        var block = CodeBlocks.First("````markdown\nAvant\n```csharp\nint x;\n```\nAprès\n````\nfin")!;

        Assert.True(block.IsComplete);
        Assert.Equal("Avant\n```csharp\nint x;\n```\nAprès", block.Code);
    }

    [Fact]
    public void Windows_line_endings_are_read_like_unix_ones()
    {
        var block = CodeBlocks.First("```js\r\nlet a = 1;\r\nlet b = 2;\r\n```\r\n")!;

        Assert.Equal("let a = 1;\nlet b = 2;", block.Code);
        Assert.True(block.IsComplete);
    }

    [Theory]
    [InlineData("```csharp Program.cs", "csharp", "Program.cs")]
    [InlineData("```csharp:src/Foo.cs", "csharp", "src/Foo.cs")]
    [InlineData("```csharp title=\"Foo.cs\"", "csharp", "Foo.cs")]
    [InlineData("```js path=web/app.js", "js", "web/app.js")]
    [InlineData("```Program.cs", "", "Program.cs")]
    [InlineData("```{.python}", "python", null)]
    [InlineData("```language-html", "html", null)]
    public void The_info_string_can_announce_a_file_name(string opening, string language, string? path)
    {
        var block = CodeBlocks.First(opening + "\ncontenu\n```")!;

        Assert.Equal(language, block.Language);
        Assert.Equal(path, block.PathHint);
    }

    [Theory]
    [InlineData("**Program.cs**", "Program.cs")]
    [InlineData("### src/Models/Foo.cs", "src/Models/Foo.cs")]
    [InlineData("Fichier : Foo.cs", "Foo.cs")]
    [InlineData("`Foo.cs`:", "Foo.cs")]
    [InlineData("1. Program.cs", "Program.cs")]
    [InlineData("Voici le fichier Program.cs :", null)]
    [InlineData("Voir https://example.com/a.html", null)]
    [InlineData("Version 1.2", null)]
    public void A_file_name_alone_on_the_line_before_the_block_is_a_hint(string before, string? path)
    {
        var block = CodeBlocks.First(before + "\n```csharp\nclass A { }\n```")!;

        Assert.Equal(path, block.PathHint);
    }

    [Fact]
    public void A_hint_is_not_carried_over_to_the_next_block()
    {
        var blocks = CodeBlocks.Extract("**A.cs**\n```csharp\nclass A { }\n```\n```csharp\nclass B { }\n```");

        Assert.Equal("A.cs", blocks[0].PathHint);
        Assert.Null(blocks[1].PathHint);
    }

    [Theory]
    [InlineData("csharp", ".cs")]
    [InlineData("CS", ".cs")]
    [InlineData("python", ".py")]
    [InlineData("powershell", ".ps1")]
    [InlineData("c++", ".cpp")]
    [InlineData("xaml", ".xaml")]
    [InlineData("markdown", ".md")]
    [InlineData("cobol", ".txt")]
    [InlineData("", ".txt")]
    [InlineData(null, ".txt")]
    public void Extension_follows_the_language_label(string? language, string extension)
        => Assert.Equal(extension, CodeBlocks.ExtensionFor(language));

    [Theory]
    [InlineData("src/Program.cs", "C#")]
    [InlineData("MainPage.xaml", "XAML")]
    [InlineData("Moto.Core.csproj", "MSBuild (XML)")]
    [InlineData("notes.md", "Markdown")]
    [InlineData("data.unknown", "texte")]
    [InlineData("", "texte")]
    public void Language_label_follows_the_extension(string path, string label)
        => Assert.Equal(label, CodeBlocks.LanguageLabel(path));
}
