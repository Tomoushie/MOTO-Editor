// Moto.Core.Tests/AI/V2/StructuredToolsTests.cs
// Appels d'outils par SORTIE CONTRAINTE : le schéma imposé, la conversation aplatie, la lecture de la réponse,
// et la bascule automatique de la boucle (natif → structuré) pour un modèle qui « raconte » ou qui n'a pas « tools ».
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class StructuredToolsTests
{
    private static readonly IReadOnlyList<LlmToolSpec> Tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();

    [Fact]
    public void The_schema_offers_one_branch_per_tool_with_the_exact_parameters()
    {
        var schema = StructuredTools.BuildSchema(Tools);
        var branches = schema["anyOf"]!.AsArray();

        Assert.Equal(Tools.Count, branches.Count);
        var read = branches.Single(b => b!["properties"]!["tool"]!["const"]!.GetValue<string>() == "read_file")!;
        Assert.Equal(new[] { "tool", "arguments" }, read["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.False(read["additionalProperties"]!.GetValue<bool>());

        var args = read["properties"]!["arguments"]!;
        Assert.False(args["additionalProperties"]!.GetValue<bool>());
        Assert.NotNull(args["properties"]!["path"]);
        Assert.Equal("path", args["required"]!.AsArray().Single()!.GetValue<string>());
    }

    [Fact]
    public void Building_the_schema_does_not_alter_the_tool_specs()
    {
        var before = Tools[1].Parameters.ToJsonString();
        StructuredTools.BuildSchema(Tools);
        Assert.Equal(before, Tools[1].Parameters.ToJsonString());
    }

    [Fact]
    public void The_instructions_list_every_tool_and_mark_required_parameters()
    {
        var text = StructuredTools.BuildInstructions(Tools);

        Assert.Contains("{\"tool\":", text);
        foreach (var t in Tools) Assert.Contains($"- {t.Name} :", text);
        Assert.Contains("path (texte, obligatoire)", text);
        Assert.Contains("start_line (entier)", text);
    }

    [Fact]
    public void The_conversation_is_flattened_for_the_server()
    {
        var call = new LlmToolCall("read_file", FakeOllamaHandler.Args(("path", "a.cs")));
        var messages = new List<LlmMessage>
        {
            LlmMessage.System("Consigne."),
            LlmMessage.User("Objectif : lire."),
            LlmMessage.Assistant(string.Empty, new[] { call }),
            LlmMessage.Tool("read_file", "1 | int x;"),
            LlmMessage.User("Continue."),
        };

        var plain = StructuredTools.ToPlainMessages(messages, Tools);

        Assert.Equal(5, plain.Count);
        Assert.StartsWith("Consigne.", plain[0].Content);
        Assert.Contains("FORMAT DE RÉPONSE", plain[0].Content);
        Assert.Equal("assistant", plain[2].Role);
        Assert.Equal("{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}", plain[2].Content);
        Assert.Null(plain[2].ToolCalls);
        Assert.Equal("user", plain[3].Role);
        Assert.Equal("Résultat de « read_file » :\n1 | int x;", plain[3].Content);
        Assert.Equal("Continue.", plain[4].Content);
        Assert.Equal("system", messages[0].Role); // l'original n'est pas modifié
        Assert.Equal("Consigne.", messages[0].Content);
    }

    [Fact]
    public void Instructions_are_added_even_without_a_system_message()
    {
        var plain = StructuredTools.ToPlainMessages(new[] { LlmMessage.User("Bonjour.") }, Tools);

        Assert.Equal("system", plain[0].Role);
        Assert.Contains("FORMAT DE RÉPONSE", plain[0].Content);
        Assert.Equal("Bonjour.", plain[1].Content);
    }

    [Theory]
    [InlineData("{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}")]
    [InlineData("  {\"tool\": \"read_file\", \"arguments\": {\"path\": \"a.cs\"}}\n")]
    [InlineData("```json\n{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}\n```")]
    [InlineData("{\"name\":\"read_file\",\"parameters\":{\"path\":\"a.cs\"}}")]
    public void A_constrained_reply_is_read_as_a_tool_call(string content)
    {
        var call = StructuredTools.TryParseCall(content, Tools);

        Assert.NotNull(call);
        Assert.Equal("read_file", call!.Name);
        Assert.Equal("a.cs", call.Arguments["path"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Je vais lire le fichier.")]
    [InlineData("{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"")] // tronqué
    [InlineData("{\"tool\":\"delete_everything\",\"arguments\":{}}")]
    [InlineData("[1, 2]")]
    public void Anything_else_is_not_a_tool_call(string content)
        => Assert.Null(StructuredTools.TryParseCall(content, Tools));

    [Fact]
    public void A_missing_arguments_object_gives_empty_arguments()
    {
        var call = StructuredTools.TryParseCall("{\"tool\":\"list_dir\"}", Tools);

        Assert.NotNull(call);
        Assert.Empty(call!.Arguments);
    }
}

public class StructuredClientTests
{
    private readonly FakeOllamaHandler _fake = new();
    private OllamaChatClient Client() => new("http://127.0.0.1:11434", _fake);

    [Fact]
    public async Task Structured_mode_sends_a_schema_and_no_native_tools_and_returns_the_parsed_call()
    {
        var tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();
        _fake.Text("{\"tool\":\"read_file\",\"arguments\":{\"path\":\"a.cs\"}}");
        var streamed = new List<string>();

        var reply = await Client().ChatAsync("fake", new[] { LlmMessage.System("S"), LlmMessage.User("U") }, tools,
            onContent: streamed.Add, toolMode: LlmToolMode.Structured);

        var request = _fake.ChatRequests[0];
        Assert.Null(request["tools"]);
        Assert.NotNull(request["format"]!["anyOf"]);
        Assert.Equal(6000, request["options"]!["num_predict"]!.GetValue<int>());
        Assert.Contains("FORMAT DE RÉPONSE", request["messages"]![0]!["content"]!.GetValue<string>());

        var call = Assert.Single(reply.ToolCalls);
        Assert.Equal("read_file", call.Name);
        Assert.Equal(string.Empty, reply.Content);
        Assert.Empty(streamed); // le flux n'aurait été que du JSON brut
    }

    [Fact]
    public async Task Structured_mode_works_with_a_model_that_has_no_tools_capability()
    {
        _fake.SupportsTools = false;
        _fake.Text("{\"tool\":\"finish\",\"arguments\":{\"summary\":\"ok\"}}");
        var tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();

        var reply = await Client().ChatAsync("fake", new[] { LlmMessage.User("U") }, tools, toolMode: LlmToolMode.Structured);

        Assert.Equal("finish", Assert.Single(reply.ToolCalls).Name);
    }

    [Fact]
    public async Task Native_mode_still_refuses_a_model_without_tools_capability()
    {
        _fake.SupportsTools = false;
        var tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();

        var ex = await Assert.ThrowsAsync<LlmException>(() =>
            Client().ChatAsync("fake", new[] { LlmMessage.User("U") }, tools));

        Assert.True(ex.ToolsNotSupported);
    }

    [Fact]
    public async Task An_explicit_token_limit_is_kept_in_structured_mode()
    {
        _fake.Text("{\"tool\":\"list_dir\",\"arguments\":{}}");
        var tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();

        await Client().ChatAsync("fake", new[] { LlmMessage.User("U") }, tools,
            options: new LlmOptions { NumPredict = 300 }, toolMode: LlmToolMode.Structured);

        Assert.Equal(300, _fake.ChatRequests[0]["options"]!["num_predict"]!.GetValue<int>());
    }

    [Fact]
    public async Task Native_mode_sends_tools_and_no_format()
    {
        _fake.Text("bonjour");
        var tools = AgentToolSetV2.Default().Select(t => t.Spec).ToList();

        await Client().ChatAsync("fake", new[] { LlmMessage.User("U") }, tools);

        Assert.NotNull(_fake.ChatRequests[0]["tools"]);
        Assert.Null(_fake.ChatRequests[0]["format"]);
        Assert.Null(_fake.ChatRequests[0]["options"]!["num_predict"]);
    }
}

public class StructuredLoopTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    public void Dispose() => _ws.Dispose();

    private AgentRunRequest Req(string goal, AgentToolMode mode) => new()
    {
        Model = "fake", Goal = goal, WorkspaceRoot = _ws.Root, WriteAuditLog = false,
        BackupFolder = _ws.Backups, MaxSteps = 12, RequireVerification = false, ToolMode = mode,
    };

    private AgentLoopV2 Loop() => new(new OllamaChatClient("http://127.0.0.1:11434", _fake), new AutoApprover());

    private static string Json(string tool, string args) => "{\"tool\":\"" + tool + "\",\"arguments\":" + args + "}";

    [Fact]
    public async Task Auto_switches_to_structured_after_two_narrated_replies_and_then_completes()
    {
        _ws.Write("a.txt", "bonjour\n");
        _fake.Text("Je vais lire le fichier a.txt.")
             .Text("Je vais utiliser read_file pour lire a.txt.")
             .Text(Json("read_file", "{\"path\":\"a.txt\"}"))
             .Text(Json("finish", "{\"summary\":\"Le fichier dit bonjour.\"}"));
        var events = new List<AgentEvent>();

        var result = await Loop().RunAsync(Req("Que dit a.txt ?", AgentToolMode.Auto), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("Le fichier dit bonjour.", result.Summary);
        Assert.Equal("native→structured", result.ToolMode);
        Assert.Contains(events, e => e.Kind == AgentEventKind.Nudge && e.Text.Contains("sortie contrainte"));

        Assert.NotNull(_fake.ChatRequests[0]["tools"]);
        Assert.NotNull(_fake.ChatRequests[1]["tools"]);
        Assert.Null(_fake.ChatRequests[2]["tools"]);
        Assert.NotNull(_fake.ChatRequests[2]["format"]);

        // Le résultat de read_file est renvoyé au modèle sous forme de message « Résultat de … ».
        var last = _fake.ChatRequests[3]["messages"]!.AsArray().Last()!;
        Assert.Equal("user", last["role"]!.GetValue<string>());
        Assert.StartsWith("Résultat de « read_file » :", last["content"]!.GetValue<string>());
        Assert.Contains("1 | bonjour", last["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Auto_does_not_switch_for_a_model_that_calls_tools_properly()
    {
        _ws.Write("a.txt", "bonjour\n");
        _fake.Calls(("read_file", FakeOllamaHandler.Args(("path", "a.txt"))))
             .Calls(("finish", FakeOllamaHandler.Args(("summary", "lu"))));

        var result = await Loop().RunAsync(Req("Lis a.txt.", AgentToolMode.Auto));

        Assert.Equal("native", result.ToolMode);
        Assert.All(_fake.ChatRequests, r => Assert.Null(r["format"]));
    }

    [Fact]
    public async Task Auto_goes_straight_to_structured_when_the_model_has_no_tools_capability()
    {
        _fake.SupportsTools = false;
        _ws.Write("a.txt", "bonjour\n");
        _fake.Text(Json("read_file", "{\"path\":\"a.txt\"}"))
             .Text(Json("finish", "{\"summary\":\"fait\"}"));

        var result = await Loop().RunAsync(Req("Lis a.txt.", AgentToolMode.Auto));

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("native→structured", result.ToolMode);
        Assert.Equal(2, result.ModelCalls); // l'essai refusé n'est pas compté
        Assert.All(_fake.ChatRequests, r => Assert.NotNull(r["format"]));
    }

    [Fact]
    public async Task Native_mode_never_switches_and_a_model_without_tools_fails_clearly()
    {
        _fake.SupportsTools = false;

        var result = await Loop().RunAsync(Req("Lis a.txt.", AgentToolMode.Native));

        Assert.Equal(AgentOutcome.Failed, result.Outcome);
        Assert.Contains("ne sait pas appeler d'outils", result.Error);
    }

    [Fact]
    public async Task Structured_mode_starts_constrained_and_edits_after_approval()
    {
        var path = _ws.Write("a.txt", "un\ndeux\n");
        _fake.Text(Json("edit_file", "{\"path\":\"a.txt\",\"old_text\":\"deux\",\"new_text\":\"trois\"}"))
             .Text(Json("finish", "{\"summary\":\"modifié\"}"));

        var result = await Loop().RunAsync(Req("Remplace deux par trois dans a.txt.", AgentToolMode.Structured));

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("structured", result.ToolMode);
        Assert.Equal("un\ntrois\n", File.ReadAllText(path));
        Assert.Null(_fake.ChatRequests[0]["tools"]);
    }
}
