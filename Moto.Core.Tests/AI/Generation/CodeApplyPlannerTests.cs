// Moto.Core.Tests/AI/Generation/CodeApplyPlannerTests.cs
// « Appliquer » dans le chat : où le planificateur pose un bloc de code dans le fichier affiché (sélection, fichier entier, membre existant,
// ajout au curseur ou en fin de classe), et quand il refuse plutôt que de deviner (bloc coupé, abrégé, identique, endroit inconnu).
using System.Text.RegularExpressions;
using Moto.Core.AI.Generation;
using Xunit;

namespace Moto.Core.Tests.AI.Generation;

public class CodeApplyPlannerTests
{
    private const string Doc =
        "using System;\n" +                                    // 1
        "\n" +                                                 // 2
        "namespace Demo;\n" +                                  // 3
        "\n" +                                                 // 4
        "public class Calc\n" +                                // 5
        "{\n" +                                                // 6
        "    /// <summary>Additionne.</summary>\n" +           // 7
        "    public int Add(int a, int b)\n" +                 // 8
        "    {\n" +                                            // 9
        "        return a + b;\n" +                            // 10
        "    }\n" +                                            // 11
        "\n" +                                                 // 12
        "    public int Sub(int a, int b) => a - b;\n" +       // 13
        "\n" +                                                 // 14
        "    public string Name { get; set; } = \"calc\";\n" + // 15
        "}\n";                                                 // 16

    private const string Mul = "public int Mul(int a, int b)\n{\n    return a * b;\n}";

    private static CodeApplyOutcome Plan(string code, string doc = Doc, string? selection = null, int? selectionStart = null, int? caret = null,
                                         string path = "src/Calc.cs", string? hint = null, bool complete = true)
        => CodeApplyPlanner.Plan(new CodeApplyRequest
        {
            DisplayPath = path,
            DocumentText = doc,
            Selection = selection,
            SelectionStart = selectionStart,
            CaretIndex = caret,
            Code = code,
            PathHint = hint,
            IsComplete = complete,
        });

    private static CodeApplyPlan Ok(CodeApplyOutcome outcome)
    {
        Assert.True(outcome.Succeeded, outcome.Problem);
        return outcome.Plan!;
    }

    private static int At(string text) => Doc.IndexOf(text, StringComparison.Ordinal);

    // ── Sélection ───────────────────────────────────────────────────────────

