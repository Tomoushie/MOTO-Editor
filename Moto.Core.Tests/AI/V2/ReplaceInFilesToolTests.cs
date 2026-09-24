// Moto.Core.Tests/AI/V2/ReplaceInFilesToolTests.cs
// replace_in_files : un renommage sur plusieurs fichiers en UNE opération — mots entiers seulement, rien d'écrit avant
// l'accord, tout ou rien à l'application, fins de ligne conservées, dossiers de build ignorés.
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class ReplaceInFilesToolTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    private AgentToolContext Ctx() => new(_ws.Root, "test", new RunBackup(_ws.Root, "run1", _ws.Backups));
    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    private void Shop()
    {
        _ws.Write("Program.cs", "var p = new PricingService();\nConsole.WriteLine(p.ComputeTax(100m));\n");
        _ws.Write("Services/InvoiceService.cs", "class InvoiceService\n{\n    decimal T(PricingService s) => s.ComputeTax(5m);\n}\n");
        _ws.Write("Services/PricingService.cs",
            "class PricingService\n{\n    public decimal ComputeTax(decimal a) => a * 0.2m;\n    public decimal With(decimal a) => a + ComputeTax(a);\n    public decimal ComputeTaxes(decimal a) => a;\n}\n");
    }

    [Fact]
    public async Task Renames_across_files_with_one_combined_diff_and_writes_nothing_before_approval()
    {
        Shop();
        var before = _ws.Read("Services/PricingService.cs");

        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTax"), ("new_text", "CalculateTax")), Ctx(), default);

        Assert.NotNull(prep.Change);
        Assert.Contains("4 occurrence(s) dans 3 fichier(s)", prep.Change!.Summary);
        Assert.Contains("── Program.cs", prep.Change.Details);
        Assert.Contains("── Services/InvoiceService.cs", prep.Change.Details);
        Assert.Contains("+    public decimal CalculateTax(decimal a)", prep.Change.Details);
        Assert.Equal(before, _ws.Read("Services/PricingService.cs")); // rien d'écrit avant l'accord
        Assert.Null(prep.Change.Path);
    }

    [Fact]
    public async Task Only_whole_words_are_replaced_by_default()
    {
        Shop();
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTax"), ("new_text", "CalculateTax")), Ctx(), default);
        var applied = await prep.Change!.ApplyAsync(default);

        Assert.False(applied.IsError, applied.Text);
        Assert.Contains("ComputeTaxes", _ws.Read("Services/PricingService.cs")); // pas touché : mot plus long
        Assert.Contains("public decimal CalculateTax(decimal a)", _ws.Read("Services/PricingService.cs"));
        Assert.Contains("a + CalculateTax(a)", _ws.Read("Services/PricingService.cs"));
        Assert.Contains("p.CalculateTax(100m)", _ws.Read("Program.cs"));
        Assert.Contains("s.CalculateTax(5m)", _ws.Read("Services/InvoiceService.cs"));
        Assert.Equal(3, applied.Changes!.Count);
        Assert.Contains(new ChangedFile("Program.cs", 1, 1, false), applied.Changes);
    }

    [Fact]
    public async Task The_result_says_what_remains_elsewhere_in_the_project()
    {
        Shop();
        _ws.Write("Docs/notes.md", "On appelle ComputeTax partout.\n");

        var prep = await new ReplaceInFilesToolV2().PrepareAsync(
            A(("old_text", "ComputeTax"), ("new_text", "CalculateTax"), ("file_glob", "*.cs")), Ctx(), default);
        var applied = await prep.Change!.ApplyAsync(default);

        Assert.Contains("apparaît encore dans : Docs/notes.md", applied.Text);
    }

    [Fact]
    public async Task Whole_word_can_be_turned_off()
    {
        Shop();
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(
            A(("old_text", "ComputeTax"), ("new_text", "CalculateTax"), ("whole_word", false)), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Contains("CalculateTaxes", _ws.Read("Services/PricingService.cs"));
    }

    [Fact]
    public async Task Case_is_respected()
    {
        _ws.Write("a.cs", "int Count; int count;\n");
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "Count"), ("new_text", "Total")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal("int Total; int count;\n", _ws.Read("a.cs"));
    }

    [Fact]
    public async Task Build_folders_and_git_are_never_touched()
    {
        Shop();
        _ws.Write("bin/Debug/Old.cs", "ComputeTax\n");
        _ws.Write("obj/x.cs", "ComputeTax\n");

        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTax"), ("new_text", "CalculateTax")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal("ComputeTax\n", _ws.Read("bin/Debug/Old.cs"));
        Assert.Equal("ComputeTax\n", _ws.Read("obj/x.cs"));
    }

    [Fact]
    public async Task The_scope_can_be_narrowed_with_path_and_file_glob()
    {
        Shop();
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(
            A(("old_text", "ComputeTax"), ("new_text", "CalculateTax"), ("path", "Services")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Contains("ComputeTax", _ws.Read("Program.cs")); // hors du dossier demandé
        Assert.Contains("CalculateTax", _ws.Read("Services/InvoiceService.cs"));

        var single = await new ReplaceInFilesToolV2().PrepareAsync(
            A(("old_text", "ComputeTax"), ("new_text", "CalculateTax"), ("path", "Program.cs")), Ctx(), default);
        Assert.Equal("Program.cs", single.Change!.Path);
    }

    [Fact]
    public async Task Line_endings_and_no_bom_are_kept()
    {
        var path = _ws.Write("crlf.cs", "a Foo b\nFoo\n", crlf: true);
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "Foo"), ("new_text", "Bar")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal("a Bar b\r\nBar\r\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task A_dollar_sign_in_the_replacement_is_literal()
    {
        _ws.Write("a.cs", "x = Old;\n");
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "Old"), ("new_text", "$1 + $&")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal("x = $1 + $&;\n", _ws.Read("a.cs"));
    }

    [Fact]
    public async Task Nothing_is_written_when_a_file_changed_while_waiting_for_confirmation()
    {
        Shop();
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTax"), ("new_text", "CalculateTax")), Ctx(), default);

        File.WriteAllText(Path.Combine(_ws.Root, "Services", "InvoiceService.cs"), "// modifié ailleurs\nComputeTax\n");

        var applied = await prep.Change!.ApplyAsync(default);

        Assert.True(applied.IsError);
        Assert.Contains("Services/InvoiceService.cs", applied.Text);
        Assert.Contains("Rien n'a été modifié", applied.Text);
        Assert.Contains("ComputeTax", _ws.Read("Program.cs")); // aucun fichier écrit
        Assert.Contains("ComputeTax", _ws.Read("Services/PricingService.cs"));
    }

    [Fact]
    public async Task Everything_is_undone_by_the_run_backup()
    {
        Shop();
        var original = _ws.Read("Program.cs");
        var ctx = Ctx();
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTax"), ("new_text", "CalculateTax")), ctx, default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal(3, ctx.Backup.Restore());
        Assert.Equal(original, _ws.Read("Program.cs"));
    }

    [Theory]
    [InlineData("", "x", "old_text")]
    [InlineData("A\nB", "x", "UNE ligne")]
    [InlineData("Same", "Same", "Aucun changement")]
    public async Task Bad_calls_are_rejected_with_a_helpful_message(string oldText, string newText, string expected)
    {
        _ws.Write("a.cs", "Same\n");
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", oldText), ("new_text", newText)), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains(expected, prep.Rejected!.Text);
    }

    [Fact]
    public async Task A_missing_new_text_is_rejected_but_an_empty_one_is_allowed()
    {
        _ws.Write("a.cs", "int Debug = 1; // Debug\n");
        var tool = new ReplaceInFilesToolV2();

        var missing = await tool.PrepareAsync(A(("old_text", "Debug")), Ctx(), default);
        Assert.Contains("new_text", missing.Rejected!.Text);

        var empty = await tool.PrepareAsync(A(("old_text", "// Debug"), ("new_text", "")), Ctx(), default);
        Assert.NotNull(empty.Change);
    }

    [Fact]
    public async Task No_match_says_how_to_find_the_right_name()
    {
        Shop();
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTaks"), ("new_text", "X")), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("Aucune occurrence", prep.Rejected!.Text);
        Assert.Contains("search_text", prep.Rejected.Text);
    }

    [Fact]
    public async Task A_path_outside_the_project_is_refused()
    {
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "a"), ("new_text", "b"), ("path", "../autre")), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("sort du dossier du projet", prep.Rejected!.Text);
    }

    [Fact]
    public async Task Binary_and_non_utf8_files_are_skipped_not_corrupted()
    {
        Shop();
        var latin1 = Path.Combine(_ws.Root, "old.txt");
        File.WriteAllBytes(latin1, new byte[] { 0x43, 0x6F, 0x6D, 0x70, 0x75, 0x74, 0x65, 0x54, 0x61, 0x78, 0x20, 0xE9 }); // « ComputeTax é » en Latin-1
        var bytes = File.ReadAllBytes(latin1);

        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "ComputeTax"), ("new_text", "CalculateTax")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal(bytes, File.ReadAllBytes(latin1));
        Assert.Contains("ignorés", prep.Change.Details);
    }

    [Fact]
    public async Task Too_many_changes_at_once_are_refused()
    {
        for (var i = 0; i < 45; i++) _ws.Write($"f{i}.cs", "Old\n");

        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "Old"), ("new_text", "New")), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("Trop de changements", prep.Rejected!.Text);
    }

    private static bool Finds(string oldText, bool wholeWord, string text)
        => ReplaceInFilesToolV2.BuildRegex(oldText, wholeWord).IsMatch(text);

    [Fact]
    public void Regex_is_word_bounded_only_when_the_text_starts_and_ends_with_a_word_character()
    {
        Assert.True(Finds("Foo", true, "a Foo b"));
        Assert.False(Finds("Foo", true, "FooBar"));
        Assert.False(Finds("Foo", true, "Foo_1"));
        Assert.False(Finds("Foo", true, "éFoo"));
        Assert.True(Finds("Foo", false, "FooBar"));
        Assert.True(Finds("$x", true, "a$xb")); // « $x » commence par « $ » : recherche simple, sans bornes
    }
}

public class ReplaceInFilesLoopTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    public void Dispose() => _ws.Dispose();

    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    private AgentRunRequest Req(string goal) => new()
    {
        Model = "fake", Goal = goal, WorkspaceRoot = _ws.Root, WriteAuditLog = false,
        BackupFolder = _ws.Backups, MaxSteps = 12, RequireVerification = false,
    };

    [Fact]
    public async Task A_rename_is_one_confirmation_and_every_touched_file_is_reported()
    {
        _ws.Write("A.cs", "class A { void Old() { } }\n");
        _ws.Write("B.cs", "class B { void M(A a) { a.Old(); } }\n");
        _fake.Calls(("search_text", A(("query", "Old"))))
             .Calls(("replace_in_files", A(("old_text", "Old"), ("new_text", "New"))))
             .Calls(("finish", A(("summary", "renommé"))));
        var approver = new AutoApprover();
        var events = new List<AgentEvent>();

        var result = await new AgentLoopV2(new Moto.Core.AI.Llm.OllamaChatClient("http://127.0.0.1:11434", _fake), approver)
            .RunAsync(Req("Renomme la méthode Old en New dans tout le projet."), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal(1, approver.Approved); // UNE confirmation pour les deux fichiers
        Assert.Equal(2, result.Changes.Count);
        Assert.Contains("void New()", _ws.Read("A.cs"));
        Assert.Contains("a.New()", _ws.Read("B.cs"));
        Assert.Contains(events, e => e.Kind == AgentEventKind.ToolCalled && e.Text.StartsWith("replace_in_files Old → New"));
    }

    [Fact]
    public async Task An_edit_of_a_file_touched_by_the_rename_in_the_same_message_is_ignored()
    {
        _ws.Write("A.cs", "class A { void Old() { } }\n");
        _fake.Calls(("replace_in_files", A(("old_text", "Old"), ("new_text", "New"))),
                    ("insert_lines", A(("path", "A.cs"), ("line", 1), ("text", "// x"))))
             .Calls(("finish", A(("summary", "fait"))));

        var result = await new AgentLoopV2(new Moto.Core.AI.Llm.OllamaChatClient("http://127.0.0.1:11434", _fake), new AutoApprover())
            .RunAsync(Req("Renomme la méthode Old en New."));

        Assert.Equal("class A { void New() { } }\n", _ws.Read("A.cs")); // pas d'insertion à un numéro de ligne devenu incertain
        var second = _fake.ChatRequests[1]["messages"]!.AsArray().Last(m => m!["role"]!.GetValue<string>() == "tool" && m["tool_name"]!.GetValue<string>() == "insert_lines")!;
        Assert.Contains("vient d'être modifié", second["content"]!.GetValue<string>());
        Assert.Equal(AgentOutcome.Completed, result.Outcome);
    }
}
