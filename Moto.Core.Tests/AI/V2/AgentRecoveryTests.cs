// Moto.Core.Tests/AI/V2/AgentRecoveryTests.cs
// Les garde-fous de « reprise » de l'agent v2, nés des transcriptions du banc d'essai avec un vrai petit modèle :
//  - un build qui échoue n'est pas une vérification réussie, et le run ne se termine pas dessus ;
//  - une réponse en texte qui annonce une action, s'excuse d'une erreur d'outil ou déclare fini un code cassé est renvoyée au travail ;
//  - les appels enchaînés à l'aveugle dans un même message (modifier puis compiler, deux insertions dans un fichier) sont coupés ;
//  - insert_lines refuse d'insérer un membre hors de la classe / entre une signature et son « { » ;
//  - search_text accepte un chemin de fichier.
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class CodeStructureTests
{
    private static string[] L(string code) => code.Replace("\r\n", "\n").Split('\n');

    [Fact]
    public void Blocks_are_classified_and_their_lines_are_known()
    {
        var blocks = CodeStructure.Scan(L(
            "namespace X\n{\n    public class A\n    {\n        public void F()\n        {\n        }\n    }\n}"))!;

        Assert.Equal(3, blocks.Count);
        Assert.Contains(blocks, b => b is { Kind: BlockKind.Namespace, OpenLine: 2, CloseLine: 9 });
        Assert.Contains(blocks, b => b is { Kind: BlockKind.Type, Name: "A", OpenLine: 4, CloseLine: 8 });
        Assert.Contains(blocks, b => b is { Kind: BlockKind.Other, OpenLine: 6, CloseLine: 7 });
    }

    [Fact]
    public void Braces_in_strings_chars_comments_and_directives_are_ignored()
    {
        var blocks = CodeStructure.Scan(L(
            "class A\n{\n    string s = \"{\";\n    char c = '}';\n    // }\n    /* { */\n    string v = @\"a { \"\" b\";\n    string raw = \"\"\"{\"\"\";\n#if X {\n#endif\n}"));

        Assert.NotNull(blocks);
        Assert.Single(blocks!);
        Assert.Equal(11, blocks![0].CloseLine);
    }

    [Fact]
    public void A_generic_constraint_naming_class_does_not_make_a_type()
    {
        var blocks = CodeStructure.Scan(L("class A\n{\n    void F<T>(T x) where T : class\n    {\n    }\n}"))!;

        Assert.Contains(blocks, b => b is { Kind: BlockKind.Type, Name: "A" });
        Assert.Contains(blocks, b => b is { Kind: BlockKind.Other, OpenLine: 4 });
        Assert.DoesNotContain(blocks, b => b.Kind == BlockKind.Type && b.Name != "A");
    }

    [Theory]
    [InlineData("class A {")]
    [InlineData("class A { } }")]
    [InlineData("class A { string s = @\"")]
    public void Unreliable_code_is_reported_as_unknown_instead_of_guessed(string code)
        => Assert.Null(CodeStructure.Scan(L(code)));

    [Theory]
    [InlineData("void F()\n{\n    x();\n}", 0)]
    [InlineData("void F()\n{\n    x();", 1)]
    [InlineData("    x();\n}", -1)]
    [InlineData("var s = \"{\"; // {", 0)]
    public void Net_braces_of_a_snippet_ignore_strings_and_comments(string snippet, int expected)
        => Assert.Equal(expected, CodeStructure.NetBraces(snippet));
}

public class InsertionGuardTests
{
    private static readonly string[] File =
    {
        "using System;",                 // 1
        "",                              // 2
        "namespace Shop.Services;",      // 3
        "",                              // 4
        "public class Inventory",        // 5
        "{",                             // 6
        "    private int _n;",           // 7
        "",                              // 8
        "    public int Count()",        // 9
        "    {",                         // 10
        "        return _n;",            // 11
        "    }",                         // 12
        "}",                             // 13
    };

    private const string Method = "    public int Twice()\n    {\n        return _n * 2;\n    }";

    [Fact]
    public void A_method_before_the_closing_brace_of_the_class_is_accepted()
        => Assert.Null(InsertionGuard.Check("Services/Inventory.cs", File, 13, Method));

    [Fact]
    public void A_method_between_two_members_is_accepted()
        => Assert.Null(InsertionGuard.Check("Services/Inventory.cs", File, 9, Method));

