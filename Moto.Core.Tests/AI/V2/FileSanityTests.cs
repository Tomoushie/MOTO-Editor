// Moto.Core.Tests/AI/V2/FileSanityTests.cs
// « Ne casse pas un fichier valide » : accolades C#, JSON et XML vérifiés AVANT la confirmation, et reprise de la boucle
// quand le modèle « s'emballe » (Ollama interrompt la génération : token repeat limit).
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class FileSanityTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    private AgentToolContext Ctx() => new(_ws.Root, "test", new RunBackup(_ws.Root, "run1", _ws.Backups));
    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    private const string Balanced = "namespace N\n{\n    public class C\n    {\n        public void M()\n        {\n            Console.WriteLine(\"x\");\n        }\n    }\n}\n";

    [Fact]
    public void A_balanced_new_csharp_file_is_accepted()
        => Assert.Null(FileSanity.Check("C.cs", null, Balanced));

    [Fact]
    public void A_csharp_file_cut_in_the_middle_of_a_statement_is_refused_with_a_hint()
    {
        var cut = "namespace N {\n    public class C {\n        public void M() {\n            Console.WriteLine($\"";
        var message = FileSanity.Check("C.cs", null, cut);

        Assert.NotNull(message);
        Assert.Contains("accolade", message);
        Assert.Contains("tronqué", message);
        Assert.Contains("\\\"", message); // le rappel sur l'échappement des guillemets dans le JSON
    }

    [Fact]
    public void Removing_a_closing_brace_from_a_valid_file_is_refused()
    {
        var broken = Balanced.Replace("        }\n    }\n}\n", "        \n    }\n}\n");
        Assert.Contains("accolade", FileSanity.Check("C.cs", Balanced, broken)!);
    }

    [Fact]
    public void A_file_that_was_already_broken_stays_editable()
    {
        var already = Balanced.TrimEnd().TrimEnd('}');
        Assert.Null(FileSanity.Check("C.cs", already, already + "// commentaire\n"));
    }

    [Fact]
    public void Braces_inside_strings_and_comments_are_not_counted()
    {
        var code = "class C\n{\n    string s = \"{{{\";\n    // }}}\n    char c = '{';\n    string v = @\"{\n\";\n}\n";
        Assert.Null(FileSanity.Check("C.cs", null, code));
    }

    [Fact]
    public void Other_languages_are_never_checked()
    {
        Assert.Null(FileSanity.Check("notes.txt", null, "{ pas fermé"));
        Assert.Null(FileSanity.Check("script.js", null, "function f() {"));
    }

    [Theory]
    [InlineData("{\"a\": 1, \"b\": [1, 2]}", true)]
    [InlineData("{\"a\": 1, // commentaire\n \"b\": 2,}", true)] // tolérant : commentaires et virgule finale (tsconfig, launchSettings)
    [InlineData("{\"a\": 1, \"b\": [1, 2", false)]
    [InlineData("{\"a\": }", false)]
    public void Json_must_stay_readable(string json, bool valid)
        => Assert.Equal(valid, FileSanity.Check("data.json", null, json) is null);

    [Fact]
    public void Xml_and_xaml_must_stay_well_formed_and_a_double_hyphen_in_a_comment_is_caught()
    {
        Assert.Null(FileSanity.Check("Page.xaml", null, "<Page>\n  <!-- un commentaire — propre -->\n  <Grid />\n</Page>"));
        Assert.Contains("XML valide", FileSanity.Check("Page.xaml", null, "<Page>\n  <Grid />\n"));
        Assert.Contains("XML valide", FileSanity.Check("Page.xaml", null, "<Page>\n  <!-- mauvais -- commentaire -->\n</Page>"));
        Assert.Contains("XML valide", FileSanity.Check("App.csproj", "<Project />", "<Project><PropertyGroup></Project>"));
        Assert.Null(FileSanity.Check("App.csproj", "<Project><Broken>", "<Project><Broken><More>")); // déjà invalide avant
    }

    // ── Dans les outils ─────────────────────────────────────────────────────

    [Fact]
    public async Task Write_file_refuses_a_truncated_csharp_file_before_any_confirmation()
    {
        var cut = "namespace N {\n    public class C {\n        void M() {\n            Console.WriteLine($\"";
        var prep = await new WriteFileToolV2().PrepareAsync(A(("path", "C.cs"), ("content", cut)), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("accolade", prep.Rejected!.Text);
        Assert.False(_ws.Exists("C.cs"));
    }

    [Fact]
    public async Task Edit_file_refuses_an_edit_that_unbalances_a_valid_file()
    {
        _ws.Write("C.cs", Balanced);
        var prep = await new EditFileToolV2().PrepareAsync(
            A(("path", "C.cs"), ("old_text", "Console.WriteLine(\"x\");\n        }"), ("new_text", "Console.WriteLine(\"x\");")), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("accolade", prep.Rejected!.Text);
        Assert.Equal(Balanced, _ws.Read("C.cs"));
    }

    [Fact]
    public async Task Edit_file_accepts_a_normal_edit()
    {
        _ws.Write("C.cs", Balanced);
        var prep = await new EditFileToolV2().PrepareAsync(
            A(("path", "C.cs"), ("old_text", "Console.WriteLine(\"x\");"), ("new_text", "Console.WriteLine(\"y\");")), Ctx(), default);

        Assert.NotNull(prep.Change);
    }

    [Fact]
    public async Task Replace_in_files_refuses_when_a_replacement_would_unbalance_a_file()
    {
        _ws.Write("C.cs", Balanced);
        var prep = await new ReplaceInFilesToolV2().PrepareAsync(A(("old_text", "M"), ("new_text", "M() {")), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("accolade", prep.Rejected!.Text);
    }
}

public class GlitchRetryTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    public void Dispose() => _ws.Dispose();

    private const string Glitch = "{\"error\":\"prediction aborted, token repeat limit reached\"}\n";

    private AgentRunRequest Req() => new()
    {
        Model = "fake", Goal = "Lis a.txt.", WorkspaceRoot = _ws.Root, WriteAuditLog = false,
        BackupFolder = _ws.Backups, MaxSteps = 6, RequireVerification = false, ToolMode = AgentToolMode.Native,
    };

    private AgentLoopV2 Loop() => new(new OllamaChatClient("http://127.0.0.1:11434", _fake), new AutoApprover());

    [Fact]
    public async Task A_runaway_generation_is_retried_at_a_higher_temperature_and_the_run_goes_on()
    {
        _fake.Enqueue(Glitch).Calls(("finish", FakeOllamaHandler.Args(("summary", "fait"))));
        var events = new List<AgentEvent>();

        var result = await Loop().RunAsync(Req(), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.ModelCalls); // l'essai interrompu n'est pas compté
        Assert.Contains(events, e => e.Kind == AgentEventKind.Nudge && e.Text.Contains("nouvel essai (1/2)"));

        var first = _fake.ChatRequests[0]["options"]!["temperature"]!.GetValue<double>();
        var second = _fake.ChatRequests[1]["options"]!["temperature"]!.GetValue<double>();
        Assert.True(second > first, $"{second} devrait dépasser {first}");
    }

    [Fact]
    public async Task After_two_retries_the_error_is_reported_instead_of_looping_forever()
    {
        _fake.Enqueue(Glitch).Enqueue(Glitch).Enqueue(Glitch);

        var result = await Loop().RunAsync(Req());

        Assert.Equal(AgentOutcome.Failed, result.Outcome);
        Assert.Contains("repeat limit", result.Error);
        Assert.Equal(3, _fake.ChatRequests.Count);
    }
}