    [Fact]
    public void A_selection_is_replaced_and_the_code_takes_the_indentation_of_its_line()
    {
        var plan = Ok(Plan("{\n    var sum = a + b;\n    return sum;\n}", selection: "    {\n        return a + b;\n    }"));

        Assert.Equal(CodeApplyKind.ReplaceSelection, plan.Kind);
        Assert.Equal(Doc.Replace("    {\n        return a + b;\n    }", "    {\n        var sum = a + b;\n        return sum;\n    }"), plan.NewText);
        Assert.Equal("Remplacer la sélection (lignes 9 à 11)", plan.Description);
        Assert.Equal(9, plan.FirstLine);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void A_selection_that_starts_after_the_indentation_keeps_that_indentation()
    {
        var plan = Ok(Plan("return checked(a + b);", selection: "return a + b;"));

        Assert.Equal(Doc.Replace("        return a + b;", "        return checked(a + b);"), plan.NewText);
        Assert.Equal("Remplacer la sélection (ligne 10)", plan.Description);
    }

    [Fact]
    public void The_editor_position_tells_which_copy_of_a_repeated_selection_to_replace()
    {
        const string doc = "x = 1\ny = 2\nx = 1\n";

        Assert.False(Plan("x = 5", doc, selection: "x = 1", path: "notes.txt").Succeeded); // sans position : ambigu, on ne devine pas
        var plan = Ok(Plan("x = 5", doc, selection: "x = 1", selectionStart: 12, path: "notes.txt"));

        Assert.Equal("x = 1\ny = 2\nx = 5\n", plan.NewText);
        Assert.Equal("Remplacer la sélection (ligne 3)", plan.Description);
    }

    [Fact]
    public void A_selection_that_is_no_longer_in_the_file_is_refused()
    {
        var outcome = Plan("int z = 0;", selection: "int nope = 1;");

        Assert.False(outcome.Succeeded);
        Assert.Contains("ne correspond plus", outcome.Problem);
    }

    // ── Fichier entier ──────────────────────────────────────────────────────

    [Fact]
    public void A_block_that_repeats_most_of_the_file_replaces_the_whole_file()
    {
        var code = Doc.Replace("return a + b;", "return checked(a + b);").TrimEnd('\n');
        var plan = Ok(Plan(code));

        Assert.Equal(CodeApplyKind.ReplaceFile, plan.Kind);
        Assert.Equal(code + "\n", plan.NewText);
        Assert.Equal("Remplacer tout le fichier « Calc.cs »", plan.Description);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Replacing_the_whole_file_warns_when_the_block_drops_the_first_lines()
    {
        var code = Doc.Replace("using System;\n\n", string.Empty).Replace("return a + b;", "return checked(a + b);");
        var plan = Ok(Plan(code));

        Assert.Equal(CodeApplyKind.ReplaceFile, plan.Kind);
        Assert.Contains(plan.Warnings, w => w.Contains("ne reprend pas le début du fichier") && w.Contains("using System;"));
    }

    [Fact]
    public void Replacing_the_whole_file_warns_when_the_block_drops_the_last_lines()
    {
        var plan = Ok(Plan(Doc.Replace("\n    public string Name { get; set; } = \"calc\";\n", string.Empty)));

        Assert.Equal(CodeApplyKind.ReplaceFile, plan.Kind);
        Assert.Contains(plan.Warnings, w => w.Contains("ne reprend pas la fin du fichier"));
    }

    [Fact]
    public void An_empty_file_receives_the_block()
    {
        var plan = Ok(Plan("public class B\n{\n}", doc: "\n", path: "B.cs"));

        Assert.Equal(CodeApplyKind.ReplaceFile, plan.Kind);
        Assert.Equal("public class B\n{\n}\n", plan.NewText);
        Assert.Equal("Écrire ce code dans « B.cs », qui est vide", plan.Description);
    }

    // ── Membre existant (C#) ────────────────────────────────────────────────

    [Fact]
    public void An_existing_method_is_replaced_in_place_and_its_doc_comment_stays_when_the_block_has_none()
    {
        var plan = Ok(Plan("public int Add(int a, int b)\n{\n    return checked(a + b);\n}"));

        Assert.Equal(CodeApplyKind.ReplaceMember, plan.Kind);
        Assert.Equal(Doc.Replace("return a + b;", "return checked(a + b);"), plan.NewText);
        Assert.Equal("Remplacer la méthode « Add » (lignes 8 à 11)", plan.Description);
    }

    [Fact]
    public void A_doc_comment_in_the_block_replaces_the_one_above_the_method_instead_of_doubling_it()
    {
        var plan = Ok(Plan("/// <summary>Additionne sans débordement.</summary>\npublic int Add(int a, int b)\n{\n    return checked(a + b);\n}"));

        Assert.Equal(Doc.Replace("/// <summary>Additionne.</summary>", "/// <summary>Additionne sans débordement.</summary>")
                        .Replace("return a + b;", "return checked(a + b);"), plan.NewText);
        Assert.Equal("Remplacer la méthode « Add » (lignes 7 à 11)", plan.Description);
    }

    [Fact]
    public void An_attribute_above_the_method_is_not_lost_when_the_block_only_brings_a_doc_comment()
    {
        const string doc = "public class Calc\n{\n    /// <summary>Vieux.</summary>\n    [Obsolete]\n    public int Add(int a, int b)\n    {\n        return a + b;\n    }\n}\n";
        var plan = Ok(Plan("/// <summary>Neuf.</summary>\npublic int Add(int a, int b)\n{\n    return checked(a + b);\n}", doc));

        Assert.Contains("    [Obsolete]\n", plan.NewText);
        Assert.Contains("/// <summary>Neuf.</summary>", plan.NewText);
    }

    [Fact]
    public void A_method_whose_signature_changed_is_still_found_by_its_name()
    {
        var plan = Ok(Plan("public long Add(long a, long b)\n{\n    return a + b;\n}"));

        Assert.Equal(CodeApplyKind.ReplaceMember, plan.Kind);
        Assert.Equal(Doc.Replace("public int Add(int a, int b)", "public long Add(long a, long b)"), plan.NewText);
    }

    [Fact]
    public void With_overloads_the_method_is_added_next_to_them_with_a_warning_instead_of_guessing_which_one_to_replace()
    {
        const string doc = "public class Calc\n{\n    public int Add(int a, int b)\n    {\n        return a + b;\n    }\n\n"
                         + "    public double Add(double a, double b)\n    {\n        return a + b;\n    }\n}\n";
        var plan = Ok(Plan("public long Add(long a, long b)\n{\n    return a + b;\n}", doc));

        Assert.Equal(CodeApplyKind.AppendToType, plan.Kind);
        Assert.EndsWith("        return a + b;\n    }\n\n    public long Add(long a, long b)\n    {\n        return a + b;\n    }\n}\n", plan.NewText);
        Assert.Contains(plan.Warnings, w => w.Contains("« Add » existe déjà dans ce fichier (2 fois)"));
    }

    [Fact]
    public void A_method_written_with_its_brace_on_the_same_line_is_replaced_too()
    {
        const string doc = "public class Calc {\n    public int Add(int a, int b) {\n        return a + b;\n    }\n}\n";
        var plan = Ok(Plan("public int Add(int a, int b) {\n    return checked(a + b);\n}", doc));

        Assert.Equal("public class Calc {\n    public int Add(int a, int b) {\n        return checked(a + b);\n    }\n}\n", plan.NewText);
        Assert.Equal("Remplacer la méthode « Add » (lignes 2 à 4)", plan.Description);
    }

    [Fact]
    public void A_one_line_property_is_replaced_on_its_line()
    {
        var plan = Ok(Plan("public string Name { get; init; } = \"moto\";"));

        Assert.Equal(Doc.Replace("public string Name { get; set; } = \"calc\";", "public string Name { get; init; } = \"moto\";"), plan.NewText);
        Assert.Equal("Remplacer la propriété « Name » (ligne 15)", plan.Description);
    }

    [Fact]
    public void A_rewritten_class_replaces_the_class_and_keeps_the_usings_and_namespace_around_it()
    {
        var plan = Ok(Plan("public class Calc\n{\n    public int Add(int a, int b) => a + b;\n}"));

        Assert.Equal(CodeApplyKind.ReplaceMember, plan.Kind);
        Assert.Equal("using System;\n\nnamespace Demo;\n\npublic class Calc\n{\n    public int Add(int a, int b) => a + b;\n}\n", plan.NewText);
        Assert.Equal("Remplacer la classe « Calc » (lignes 5 à 16)", plan.Description);
        Assert.Contains(plan.Warnings, w => w.Contains("ne contient plus « Sub », « Name »") && w.Contains("ils disparaîtront"));
    }

    [Fact]
    public void A_whole_file_rewritten_inside_a_namespace_block_says_which_methods_would_disappear()
    {
        const string doc = "using System;\n\nnamespace Demo\n{\n    public class Calc\n    {\n        public int Add(int a, int b) => a + b;\n\n"
                         + "        public int Sub(int a, int b) => a - b;\n\n        public int Mul(int a, int b) => a * b;\n    }\n}\n";
        const string code = "using System;\n\nnamespace Demo\n{\n    public class Calc\n    {\n        public int Add(int a, int b) => checked(a + b);\n\n"
                          + "        public int Sub(int a, int b) => a - b;\n    }\n}";
        var plan = Ok(Plan(code, doc));

        Assert.Equal(code + "\n", plan.NewText);
        Assert.Equal("Remplacer le namespace « Demo » (lignes 3 à 13)", plan.Description);
        Assert.Contains(plan.Warnings, w => w.Contains("ne contient plus « Mul »") && w.Contains("il disparaîtra"));
    }

    [Fact]
    public void A_method_that_is_already_there_identically_changes_nothing()
    {
        var outcome = Plan("public int Add(int a, int b)\n{\n    return a + b;\n}");

        Assert.False(outcome.Succeeded);
        Assert.Contains("déjà dans le fichier", outcome.Problem);
    }

    // ── Ajout ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_method_without_a_cursor_goes_at_the_end_of_the_class()
    {
        var plan = Ok(Plan(Mul));

        Assert.Equal(CodeApplyKind.AppendToType, plan.Kind);
        Assert.Equal(Doc.Replace("= \"calc\";\n}", "= \"calc\";\n\n    public int Mul(int a, int b)\n    {\n        return a * b;\n    }\n}"), plan.NewText);
        Assert.Equal("Ajouter à la fin de la classe « Calc » (avant la ligne 16)", plan.Description);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void A_cursor_between_two_members_receives_the_new_method()
    {
        var plan = Ok(Plan(Mul, caret: At("    }\n\n") + "    }\n".Length)); // sur la ligne vide 12

        Assert.Equal(CodeApplyKind.InsertAtCursor, plan.Kind);
        Assert.Equal(Doc.Replace("    }\n\n    public int Sub", "    }\n\n    public int Mul(int a, int b)\n    {\n        return a * b;\n    }\n\n    public int Sub"),
                     plan.NewText);
        Assert.Equal("Insérer à la ligne 12, là où est ton curseur", plan.Description);
    }

    [Fact]
    public void A_cursor_inside_another_method_is_not_used_for_a_new_method()
    {
        var plan = Ok(Plan(Mul, caret: At("return a + b;")));

        Assert.Equal(CodeApplyKind.AppendToType, plan.Kind);
        Assert.Contains("    public int Mul(int a, int b)\n    {\n        return a * b;\n    }\n}\n", plan.NewText);
        Assert.Contains(plan.Warnings, w => w.Contains("ligne 10"));
    }

    [Fact]
    public void A_cursor_between_a_method_signature_and_its_brace_is_not_used()
    {
        var plan = Ok(Plan(Mul, caret: At("    {\n        return")));

        Assert.Equal(CodeApplyKind.AppendToType, plan.Kind);
        Assert.Contains(plan.Warnings, w => w.Contains("ligne 9"));
    }

    [Fact]
    public void A_method_without_an_access_modifier_is_not_slipped_inside_another_method()
    {
        var plan = Ok(Plan("int Twice(int x) => x * 2;", caret: At("return a + b;")));

        Assert.Equal(CodeApplyKind.AppendToType, plan.Kind);
        Assert.EndsWith("= \"calc\";\n\n    int Twice(int x) => x * 2;\n}\n", plan.NewText);
    }

    [Fact]
    public void Plain_instructions_go_where_the_cursor_is_with_the_indentation_of_that_line()
    {
        var plan = Ok(Plan("Console.WriteLine(a);", caret: At("return a + b;")));

        Assert.Equal(CodeApplyKind.InsertAtCursor, plan.Kind);
        Assert.Equal(Doc.Replace("        return a + b;", "        Console.WriteLine(a);\n        return a + b;"), plan.NewText);
        Assert.Equal("Insérer à la ligne 10, là où est ton curseur", plan.Description);
    }

    [Fact]
    public void Plain_instructions_without_a_cursor_are_not_placed_at_random()
    {
        var outcome = Plan("Console.WriteLine(a);");

        Assert.False(outcome.Succeeded);
        Assert.Contains("clique dans le fichier", outcome.Problem);
    }

    [Fact]
    public void After_a_line_that_opens_a_block_the_code_is_indented_one_step_further()
    {
        var plan = Ok(Plan("return 42", "def f():\n", caret: "def f():".Length, path: "tool.py"));

        Assert.Equal("def f():\n    return 42\n", plan.NewText);
        Assert.Equal("Insérer à la ligne 2, là où est ton curseur", plan.Description);
    }

    // ── « using » ───────────────────────────────────────────────────────────

    [Fact]
    public void Using_lines_at_the_top_of_the_block_go_to_the_top_of_the_file()
    {
        var plan = Ok(Plan("using System.Linq;\n\npublic int Max(int[] values)\n{\n    return values.Max();\n}"));

        Assert.Equal(CodeApplyKind.AppendToType, plan.Kind);
        Assert.StartsWith("using System;\nusing System.Linq;\n\nnamespace Demo;\n", plan.NewText);
        Assert.Contains("    public int Max(int[] values)\n    {\n        return values.Max();\n    }\n}\n", plan.NewText);
        Assert.Single(Regex.Matches(plan.NewText, "using System.Linq;"));
        Assert.EndsWith("— et ajouter « using System.Linq; » en haut du fichier", plan.Description);
    }

    [Fact]
    public void A_block_of_using_lines_only_adds_the_missing_ones()
    {
        var plan = Ok(Plan("using System;\nusing System.Text;"));

        Assert.Equal(CodeApplyKind.AddUsings, plan.Kind);
        Assert.StartsWith("using System;\nusing System.Text;\n\nnamespace Demo;", plan.NewText);
        Assert.False(Plan("using System;").Succeeded); // déjà là : rien à faire
    }

    // ── Refus et garde-fous communs ─────────────────────────────────────────

    [Fact]
    public void An_abridged_block_is_refused_because_it_would_erase_what_it_hides()
    {
        var outcome = Plan("public int Add(int a, int b)\n{\n    // ... reste du code inchangé\n}");

        Assert.False(outcome.Succeeded);
        Assert.Contains("abrégé", outcome.Problem);
    }

    [Fact]
    public void A_block_cut_off_by_the_end_of_the_reply_is_refused()
    {
        var outcome = Plan("public int Mul(int a, int b)\n{\n    return a", complete: false);

        Assert.False(outcome.Succeeded);
        Assert.Contains("incomplet", outcome.Problem);
    }

    [Fact]
    public void A_file_with_windows_line_endings_keeps_them()
    {
        var plan = Ok(Plan(Mul, doc: Doc.Replace("\n", "\r\n")));

        Assert.DoesNotMatch("(?<!\r)\n", plan.NewText);
        Assert.Contains("    public int Mul(int a, int b)\r\n    {\r\n", plan.NewText);
    }

    [Fact]
    public void A_block_written_for_another_file_is_flagged()
    {
        Assert.Contains(Ok(Plan(Mul, hint: "src/Other.cs")).Warnings, w => w.Contains("« Other.cs »") && w.Contains("« Calc.cs »"));
        Assert.Empty(Ok(Plan(Mul, hint: "Calc.cs")).Warnings);
    }
}
