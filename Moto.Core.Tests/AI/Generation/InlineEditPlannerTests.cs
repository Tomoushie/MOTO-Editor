// Moto.Core.Tests/AI/Generation/InlineEditPlannerTests.cs
// Ce qui protège le fichier de l'utilisateur quand un modèle « réécrit » : rien n'est proposé (ni a fortiori appliqué) si la réponse est
// coupée, abrégée, sans code, ambiguë ou si elle casserait le fichier. Et quand c'est proposé, le texte final est exactement celui attendu.
using Moto.Core.AI.Generation;
using Xunit;

namespace Moto.Core.Tests.AI.Generation;

public class InlineEditPlannerTests
{
    private const string Doc = "class A\n{\n    int x = 1;\n    int y = 2;\n}\n";

    private static InlineEditRequest Sel(string? selection, string doc = Doc, string path = "A.cs")
        => new() { DisplayPath = path, DocumentText = doc, Selection = selection, Instruction = "change" };

    private static string Reply(string code, string language = "csharp") => $"Voilà :\n```{language}\n{code}\n```";

    // ── Ce qui marche ───────────────────────────────────────────────────────

    [Fact]
    public void A_selection_is_replaced_in_place_and_nothing_else_moves()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), Reply("    int x = 5;"), "m");

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\n{\n    int x = 5;\n    int y = 2;\n}\n", outcome.Plan!.NewText);
        Assert.Equal(InlineEditScope.Selection, outcome.Plan.Scope);
        Assert.Equal(1, outcome.Plan.Diff.Added);
        Assert.Equal(1, outcome.Plan.Diff.Removed);
        Assert.Contains("+    int x = 5;", outcome.Plan.Diff.Unified);
        Assert.Equal("m", outcome.Model);
    }

    [Fact]
    public void Without_a_selection_the_whole_file_is_replaced_and_its_final_newline_is_kept()
    {
        var outcome = InlineEditPlanner.Plan(Sel(null), Reply("class A\n{\n    int x = 9;\n}"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\n{\n    int x = 9;\n}\n", outcome.Plan!.NewText);
        Assert.Equal(InlineEditScope.WholeFile, outcome.Plan.Scope);
    }

    [Fact]
    public void A_blank_selection_counts_as_no_selection()
        => Assert.Equal(InlineEditScope.WholeFile, Sel("  \n ").Scope);

    [Fact]
    public void A_whole_line_selection_keeps_its_line_break()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;\n"), Reply("    int x = 5;"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\n{\n    int x = 5;\n    int y = 2;\n}\n", outcome.Plan!.NewText);
    }

    [Fact]
    public void Windows_line_endings_of_the_document_are_kept()
    {
        var doc = Doc.Replace("\n", "\r\n");

        // L'éditeur peut donner la sélection avec « \n » seulement.
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;", doc), Reply("    int x = 5;"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\r\n{\r\n    int x = 5;\r\n    int y = 2;\r\n}\r\n", outcome.Plan!.NewText);
        Assert.Equal(1, outcome.Plan.Diff.Added);
    }

    [Fact]
    public void A_selection_can_grow_into_several_lines()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int y = 2;"), Reply("    int y = 2;\n    int z = 3;"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\n{\n    int x = 1;\n    int y = 2;\n    int z = 3;\n}\n", outcome.Plan!.NewText);
        Assert.Equal((1, 0), (outcome.Plan.Diff.Added, outcome.Plan.Diff.Removed));
    }

    // ── Refus : la réponse ne peut pas être appliquée ───────────────────────

    [Fact]
    public void A_reply_without_code_is_refused_and_quoted()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), "Je ne sais pas comment faire ça, peux-tu préciser ?");

        Assert.False(outcome.Succeeded);
        Assert.Contains("pas renvoyé de code", outcome.Problem);
        Assert.Contains("peux-tu préciser", outcome.Problem);
        Assert.Contains("Rien n'a été modifié", outcome.Problem);
    }

    [Fact]
    public void An_empty_reply_is_refused()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), "  ");

        Assert.False(outcome.Succeeded);
        Assert.Contains("rien répondu", outcome.Problem);
    }

    [Fact]
    public void A_truncated_reply_is_refused_instead_of_replacing_the_file_with_half_of_it()
    {
        var outcome = InlineEditPlanner.Plan(Sel(null), "```csharp\nclass A\n{\n    int x = 9;\n    void F()\n    {\n        Console.Wri");

        Assert.False(outcome.Succeeded);
        Assert.Contains("coupée", outcome.Problem);
    }

    [Theory]
    [InlineData("    int x = 5;\n    // ... le reste du code inchangé")]
    [InlineData("    int x = 5;\n    // ...")]
    [InlineData("    int x = 5;\n    ...")]
    [InlineData("    int x = 5;\n    /* ... existing code ... */")]
    [InlineData("    int x = 5;\n    // rest of the code unchanged")]
    [InlineData("    int x = 5;\n    # reste du fichier inchangé")]
    public void An_abbreviated_reply_is_refused(string code)
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), Reply(code));

        Assert.False(outcome.Succeeded);
        Assert.Contains("abrégé", outcome.Problem);
    }

    [Fact]
    public void An_ellipsis_that_was_already_in_the_original_is_not_an_abbreviation()
    {
        var doc = "def f():\n    ...\n\ndef g():\n    return 1\n";

        var outcome = InlineEditPlanner.Plan(Sel("def g():\n    return 1", doc, "a.py"), Reply("def g():\n    return 2", "python"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Contains("return 2", outcome.Plan!.NewText);
    }

    [Fact]
    public void A_comment_that_merely_ends_with_dots_is_not_an_abbreviation()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), Reply("    // on attend le serveur...\n    int x = 1000;"));

        Assert.True(outcome.Succeeded, outcome.Problem);
    }

    [Fact]
    public void A_selection_that_appears_twice_is_not_guessed()
    {
        var doc = "a();\nb();\na();\n";

        var outcome = InlineEditPlanner.Plan(Sel("a();", doc, "x.cs"), Reply("c();"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("plusieurs fois", outcome.Problem);
    }

    [Fact]
    public void A_selection_that_is_no_longer_in_the_file_is_refused()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int q = 0;"), Reply("    int q = 1;"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("ne correspond plus", outcome.Problem);
    }

    [Fact]
    public void An_identical_reply_changes_nothing()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), Reply("    int x = 1;"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("rien changé", outcome.Problem);
        Assert.Contains("chat", outcome.Problem); // une question posée dans le bandeau ressort souvent en « code identique » : on dit où la poser
    }

    [Fact]
    public void An_empty_block_never_replaces_a_whole_file()
    {
        var outcome = InlineEditPlanner.Plan(Sel(null), "```csharp\n```");

        Assert.False(outcome.Succeeded);
        Assert.Contains("vide", outcome.Problem);
    }

    [Fact]
    public void Code_that_would_unbalance_the_braces_is_refused()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), Reply("    void F()\n    {\n        int q = 1;"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("casserait", outcome.Problem);
        Assert.Contains("A.cs", outcome.Problem);
        Assert.DoesNotContain("Recopie", outcome.Problem); // le message est pour un humain, pas pour un modèle
    }

    [Fact]
    public void Json_that_stops_being_valid_is_refused()
    {
        var doc = "{\n  \"a\": 1,\n  \"b\": 2\n}\n";

        var outcome = InlineEditPlanner.Plan(Sel("  \"b\": 2", doc, "data.json"), Reply("  \"b\": ", "json"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("JSON", outcome.Problem);
    }

    // ── Avertissements : applicable, mais à regarder ────────────────────────

    [Fact]
    public void A_big_shrink_is_flagged()
    {
        var big = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"    int value{i} = {i};"));
        var doc = "class A\n{\n" + big + "\n}\n";

        var outcome = InlineEditPlanner.Plan(Sel(big, doc), Reply("    int only = 1;"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Contains(outcome.Plan!.Warnings, w => w.Contains("% de la taille"));
    }

    [Fact]
    public void Copying_the_neighbouring_line_is_flagged()
    {
        var doc = "class A\n{\n    int firstCounter = 1;\n    int secondCounter = 2;\n}\n";

        var outcome = InlineEditPlanner.Plan(Sel("    int secondCounter = 2;", doc), Reply("    int firstCounter = 1;\n    int secondCounter = 20;"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Contains(outcome.Plan!.Warnings, w => w.Contains("AUTOUR"));
    }

    [Fact]
    public void Short_neighbouring_lines_such_as_braces_are_not_flagged()
    {
        var outcome = InlineEditPlanner.Plan(Sel("two", "one\ntwo\nthree\n", "notes.txt"), Reply("two\nthree", "text"));

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.DoesNotContain(outcome.Plan!.Warnings, w => w.Contains("AUTOUR"));
    }

    [Fact]
    public void Several_blocks_use_the_first_and_say_so()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int x = 1;"), Reply("    int x = 5;") + "\nEt un exemple :\n```csharp\nnew A();\n```");

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Contains("int x = 5;", outcome.Plan!.NewText);
        Assert.Contains(outcome.Plan.Warnings, w => w.Contains("2 blocs"));
    }

    [Fact]
    public void An_empty_block_on_a_selection_deletes_it_but_says_so()
    {
        var outcome = InlineEditPlanner.Plan(Sel("    int y = 2;\n"), "```csharp\n```");

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Equal("class A\n{\n    int x = 1;\n}\n", outcome.Plan!.NewText);
        Assert.Contains(outcome.Plan.Warnings, w => w.Contains("SUPPRIMÉ"));
    }

    // ── Une question posée dans le bandeau d'édition (mesuré le 24/09 sur qwen2.5-coder:7b) ──

    private static InlineEditRequest WholeFile(string instruction)
        => new() { DisplayPath = "A.cs", DocumentText = Doc, Instruction = instruction };

    [Fact]
    public void A_question_answered_by_an_added_comment_is_flagged_as_a_probable_question()
    {
        var reply = Reply(Doc.TrimEnd('\n') + "\n\n// La capitale de la France est Paris.");

        var outcome = InlineEditPlanner.Plan(WholeFile("Quelle est la capitale de la France ?"), reply);

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.Contains(outcome.Plan!.Warnings, w => w.Contains("AJOUTER des commentaires"));
    }

    [Theory]
    [InlineData("Ajoute un commentaire au-dessus de la classe")]
    [InlineData("Documente cette classe")]
    [InlineData("Explique le code en notes dans le fichier")]
    public void Adding_only_comments_is_not_flagged_when_comments_were_asked_for(string instruction)
    {
        var reply = Reply("// Une classe de test.\n" + Doc.TrimEnd('\n'));

        var outcome = InlineEditPlanner.Plan(WholeFile(instruction), reply);

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.DoesNotContain(outcome.Plan!.Warnings, w => w.Contains("AJOUTER des commentaires"));
    }

    [Fact]
    public void Adding_real_code_is_not_flagged()
    {
        var reply = Reply("class A\n{\n    int x = 1;\n    int y = 2;\n    int z = 3;\n}");

        var outcome = InlineEditPlanner.Plan(WholeFile("ajoute z"), reply);

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.DoesNotContain(outcome.Plan!.Warnings, w => w.Contains("AJOUTER des commentaires"));
    }

    [Fact]
    public void Changing_code_while_adding_a_comment_is_not_flagged()
    {
        var reply = Reply("class A\n{\n    // valeur de départ\n    int x = 5;\n    int y = 2;\n}");

        var outcome = InlineEditPlanner.Plan(WholeFile("mets x à 5"), reply);

        Assert.True(outcome.Succeeded, outcome.Problem);
        Assert.DoesNotContain(outcome.Plan!.Warnings, w => w.Contains("AJOUTER des commentaires"));
    }

    // ── Localisation de la sélection ────────────────────────────────────────

    [Fact]
    public void Locate_finds_a_unique_selection()
    {
        var (index, problem) = InlineEditPlanner.Locate("0123456789ABCDEF", "ABC");

        Assert.Equal(10, index);
        Assert.Null(problem);
    }

    [Fact]
    public void Locate_refuses_overlapping_repetitions()
        => Assert.NotNull(InlineEditPlanner.Locate("aaa", "aa").Problem);
}