    [Fact]
    public void A_method_appended_after_the_last_brace_is_refused_with_the_line_to_use()
    {
        var refusal = InsertionGuard.Check("Services/Inventory.cs", File, 14, Method);

        Assert.NotNull(refusal);
        Assert.Contains("EN DEHORS de toute classe", refusal);
        Assert.Contains("« Inventory »", refusal);
        Assert.Contains("line=13", refusal);
    }

    [Fact]
    public void An_insertion_between_a_signature_and_its_opening_brace_is_refused()
    {
        var refusal = InsertionGuard.Check("Services/Inventory.cs", File, 10, Method);

        Assert.NotNull(refusal);
        Assert.Contains("ENTRE la signature et son corps", refusal);
    }

    [Fact]
    public void A_member_inserted_inside_a_method_body_is_refused_and_the_class_end_is_suggested()
    {
        var refusal = InsertionGuard.Check("Services/Inventory.cs", File, 11, Method);

        Assert.NotNull(refusal);
        Assert.Contains("À L'INTÉRIEUR d'une méthode", refusal);
        Assert.Contains("line=13", refusal);
    }

    [Fact]
    public void A_using_line_or_a_new_class_at_the_end_is_not_a_member_and_is_accepted()
    {
        Assert.Null(InsertionGuard.Check("Services/Inventory.cs", File, 1, "using System.Linq;"));
        Assert.Null(InsertionGuard.Check("Services/Inventory.cs", File, 14, "public sealed class Other\n{\n}"));
        Assert.Null(InsertionGuard.Check("Services/Inventory.cs", File, 14, "// fin du fichier"));
    }

    [Fact]
    public void Unbalanced_braces_in_the_inserted_text_are_refused()
    {
        var refusal = InsertionGuard.Check("Services/Inventory.cs", File, 13, "    public void F()\n    {\n        x();");

        Assert.NotNull(refusal);
        Assert.Contains("1 accolade(s) « { » de trop", refusal);
    }

    [Fact]
    public void Other_languages_and_unreadable_files_are_never_blocked()
    {
        Assert.Null(InsertionGuard.Check("notes.py", File, 14, Method));
        Assert.Null(InsertionGuard.Check("Broken.cs", new[] { "class A {", "    int x;" }, 3, Method));
    }

    [Fact]
    public void A_block_scoped_namespace_is_understood()
    {
        var lines = new[] { "namespace X", "{", "    public class A", "    {", "    }", "}" };

        var refusal = InsertionGuard.Check("A.cs", lines, 6, Method);   // avant l'accolade du namespace : hors de la classe

        Assert.NotNull(refusal);
        Assert.Contains("« A »", refusal);
        Assert.Contains("line=5", refusal);
        Assert.Null(InsertionGuard.Check("A.cs", lines, 5, Method));
    }
}

