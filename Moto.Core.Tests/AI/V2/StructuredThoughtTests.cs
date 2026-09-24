// Moto.Core.Tests/AI/V2/StructuredThoughtTests.cs
// Le champ facultatif « pensee » (mode structuré), l'avertissement d'exploration sans fin et le rappel « conclus »
// après une compilation réussie — trois garde-fous tirés des transcriptions du banc d'essai.
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class StructuredThoughtSchemaTests
{
    private static readonly IReadOnlyList<LlmToolSpec> Tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();

    [Fact]
    public void Without_thought_the_schema_and_instructions_are_unchanged()
    {
        var schema = StructuredTools.BuildSchema(Tools);
        Assert.All(schema["anyOf"]!.AsArray(), b => Assert.Null(b!["properties"]!["pensee"]));
        Assert.DoesNotContain("pensee", StructuredTools.BuildInstructions(Tools));
    }

    [Fact]
    public void With_thought_every_branch_requires_a_short_sentence_written_before_the_tool()
    {
        var schema = StructuredTools.BuildSchema(Tools, withThought: true);

        foreach (var branch in schema["anyOf"]!.AsArray())
        {
            var thought = branch!["properties"]!["pensee"]!;
            Assert.Equal("string", thought["type"]!.GetValue<string>());
            Assert.Equal(StructuredTools.ThoughtMaxLength, thought["maxLength"]!.GetValue<int>());
            Assert.Equal(new[] { "pensee", "tool", "arguments" }, branch["required"]!.AsArray().Select(n => n!.GetValue<string>()));
            // L'ordre des propriétés compte : le modèle écrit la phrase AVANT de choisir l'outil.
            Assert.Equal("pensee", ((JsonObject)branch["properties"]!).First().Key);
        }
    }

    [Fact]
    public void With_thought_the_instructions_show_the_field()
    {
        var text = StructuredTools.BuildInstructions(Tools, withThought: true);
        Assert.Contains("{\"pensee\":", text);
        Assert.Contains("UNE phrase", text);
    }

    [Fact]
    public void A_thought_is_written_first_when_a_call_is_rendered()
    {
        var call = new LlmToolCall("read_file", FakeOllamaHandler.Args(("path", "a.cs")));
        Assert.Equal("{\"pensee\":\"Je lis a.cs.\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}", StructuredTools.Render(call, "Je lis a.cs."));
        Assert.Equal("{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}", StructuredTools.Render(call));
    }

    [Fact]
    public void The_thought_is_read_back_with_the_call()
    {
        var call = StructuredTools.TryParse("{\"pensee\":\"  Je lis a.cs.  \",\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}", Tools, out var thought);

        Assert.Equal("read_file", call!.Name);
        Assert.Equal("Je lis a.cs.", thought);
        Assert.Null(StructuredTools.TryParse("{\"pensee\":\"x\",\"tool\":\"inconnu\",\"arguments\":{}}", Tools, out _));
        Assert.NotNull(StructuredTools.TryParse("{\"tool\":\"list_dir\",\"arguments\":{}}", Tools, out var none));
        Assert.Equal(string.Empty, none);
    }

    [Fact]
    public void The_history_keeps_the_thought_of_earlier_calls_so_the_model_sees_its_own_format()
    {
        var call = new LlmToolCall("read_file", FakeOllamaHandler.Args(("path", "a.cs")));
        var messages = new List<LlmMessage>
        {
            LlmMessage.System("S"), LlmMessage.User("U"),
            LlmMessage.Assistant("Je lis a.cs.", new[] { call }),
            LlmMessage.Tool("read_file", "1 | x"),
        };

        var withThought = StructuredTools.ToPlainMessages(messages, Tools, withThought: true);
        var without = StructuredTools.ToPlainMessages(messages, Tools);

        Assert.StartsWith("{\"pensee\":\"Je lis a.cs.\"", withThought[2].Content);
        Assert.StartsWith("{\"tool\":\"read_file\"", without[2].Content);
    }
}

public class StructuredThoughtClientAndLoopTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    public void Dispose() => _ws.Dispose();

    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);
    private OllamaChatClient Client() => new("http://127.0.0.1:11434", _fake);

    private AgentRunRequest Req(string goal, bool thought = true, AgentToolMode mode = AgentToolMode.Structured) => new()
    {
        Model = "fake", Goal = goal, WorkspaceRoot = _ws.Root, WriteAuditLog = false, BackupFolder = _ws.Backups,
        MaxSteps = 30, RequireVerification = false, ToolMode = mode,
        Options = new LlmOptions { StructuredThought = thought },
    };

    private AgentLoopV2 Loop(Func<string, string, CancellationToken, Task<TerminalCommandResult>>? run = null)
        => new(Client(), new AutoApprover(), runCommand: run);

    private string LastMessage(int requestIndex)
        => _fake.ChatRequests[requestIndex]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();

    [Fact]
    public async Task The_client_asks_for_the_thought_and_returns_it_as_the_reply_text()
    {
        var tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();
        _fake.Text("{\"pensee\":\"Je commence par lire.\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}");

        var reply = await Client().ChatAsync("fake", new[] { LlmMessage.User("U") }, tools,
            options: new LlmOptions { StructuredThought = true }, toolMode: LlmToolMode.Structured);

        Assert.NotNull(_fake.ChatRequests[0]["format"]!["anyOf"]![0]!["properties"]!["pensee"]);
        Assert.Equal("Je commence par lire.", reply.Content);
        Assert.Equal("read_file", Assert.Single(reply.ToolCalls).Name);
    }

    [Fact]
    public async Task The_loop_shows_the_thought_as_a_text_event_and_completes()
    {
        _ws.Write("a.txt", "bonjour\n");
        _fake.Text("{\"pensee\":\"Je lis a.txt.\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.txt\"}}")
             .Text("{\"pensee\":\"J'ai la réponse.\",\"tool\":\"finish\",\"arguments\":{\"summary\":\"Il dit bonjour.\"}}");
        var events = new List<AgentEvent>();

        var result = await Loop().RunAsync(Req("Que dit a.txt ?"), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal(new[] { "Je lis a.txt.", "J'ai la réponse." }, events.Where(e => e.Kind == AgentEventKind.Text).Select(e => e.Text));

        // La conversation renvoyée au serveur garde la phrase, dans le format que le modèle doit continuer d'écrire.
        var history = _fake.ChatRequests[1]["messages"]!.AsArray().Select(m => m!["content"]!.GetValue<string>());
        Assert.Contains(history, c => c.StartsWith("{\"pensee\":\"Je lis a.txt.\""));
    }

    [Fact]
    public async Task Without_the_option_no_text_event_is_emitted_in_structured_mode()
    {
        _ws.Write("a.txt", "bonjour\n");
        _fake.Text("{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.txt\"}}")
             .Text("{\"tool\":\"finish\",\"arguments\":{\"summary\":\"fait\"}}");
        var events = new List<AgentEvent>();

        await Loop().RunAsync(Req("Que dit a.txt ?", thought: false), events.Add);

        Assert.DoesNotContain(events, e => e.Kind == AgentEventKind.Text);
        Assert.Null(_fake.ChatRequests[0]["format"]!["anyOf"]![0]!["properties"]!["pensee"]);
    }

    // ── Exploration sans fin ────────────────────────────────────────────────

    [Fact]
    public async Task After_six_reads_without_any_change_the_model_is_told_to_make_the_change_now()
    {
        _ws.Write("A.cs", "class A { }\n");
        for (var i = 0; i < 6; i++) _fake.Calls(("search_text", A(("query", "mot" + i))));
        _fake.Calls(("finish", A(("summary", "rien"))));

        var events = new List<AgentEvent>();
        await Loop().RunAsync(Req("Ajoute une méthode Hello à la classe A.", mode: AgentToolMode.Native), events.Add);

        // Le résultat de la 6e recherche (renvoyé dans la requête n° 6) porte l'avertissement ; celui de la 5e non.
        Assert.DoesNotContain("Tu explores", LastMessage(5));
        Assert.Contains("Tu explores depuis 6 pas", LastMessage(6));
        Assert.Contains("edit_file", LastMessage(6));
    }

    [Fact]
    public async Task A_question_is_never_pushed_to_edit_however_long_the_exploration()
    {
        _ws.Write("A.cs", "class A { }\n");
        for (var i = 0; i < 7; i++) _fake.Calls(("search_text", A(("query", "mot" + i))));
        _fake.Calls(("finish", A(("summary", "dans A.cs"))));

        await Loop().RunAsync(Req("Dans quel fichier est définie la classe A ?", mode: AgentToolMode.Native));

        Assert.All(Enumerable.Range(1, _fake.ChatRequests.Count - 1), i => Assert.DoesNotContain("Tu explores", LastMessage(i)));
    }

    [Fact]
    public async Task After_a_write_there_is_no_exploration_warning()
    {
        _ws.Write("A.cs", "class A { }\n");
        for (var i = 0; i < 4; i++) _fake.Calls(("search_text", A(("query", "mot" + i))));
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "class A { }"), ("new_text", "class A { void Hello() { } }"))));
        for (var i = 4; i < 9; i++) _fake.Calls(("search_text", A(("query", "mot" + i))));
        _fake.Calls(("finish", A(("summary", "fait"))));

        await Loop().RunAsync(Req("Ajoute une méthode Hello à la classe A.", mode: AgentToolMode.Native));

        Assert.All(Enumerable.Range(1, _fake.ChatRequests.Count - 1), i => Assert.DoesNotContain("Tu explores", LastMessage(i)));
    }

    // ── Compilation réussie ─────────────────────────────────────────────────

    [Fact]
    public async Task A_successful_build_after_changes_tells_the_model_to_conclude()
    {
        _ws.Write("A.cs", "class A { int x = 1; }\n");
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1"), ("new_text", "int x = 2"))))
             .Calls(("run_command", A(("command", "dotnet build"))))
             .Calls(("finish", A(("summary", "fait"))));
        Task<TerminalCommandResult> Ok(string cmd, string dir, CancellationToken ct)
            => Task.FromResult(new TerminalCommandResult { ExitCode = 0, Output = "La génération a réussi." });

        var result = await Loop(Ok).RunAsync(Req("Change x en 2 dans A.cs.", mode: AgentToolMode.Native));

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Contains("La compilation a réussi", LastMessage(2));
        Assert.Contains("appelle finish", LastMessage(2));
    }

    [Fact]
    public async Task A_build_without_any_change_gets_no_conclude_hint()
    {
        _ws.Write("A.cs", "class A { }\n");
        _fake.Calls(("run_command", A(("command", "dotnet build"))))
             .Calls(("finish", A(("summary", "rien à faire"))));
        Task<TerminalCommandResult> Ok(string cmd, string dir, CancellationToken ct)
            => Task.FromResult(new TerminalCommandResult { ExitCode = 0, Output = "La génération a réussi." });

        await Loop(Ok).RunAsync(Req("Compile le projet.", mode: AgentToolMode.Native));

        Assert.DoesNotContain("La compilation a réussi", LastMessage(1));
    }
}
