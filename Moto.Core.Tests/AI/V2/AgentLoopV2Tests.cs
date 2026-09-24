// Moto.Core.Tests/AI/V2/AgentLoopV2Tests.cs
// La boucle de l'agent v2 contre un FAUX serveur Ollama scénarisé : ce qui compte ici n'est pas la qualité d'un
// vrai modèle (mesurée à part, par AgentBench) mais que la mécanique soit juste — rien d'écrit sans accord,
// refus/boucles/erreurs bornés, appel de secours pour le JSON écrit en texte, vérification avant de terminer.
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class AgentLoopV2Tests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    public void Dispose() => _ws.Dispose();

    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    private AgentLoopV2 Loop(IAgentApprover approver, Func<string, string, CancellationToken, Task<TerminalCommandResult>>? run = null)
        => new(new OllamaChatClient("http://127.0.0.1:11434", _fake), approver, runCommand: run);

    private AgentRunRequest Req(string goal = "faire la tâche", bool verify = false) => new()
    {
        Model = "fake",
        Goal = goal,
        WorkspaceRoot = _ws.Root,
        WriteAuditLog = false,
        BackupFolder = _ws.Backups,
        MaxSteps = 12,
        RequireVerification = verify,
    };

    [Fact]
    public async Task Reads_edits_after_approval_and_finishes()
    {
        var path = _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        _fake.Calls(("read_file", A(("path", "Program.cs"))))
             .Calls(("edit_file", A(("path", "Program.cs"), ("old_text", "int y = 2;"), ("new_text", "int y = 3;"))))
             .Calls(("finish", A(("summary", "y vaut 3."))));
        var approver = new AutoApprover();

        var events = new List<AgentEvent>();
        var result = await Loop(approver).RunAsync(Req(), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("y vaut 3.", result.Summary);
        Assert.Equal("int x = 1;\nint y = 3;\n", File.ReadAllText(path));
        Assert.Equal(1, approver.Approved);
        Assert.Equal(3, result.ToolCalls);
        Assert.Equal(0, result.ToolErrors);
        Assert.Equal(new ChangedFile("Program.cs", 1, 1, false), Assert.Single(result.Changes));
        Assert.Contains(events, e => e.Kind == AgentEventKind.ToolProposed && e.Path == "Program.cs");
        Assert.Equal(AgentEventKind.Finished, events[^1].Kind);

        // La requête envoyée à Ollama : contexte demandé, 7 outils, flux, conservation du modèle en mémoire.
        var first = _fake.ChatRequests[0];
        Assert.Equal(16384, first["options"]!["num_ctx"]!.GetValue<int>());
        Assert.Equal(8, first["tools"]!.AsArray().Count);
        Assert.True(first["stream"]!.GetValue<bool>());
        Assert.Equal("30m", first["keep_alive"]!.GetValue<string>());
        Assert.Equal("system", first["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Contains("Objectif : faire la tâche", first["messages"]![1]!["content"]!.GetValue<string>());

        // Le résultat de read_file est bien renvoyé au modèle au pas suivant, sous le rôle « tool ».
        var lastOfSecond = _fake.ChatRequests[1]["messages"]!.AsArray().Last()!;
        Assert.Equal("tool", lastOfSecond["role"]!.GetValue<string>());
        Assert.Equal("read_file", lastOfSecond["tool_name"]!.GetValue<string>());
        Assert.Contains("1 | int x = 1;", lastOfSecond["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Nothing_is_written_when_the_human_refuses_and_three_refusals_stop_the_run()
    {
        var path = _ws.Write("a.txt", "un\n");
        for (var i = 0; i < 4; i++)
            _fake.Calls(("edit_file", A(("path", "a.txt"), ("old_text", "un"), ("new_text", "deux" + i))));
        var approver = new AutoApprover(_ => false);

        var result = await Loop(approver).RunAsync(Req());

        Assert.Equal(AgentOutcome.Declined, result.Outcome);
        Assert.Equal("un\n", File.ReadAllText(path));
        Assert.Equal(3, approver.Refused);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task A_path_that_escapes_the_project_is_refused_without_bothering_the_human()
    {
        _fake.Calls(("edit_file", A(("path", "../evil.txt"), ("old_text", "a"), ("new_text", "b"))))
             .Calls(("finish", A(("summary", "ok"))));
        var approver = new AutoApprover();

        var result = await Loop(approver).RunAsync(Req());

        Assert.Equal(0, approver.Approved + approver.Refused);
        Assert.Equal(1, result.ToolErrors);
        var toolMessage = _fake.ChatRequests[1]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
        Assert.Contains("sort du dossier du projet", toolMessage);
    }

    [Fact]
    public async Task A_tool_call_written_as_json_text_is_still_executed()
    {
        _ws.Write("a.txt", "bonjour\n");
        _fake.Text("Je lis d'abord :\n```json\n{\"name\": \"read_file\", \"arguments\": {\"path\": \"a.txt\"}}\n```")
             .Calls(("finish", A(("summary", "lu"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal(2, result.ToolCalls);
        var toolMessage = _fake.ChatRequests[1]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
        Assert.Contains("1 | bonjour", toolMessage);
    }

    [Fact]
    public async Task The_same_call_repeated_is_detected_as_a_loop()
    {
        _ws.Write("a.txt", "x\n");
        for (var i = 0; i < 8; i++) _fake.Calls(("read_file", A(("path", "a.txt"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Equal(AgentOutcome.LoopDetected, result.Outcome);
        Assert.Equal(5, result.ToolCalls);
    }

    [Fact]
    public async Task Six_tool_errors_in_a_row_stop_the_run()
    {
        for (var i = 0; i < 8; i++) _fake.Calls(("read_file", A(("path", $"absent{i}.txt"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Equal(AgentOutcome.Failed, result.Outcome);
        Assert.Contains("erreurs d'outils d'affilée", result.Error);
        Assert.Equal(6, result.ToolErrors);
    }

    [Fact]
    public async Task Unknown_tool_is_reported_with_the_list_of_real_ones()
    {
        _fake.Calls(("delete_everything", A())).Calls(("finish", A(("summary", "fin"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Equal(1, result.ToolErrors);
        var msg = _fake.ChatRequests[1]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
        Assert.Contains("Outil inconnu", msg);
        Assert.Contains("edit_file", msg);
    }

    [Fact]
    public async Task Changed_code_must_be_built_before_the_run_can_finish()
    {
        _ws.Write("A.cs", "class A { int x = 1; }\n");
        var commands = new List<string>();
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 2;"))))
             .Calls(("finish", A(("summary", "trop tôt"))))                                  // refusé une fois
             .Calls(("run_command", A(("command", "dotnet build A.csproj"))))
             .Calls(("finish", A(("summary", "modifié et compilé"))));

        var result = await Loop(new AutoApprover(), (cmd, _, _) =>
        {
            commands.Add(cmd);
            return Task.FromResult(new TerminalCommandResult { ExitCode = 0, Output = "La génération a réussi." });
        }).RunAsync(Req(verify: true));

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("modifié et compilé", result.Summary);
        Assert.Equal(new[] { "dotnet build A.csproj" }, commands);
        var nudge = _fake.ChatRequests[3]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
        Assert.Contains("sans vérifier qu'il compile", nudge);
    }

    [Fact]
    public async Task Finishing_after_only_failed_writes_is_questioned_once_and_the_result_carries_a_warning()
    {
        _ws.Write("a.txt", "un\n");
        _fake.Calls(("edit_file", A(("path", "a.txt"), ("old_text", "absent"), ("new_text", "x"))))   // échoue : passage introuvable
             .Calls(("finish", A(("summary", "c'est fait"))))                                       // mensonge : rappelé à l'ordre
             .Calls(("finish", A(("summary", "rien n'a pu être modifié"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("rien n'a pu être modifié", result.Summary);
        Assert.Empty(result.Changes);
        Assert.Contains("Aucune modification", result.Warning);
        var reminder = _fake.ChatRequests[2]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
        Assert.Contains("AUCUN fichier n'a été modifié", reminder);
    }

    [Fact]
    public async Task A_real_change_leaves_no_warning()
    {
        _ws.Write("a.txt", "un\n");
        _fake.Calls(("edit_file", A(("path", "a.txt"), ("old_text", "un"), ("new_text", "deux"))))
             .Calls(("finish", A(("summary", "fait"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task The_build_reminder_is_given_only_once()
    {
        _ws.Write("A.cs", "class A { int x = 1; }\n");
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 2;"))))
             .Calls(("finish", A(("summary", "premier essai"))))
             .Calls(("finish", A(("summary", "je termine quand même"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req(verify: true));

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("je termine quand même", result.Summary);
    }

    [Fact]
    public async Task A_text_only_answer_gets_one_nudge_then_counts_as_the_final_answer()
    {
        _fake.Text("Je vais lire le fichier.").Text("Voici la réponse.");

        var events = new List<AgentEvent>();
        var result = await Loop(new AutoApprover()).RunAsync(Req(), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("Voici la réponse.", result.Summary);
        Assert.Single(events, e => e.Kind == AgentEventKind.Nudge);
        Assert.Equal(2, result.ModelCalls);
    }

    [Fact]
    public async Task A_model_without_tool_support_fails_with_a_clear_message_before_doing_anything()
    {
        _fake.SupportsTools = false;

        var result = await Loop(new AutoApprover()).RunAsync(Req());

        Assert.Equal(AgentOutcome.Failed, result.Outcome);
        Assert.Contains("ne sait pas appeler d'outils", result.Error);
        Assert.Empty(_fake.ChatRequests);
    }

    [Fact]
    public async Task A_cancelled_run_reports_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Loop(new AutoApprover()).RunAsync(Req(), ct: cts.Token);

        Assert.Equal(AgentOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task The_step_limit_is_enforced()
    {
        _ws.Write("a.txt", "x\n");
        for (var i = 0; i < 6; i++) _fake.Calls(("read_file", A(("path", "a.txt"), ("start_line", i + 1))));

        var request = new AgentRunRequest
        {
            Model = "fake", Goal = "g", WorkspaceRoot = _ws.Root, WriteAuditLog = false, BackupFolder = _ws.Backups, MaxSteps = 3,
        };
        var result = await Loop(new AutoApprover()).RunAsync(request);

        Assert.Equal(AgentOutcome.StepLimit, result.Outcome);
        Assert.Equal(3, result.ModelCalls);
    }

    [Fact]
    public async Task Undoing_a_run_restores_every_touched_file()
    {
        var a = _ws.Write("a.txt", "avant\n");
        _fake.Calls(("edit_file", A(("path", "a.txt"), ("old_text", "avant"), ("new_text", "après"))))
             .Calls(("write_file", A(("path", "b.txt"), ("content", "neuf\n"))))
             .Calls(("finish", A(("summary", "fait"))));

        var result = await Loop(new AutoApprover()).RunAsync(Req());
        Assert.Equal("après\n", File.ReadAllText(a));
        Assert.True(_ws.Exists("b.txt"));

        Assert.Equal(2, result.Backup!.Restore());
        Assert.Equal("avant\n", File.ReadAllText(a));
        Assert.False(_ws.Exists("b.txt"));
    }
}

public class ToolCallSalvageTests
{
    private static readonly HashSet<string> Known = new() { "read_file", "edit_file", "finish" };

    [Fact]
    public void Finds_a_call_inside_a_json_fence_and_keeps_the_prose()
    {
        var (calls, rest) = ToolCallSalvage.Extract("Je regarde :\n```json\n{\"name\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}\n```", Known);

        var call = Assert.Single(calls);
        Assert.Equal("read_file", call.Name);
        Assert.Equal("a.cs", call.Arguments["path"]!.GetValue<string>());
        Assert.Equal("Je regarde :", rest);
    }

    [Fact]
    public void Accepts_tool_call_tags_string_arguments_and_flat_arguments()
    {
        var tagged = ToolCallSalvage.Extract("<tool_call>{\"name\":\"read_file\",\"arguments\":\"{\\\"path\\\":\\\"b.cs\\\"}\"}</tool_call>", Known);
        Assert.Equal("b.cs", Assert.Single(tagged.Calls).Arguments["path"]!.GetValue<string>());

        var flat = ToolCallSalvage.Extract("{\"name\":\"read_file\",\"path\":\"c.cs\"}", Known);
        Assert.Equal("c.cs", Assert.Single(flat.Calls).Arguments["path"]!.GetValue<string>());

        var openAi = ToolCallSalvage.Extract("{\"function\":{\"name\":\"finish\",\"arguments\":{\"summary\":\"ok\"}}}", Known);
        Assert.Equal("finish", Assert.Single(openAi.Calls).Name);
    }

    [Fact]
    public void Several_calls_are_returned_in_order()
    {
        var (calls, _) = ToolCallSalvage.Extract(
            "[{\"name\":\"read_file\",\"arguments\":{\"path\":\"a\"}},{\"name\":\"read_file\",\"arguments\":{\"path\":\"b\"}}]", Known);
        Assert.Equal(new[] { "a", "b" }, calls.Select(c => c.Arguments["path"]!.GetValue<string>()));
    }

    [Fact]
    public void Ordinary_json_and_unknown_tools_are_never_executed()
    {
        Assert.Empty(ToolCallSalvage.Extract("Voici la config : {\"name\": \"MonApp\", \"version\": 2}", Known).Calls);
        Assert.Empty(ToolCallSalvage.Extract("{\"name\":\"format_disk\",\"arguments\":{}}", Known).Calls);
        Assert.Empty(ToolCallSalvage.Extract("du texte { pas du json", Known).Calls);
    }

    [Fact]
    public void Braces_inside_strings_do_not_break_the_scan()
    {
        var (calls, _) = ToolCallSalvage.Extract(
            "{\"name\":\"edit_file\",\"arguments\":{\"path\":\"a.cs\",\"old_text\":\"void F() { }\",\"new_text\":\"void F() { G(); }\"}}", Known);
        Assert.Equal("void F() { G(); }", Assert.Single(calls).Arguments["new_text"]!.GetValue<string>());
    }
}

public class ContextTrimmerTests
{
    [Fact]
    public void Old_tool_results_are_replaced_first_and_the_instructions_are_kept()
    {
        var big = new string('x', 12_000);
        var messages = new List<LlmMessage> { LlmMessage.System("CONSIGNE"), LlmMessage.User("Objectif : X") };
        for (var i = 0; i < 6; i++)
        {
            messages.Add(LlmMessage.Assistant(string.Empty, new[] { new LlmToolCall("read_file", new JsonObject { ["path"] = $"f{i}.cs" }) }));
            messages.Add(LlmMessage.Tool("read_file", big));
        }

        var fits = ContextTrimmer.Fit(messages, numCtx: 16384, toolSpecChars: 2000);

        Assert.True(fits);
        Assert.Equal("CONSIGNE", messages[0].Content);
        Assert.Equal("Objectif : X", messages[1].Content);
        var tools = messages.Where(m => m.Role == "tool").ToList();
        Assert.StartsWith("[ancien résultat", tools[0].Content);
        Assert.Equal(big, tools[^1].Content);            // le plus récent reste intact
    }

    [Fact]
    public void A_conversation_that_already_fits_is_left_alone()
    {
        var messages = new List<LlmMessage> { LlmMessage.System("s"), LlmMessage.User("u"), LlmMessage.Tool("read_file", "petit") };
        Assert.True(ContextTrimmer.Fit(messages, 16384, 2000));
        Assert.Equal("petit", messages[2].Content);
    }
}