public class AgentRecoveryTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    public void Dispose() => _ws.Dispose();

    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    private AgentLoopV2 Loop(Func<string, string, CancellationToken, Task<TerminalCommandResult>>? run = null)
        => new(new OllamaChatClient("http://127.0.0.1:11434", _fake), new AutoApprover(), runCommand: run);

    private AgentRunRequest Req(string goal, bool verify = false) => new()
    {
        Model = "fake",
        Goal = goal,
        WorkspaceRoot = _ws.Root,
        WriteAuditLog = false,
        BackupFolder = _ws.Backups,
        MaxSteps = 30,
        RequireVerification = verify,
    };

    private string LastMessage(int requestIndex)
        => _fake.ChatRequests[requestIndex]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();

    private static Func<string, string, CancellationToken, Task<TerminalCommandResult>> Builds(List<string> commands, params int[] exitCodes)
    {
        var queue = new Queue<int>(exitCodes);
        return (cmd, _, _) =>
        {
            commands.Add(cmd);
            var code = queue.Count > 0 ? queue.Dequeue() : 0;
            return Task.FromResult(new TerminalCommandResult
            {
                ExitCode = code,
                Output = code == 0 ? "La génération a réussi." : "A.cs(1,5): error CS1002: ; attendu [A.csproj]\nÉCHEC de la build.",
            });
        };
    }

    // ── Compilation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_build_that_fails_is_not_a_verification_and_the_run_carries_on_until_it_passes()
    {
        var path = _ws.Write("A.cs", "class A { int x = 1 }\n");
        var commands = new List<string>();
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1"), ("new_text", "int x = 2"))))
             .Calls(("run_command", A(("command", "dotnet build"))))                                       // échoue (code 1)
             .Calls(("finish", A(("summary", "c'est bon"))))                                               // refusé : le build a échoué
             .Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 2"), ("new_text", "int x = 2;"))))
             .Calls(("run_command", A(("command", "dotnet build"))))                                       // réussit (code 0)
             .Calls(("finish", A(("summary", "corrigé"))));

        var result = await Loop(Builds(commands, 1, 0)).RunAsync(Req("Change x en 2 dans A.cs.", verify: true));

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("corrigé", result.Summary);
        Assert.Equal("class A { int x = 2; }\n", File.ReadAllText(path));
        Assert.Equal(2, commands.Count);
        Assert.Null(result.Warning);
        var nudge = LastMessage(3);
        Assert.Contains("ÉCHOUÉ", nudge);
        Assert.Contains("error CS1002", nudge);
    }

    [Fact]
    public async Task A_text_answer_after_a_failed_build_is_sent_back_to_fix_it()
    {
        _ws.Write("A.cs", "class A { int x = 1 }\n");
        var commands = new List<string>();
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1"), ("new_text", "int x = 2"))))
             .Calls(("run_command", A(("command", "dotnet build"))))
             .Text("Il y a une erreur, je vais la corriger.")                                              // texte seul : renvoyé
             .Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 2"), ("new_text", "int x = 2;"))))
             .Calls(("run_command", A(("command", "dotnet build"))))
             .Calls(("finish", A(("summary", "corrigé"))));

        var events = new List<AgentEvent>();
        var result = await Loop(Builds(commands, 1, 0)).RunAsync(Req("Change x en 2 dans A.cs.", verify: true), events.Add);

        Assert.Equal("corrigé", result.Summary);
        Assert.Contains(events, e => e.Kind == AgentEventKind.Nudge && e.Text.Contains("compilation a échoué"));
    }

    [Fact]
    public async Task After_four_reminders_a_still_failing_build_lets_the_run_end_with_a_warning()
    {
        _ws.Write("A.cs", "class A { int x = 1 }\n");
        var commands = new List<string>();
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1"), ("new_text", "int x = 2"))))
             .Calls(("run_command", A(("command", "dotnet build"))));
        for (var i = 0; i < 5; i++) _fake.Calls(("finish", A(("summary", "voilà"))));

        var events = new List<AgentEvent>();
        var result = await Loop(Builds(commands, 1)).RunAsync(Req("Change x en 2 dans A.cs.", verify: true), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal(4, events.Count(e => e.Kind == AgentEventKind.Nudge));
        Assert.Contains("compilation a échoué", result.Warning);
    }

    // ── Réponses en texte ───────────────────────────────────────────────────

    [Fact]
    public async Task An_announced_action_is_sent_back_even_when_the_task_is_only_a_question()
    {
        _ws.Write("A.cs", "class A { }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Text("Je vais maintenant chercher dans les autres fichiers.")
             .Calls(("finish", A(("summary", "Rien d'autre."))));

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Que contient A.cs ?"), events.Add);

        Assert.Equal("Rien d'autre.", result.Summary);
        Assert.Single(events, e => e.Kind == AgentEventKind.Nudge);
        Assert.Contains("annonces une action", LastMessage(2));
    }

    [Fact]
    public async Task An_announcement_is_nudged_at_most_twice_then_accepted_as_the_answer()
    {
        _ws.Write("A.cs", "class A { }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Text("Je vais lire.").Text("Je vais lire.").Text("Je vais lire.");

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Que contient A.cs ?"), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal(2, events.Count(e => e.Kind == AgentEventKind.Nudge));
    }

    [Fact]
    public async Task A_text_reply_after_a_failed_tool_call_is_sent_back_with_the_error_in_mind()
    {
        _ws.Write("A.cs", "class A { }\n");
        _fake.Calls(("read_file", A(("path", "Absent.cs"))))
             .Text("Désolé, le fichier est introuvable.")
             .Calls(("read_file", A(("path", "A.cs"))))
             .Calls(("finish", A(("summary", "A est vide."))));

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Que contient le fichier A.cs ?"), events.Add);

        Assert.Equal("A est vide.", result.Summary);
        Assert.Contains("dernier appel d'outil a échoué", LastMessage(2));
        Assert.Contains("aucune autorisation", LastMessage(2));
    }

    [Theory]
    [InlineData("Je vais lire le fichier.", true)]
    [InlineData("Appelons edit_file pour corriger cela.", true)]
    [InlineData("Voici la commande pour compiler :", true)]
    [InlineData("Let me read the file first.", true)]
    [InlineData("Le fichier contient 3 méthodes.", false)]
    [InlineData("Terminé : la constante vaut maintenant 250.", false)]
    [InlineData("", false)]
    public void Announcements_are_recognised(string text, bool expected)
        => Assert.Equal(expected, AgentLoopV2.LooksLikeAnnouncement(text));

    // ── Plusieurs appels dans un même message ───────────────────────────────

    [Fact]
    public async Task Blind_chains_in_one_message_run_only_the_first_modification()
    {
        var path = _ws.Write("A.cs", "class A\n{\n    int x = 1;\n}\n");
        var commands = new List<string>();
        _fake.Calls(
                ("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 2;"))),
                ("edit_file", A(("path", "A.cs"), ("old_text", "int x = 2;"), ("new_text", "int x = 3;"))),   // même fichier : ignoré
                ("run_command", A(("command", "dotnet build"))))                                            // après une modif : ignoré
             .Calls(("finish", A(("summary", "fait"))));

        var result = await Loop(Builds(commands, 0)).RunAsync(Req("Change x en 2 dans A.cs."));

        Assert.Equal("class A\n{\n    int x = 2;\n}\n", File.ReadAllText(path));
        Assert.Empty(commands);
        Assert.Equal(1, result.Changes.Count);
        var tools = _fake.ChatRequests[1]["messages"]!.AsArray().Where(m => m!["role"]!.GetValue<string>() == "tool").Select(m => m!["content"]!.GetValue<string>()).ToList();
        Assert.Equal(3, tools.Count);
        Assert.Contains("Fichier modifié", tools[0]);
        Assert.Contains("numéros de ligne ont changé", tools[1]);
        Assert.Contains("Attends le résultat de la modification", tools[2]);
    }

    [Fact]
    public async Task Modifications_of_different_files_in_one_message_are_all_applied()
    {
        var a = _ws.Write("A.txt", "un\n");
        var b = _ws.Write("B.txt", "deux\n");
        _fake.Calls(
                ("edit_file", A(("path", "A.txt"), ("old_text", "un"), ("new_text", "1"))),
                ("edit_file", A(("path", "B.txt"), ("old_text", "deux"), ("new_text", "2"))))
             .Calls(("finish", A(("summary", "fait"))));

        var result = await Loop().RunAsync(Req("Remplace les mots par des chiffres."));

        Assert.Equal("1\n", File.ReadAllText(a));
        Assert.Equal("2\n", File.ReadAllText(b));
        Assert.Equal(2, result.Changes.Count);
    }

    // ── Outils ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Insert_lines_outside_the_class_is_refused_before_any_confirmation_and_the_file_is_untouched()
    {
        var path = _ws.Write("A.cs", "class A\n{\n    int x;\n}\n");
        var approver = new AutoApprover();
        _fake.Calls(("insert_lines", A(("path", "A.cs"), ("line", 5), ("text", "    public int Twice() { return x * 2; }"))))
             .Calls(("insert_lines", A(("path", "A.cs"), ("line", 4), ("text", "    public int Twice() { return x * 2; }"))))
             .Calls(("finish", A(("summary", "fait"))));

        var result = await new AgentLoopV2(new OllamaChatClient("http://127.0.0.1:11434", _fake), approver)
            .RunAsync(Req("Ajoute une méthode Twice dans A.cs."));

        Assert.Contains("EN DEHORS de toute classe", LastMessage(1));
        Assert.Contains("line=4", LastMessage(1));
        Assert.Equal(1, approver.Approved);                   // seule la 2e tentative est arrivée jusqu'à l'humain
        Assert.Equal("class A\n{\n    int x;\n    public int Twice() { return x * 2; }\n}\n", File.ReadAllText(path));
        Assert.Equal(AgentOutcome.Completed, result.Outcome);
    }

    [Fact]
    public async Task Search_text_accepts_a_file_path_and_searches_only_that_file()
    {
        _ws.Write("A.cs", "class A { int limite = 100; }\n");
        _ws.Write("B.cs", "class B { int limite = 200; }\n");
        var ctx = new AgentToolContext(_ws.Root, "test", new RunBackup(_ws.Root, "run1", _ws.Backups), null);

        var one = await new SearchTextToolV2().ExecuteAsync(A(("query", "limite"), ("path", "A.cs")), ctx, default);
        var all = await new SearchTextToolV2().ExecuteAsync(A(("query", "limite")), ctx, default);
        var missing = await new SearchTextToolV2().ExecuteAsync(A(("query", "limite"), ("path", "Z.cs")), ctx, default);

        Assert.False(one.IsError);
        Assert.Contains("A.cs:1", one.Text);
        Assert.DoesNotContain("B.cs", one.Text);
        Assert.Contains("B.cs:1", all.Text);
        Assert.True(missing.IsError);
        Assert.Contains("list_dir", missing.Text);
    }
}
