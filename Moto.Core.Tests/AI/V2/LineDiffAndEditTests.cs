// Moto.Core.Tests/AI/V2/LineDiffAndEditTests.cs
// Le diff montré à l'humain, la recherche du passage à remplacer, la lecture/écriture fidèle d'un fichier.
using System.Text;
using Moto.Core.AI.Autonomy.V2;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class LineDiffTests
{
    [Fact]
    public void Identical_texts_have_no_changes()
    {
        var diff = LineDiff.Compute("a\nb\nc\n", "a\nb\nc\n");
        Assert.False(diff.HasChanges);
        Assert.Equal(string.Empty, diff.Unified);
    }

    [Fact]
    public void Line_endings_alone_are_not_a_change()
    {
        Assert.False(LineDiff.Compute("a\r\nb\r\n", "a\nb\n").HasChanges);
    }

    [Fact]
    public void One_changed_line_gives_plus1_minus1_and_a_hunk()
    {
        var diff = LineDiff.Compute("un\ndeux\ntrois\n", "un\nDEUX\ntrois\n");
        Assert.Equal(1, diff.Added);
        Assert.Equal(1, diff.Removed);
        Assert.Contains("@@ -1,3 +1,3 @@", diff.Unified);
        Assert.Contains("-deux", diff.Unified);
        Assert.Contains("+DEUX", diff.Unified);
        Assert.Equal("+1 −1", diff.Summary);
    }

    [Fact]
    public void Addition_and_deletion_are_counted()
    {
        var add = LineDiff.Compute("a\nb\n", "a\nb\nc\nd\n");
        Assert.Equal((2, 0), (add.Added, add.Removed));

        var del = LineDiff.Compute("a\nb\nc\n", "a\n");
        Assert.Equal((0, 2), (del.Added, del.Removed));
    }

    [Fact]
    public void Distant_changes_make_two_hunks()
    {
        var old = string.Join("\n", Enumerable.Range(1, 40).Select(i => "ligne " + i)) + "\n";
        var neu = old.Replace("ligne 3\n", "ligne trois\n").Replace("ligne 38\n", "ligne trente-huit\n");
        var diff = LineDiff.Compute(old, neu);

        Assert.Equal(2, diff.Unified.Split('\n').Count(l => l.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Equal((2, 2), (diff.Added, diff.Removed));
    }

    [Fact]
    public void Creating_from_empty_counts_every_line_as_added()
    {
        var diff = LineDiff.Compute(string.Empty, "x\ny\nz");
        Assert.Equal((3, 0), (diff.Added, diff.Removed));
    }
}

public class EditMatcherTests
{
    private const string Sample =
        "class A\n{\n    void Foo()\n    {\n        Console.WriteLine(\"a\");\n    }\n\n    void Bar()\n    {\n        Console.WriteLine(\"b\");\n    }\n}\n";

    [Fact]
    public void Exact_unique_match_is_replaced()
    {
        var r = EditMatcher.Apply(Sample, "Console.WriteLine(\"a\");", "Console.WriteLine(\"A!\");", replaceAll: false);
        Assert.True(r.Success, r.Error);
        Assert.Contains("\"A!\"", r.NewContent);
        Assert.Contains("\"b\"", r.NewContent);
        Assert.Equal("exact", r.Strategy);
        Assert.Equal(5, r.FirstLine);
    }

    [Fact]
    public void Ambiguous_match_is_refused_with_line_numbers()
    {
        var r = EditMatcher.Apply(Sample, "Console.WriteLine(", "Log(", replaceAll: false);
        Assert.False(r.Success);
        Assert.Contains("2 fois", r.Error);
        Assert.Contains("5", r.Error);
        Assert.Contains("10", r.Error);
    }

    [Fact]
    public void Replace_all_replaces_every_occurrence()
    {
        var r = EditMatcher.Apply(Sample, "Console.WriteLine(", "Log(", replaceAll: true);
        Assert.True(r.Success, r.Error);
        Assert.Equal(2, r.Replacements);
        Assert.DoesNotContain("Console.WriteLine", r.NewContent);
    }

    [Fact]
    public void Missing_text_gives_a_hint_pointing_at_the_closest_line()
    {
        var r = EditMatcher.Apply(Sample, "Console.WriteLine(\"zzz\");", "x", replaceAll: false);
        Assert.False(r.Success);
        Assert.Contains("introuvable", r.Error);
        Assert.Contains("ligne", r.Error);
    }

    [Fact]
    public void First_line_found_but_rest_wrong_says_where_to_reread()
    {
        var r = EditMatcher.Apply(Sample, "void Foo()\n{\n    autre chose();\n}", "x", replaceAll: false);
        Assert.False(r.Success);
        Assert.Contains("ligne 3", r.Error);
    }

    [Fact]
    public void Line_numbers_copied_from_read_file_are_ignored()
    {
        var old = " 5 |         Console.WriteLine(\"a\");";
        var r = EditMatcher.Apply(Sample, old, " 5 |         Console.WriteLine(\"A\");", replaceAll: false);
        Assert.True(r.Success, r.Error);
        Assert.Contains("sans les numéros", r.Strategy);
        Assert.Contains("Console.WriteLine(\"A\");", r.NewContent);
        Assert.DoesNotContain(" 5 |", r.NewContent);
    }

    [Fact]
    public void Lost_indentation_is_tolerated_and_new_text_is_reindented()
    {
        // Le modèle a recopié sans l'indentation du fichier.
        var old = "Console.WriteLine(\"a\");\n}";
        var neu = "Console.WriteLine(\"a\");\nConsole.WriteLine(\"a2\");\n}";
        var r = EditMatcher.Apply(Sample, old, neu, replaceAll: false);

        Assert.True(r.Success, r.Error);
        Assert.Contains("indentation", r.Strategy);
        // Les lignes insérées reprennent l'indentation du fichier (8 espaces), la dernière accolade aussi.
        Assert.Contains("        Console.WriteLine(\"a2\");", r.NewContent);
        Assert.DoesNotContain("\nConsole.WriteLine(\"a2\")", r.NewContent);
    }

    [Fact]
    public void Trailing_spaces_are_tolerated()
    {
        var content = "alpha   \nbeta\ngamma\n";
        var r = EditMatcher.Apply(content, "alpha\nbeta", "ALPHA\nBETA", replaceAll: false);
        Assert.True(r.Success, r.Error);
        Assert.Equal("ALPHA\nBETA\ngamma\n", r.NewContent);
    }

    [Fact]
    public void Deleting_a_whole_line_leaves_no_blank_line()
    {
        var r = EditMatcher.Apply("a\nb\nc\n", "b\n", string.Empty, replaceAll: false);
        Assert.True(r.Success, r.Error);
        Assert.Equal("a\nc\n", r.NewContent);
    }

    [Fact]
    public void Replacing_a_line_keeps_the_next_line_separate()
    {
        var r = EditMatcher.Apply("a\nb\nc\n", "b\n", "B", replaceAll: false);
        Assert.True(r.Success, r.Error);
        Assert.Equal("a\nB\nc\n", r.NewContent);
    }

    [Fact]
    public void Empty_old_text_is_refused()
    {
        var r = EditMatcher.Apply("a\n", "  \n", "x", replaceAll: false);
        Assert.False(r.Success);
        Assert.Contains("old_text est vide", r.Error);
    }
}

public class TextFileTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    [Fact]
    public void Crlf_and_bom_survive_a_save()
    {
        var path = Path.Combine(_ws.Root, "a.cs");
        File.WriteAllBytes(path, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("un\r\ndeux\r\n")).ToArray());

        var file = TextFile.Load(path);
        Assert.True(file.HasBom);
        Assert.Equal("\r\n", file.NewLine);
        Assert.Equal("un\ndeux\n", file.Text);

        file.Save("un\nDEUX\n");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("un\r\nDEUX\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Lf_file_stays_lf_and_accents_are_kept()
    {
        var path = _ws.Write("b.txt", "é à ç\nligne\n");
        var file = TextFile.Load(path);
        file.Save("é à ç\nautre\n");
        Assert.Equal("é à ç\nautre\n", File.ReadAllText(path));
        Assert.DoesNotContain("\r", File.ReadAllText(path));
    }

    [Fact]
    public void Non_utf8_file_is_refused_rather_than_damaged()
    {
        var path = Path.Combine(_ws.Root, "latin.txt");
        File.WriteAllBytes(path, new byte[] { 0x63, 0x61, 0x66, 0xE9 }); // « café » en Latin-1
        Assert.Throws<InvalidDataException>(() => TextFile.Load(path));
    }

    [Fact]
    public void Binary_file_is_refused()
    {
        var path = Path.Combine(_ws.Root, "x.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 0, 3 });
        Assert.Throws<InvalidDataException>(() => TextFile.Load(path));
    }
}
