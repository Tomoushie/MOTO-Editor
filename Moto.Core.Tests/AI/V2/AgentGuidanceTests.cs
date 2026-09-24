// Moto.Core.Tests/AI/V2/AgentGuidanceTests.cs
// Les garde-fous « de guidage » de l'agent v2, nés des constats du banc d'essai (AgentBench) avec un vrai petit modèle :
// aperçu du projet dans le premier message, commande de compilation proposée d'office, relance d'un modèle
// qui s'arrête sans avoir écrit alors que la tâche demandait une modification, rejet d'une réponse qui recopie un résultat d'outil.
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Llm;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class IntentHeuristicsTests
{
    [Theory]
    [InlineData("Dans Services/InventoryService.cs, ajoute une méthode publique CountInStock. Ne change rien d'autre.")]
    [InlineData("Renomme la méthode ComputeTax en CalculateTax dans tout le projet.")]
    [InlineData("Le projet ne compile plus. Corrige l'erreur.")]
    [InlineData("Crée le fichier Services/ConsoleReportWriter.cs avec une classe publique.")]
    [InlineData("Passe la constante MaxItems à 250. Ne modifie rien d'autre dans ce fichier.")]
    [InlineData("Extrais la validation dans une méthode privée. Le comportement ne doit pas changer.")]
    [InlineData("Refactor the parser and add a unit test.")]
    [InlineData("Documente les méthodes publiques sans changer le code.")]
    [InlineData("Explique ce fichier puis corrige la faute de frappe.")]
    [InlineData("Refactore Services/PricingService.cs : découpe les méthodes trop longues.")]
    public void Goals_that_ask_for_a_change_are_recognised(string goal)
        => Assert.True(IntentHeuristics.ExpectsFileChanges(goal));

    [Theory]
    [InlineData("Sans rien modifier, dis-moi combien de méthodes publiques contient la classe.")]
    [InlineData("Lecture seule : où est définie la méthode ComputeTax ?")]
    [InlineData("Que fait la méthode Restock ?")]
    [InlineData("Explique ce fichier.")]
    [InlineData("Passe en revue le code et liste les problèmes.")]
    [InlineData("Combien de fichiers .cs y a-t-il ?")]
    [InlineData("Do not modify anything. Tell me what this class does.")]
    [InlineData("")]
    [InlineData(null)]
    public void Questions_and_read_only_goals_are_never_pushed_to_edit(string? goal)
        => Assert.False(IntentHeuristics.ExpectsFileChanges(goal));

    [Fact]
    public void A_restriction_on_the_rest_is_not_a_ban_on_writing()
    {
        // « ne change rien d'AUTRE » précise la tâche ; « ne change rien » seul l'interdit.
        Assert.True(IntentHeuristics.ExpectsFileChanges("Ajoute une propriété Name. Ne change rien d'autre."));
        Assert.False(IntentHeuristics.ExpectsFileChanges("Ajoute une propriété Name ? Ne change rien, réponds seulement."));
    }
}

public class WorkspaceOverviewTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    [Fact]
    public void The_tree_lists_folders_first_and_skips_build_output_and_git()
    {
        _ws.Write("Program.cs", "");
        _ws.Write("Services/A.cs", "");
        _ws.Write("Services/B.cs", "");
        _ws.Write("bin/Debug/x.dll", "");
        _ws.Write("obj/y.txt", "");
        _ws.Write(".git/config", "");

        var info = WorkspaceOverview.Scan(_ws.Root);

        var lines = info.Tree.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(new[] { "Services/", "  A.cs", "  B.cs", "Program.cs" }, lines);
    }

    [Fact]
    public void A_huge_root_is_capped_and_says_how_much_was_left_out()
    {
        for (var i = 0; i < 40; i++) _ws.Write($"f{i:00}.txt", "");

        var info = WorkspaceOverview.Scan(_ws.Root, maxLines: 15);

        var lines = info.Tree.Replace("\r\n", "\n").Split('\n');
        Assert.True(lines.Length <= 12, $"trop de lignes : {lines.Length}");
        Assert.Contains("autres à la racine", lines[^1]);
    }

    [Fact]
    public void Expanded_folders_are_limited_and_a_folder_beyond_the_budget_shows_its_size()
    {
        for (var i = 0; i < 25; i++) _ws.Write($"Big/f{i:00}.cs", "");

        var info = WorkspaceOverview.Scan(_ws.Root, maxLines: 60, maxPerDir: 5);
        Assert.Contains("  … et 20 autres", info.Tree);

        var tight = WorkspaceOverview.Scan(_ws.Root, maxLines: 1, maxPerDir: 5);
        Assert.Contains("Big/ (25 éléments)", tight.Tree);
    }

    [Fact]
    public void A_single_csproj_gives_the_build_command_and_several_do_not()
    {
        _ws.Write("Shop.csproj", "<Project />");
        var one = WorkspaceOverview.Scan(_ws.Root);
        Assert.Equal(new[] { "Shop.csproj" }, one.Projects);
        Assert.Equal("dotnet build Shop.csproj", one.SuggestedBuild);

        _ws.Write("Lib/Lib.csproj", "<Project />");
        var two = WorkspaceOverview.Scan(_ws.Root);
        Assert.Equal(new[] { "Shop.csproj", "Lib/Lib.csproj" }, two.Projects);
        Assert.Null(two.SuggestedBuild);
    }

    [Fact]
    public void A_project_path_with_spaces_is_quoted()
        => Assert.Equal("dotnet build \"My App/My App.csproj\"", WorkspaceOverview.BuildCommand("My App/My App.csproj"));

    [Fact]
    public void The_nearest_project_is_found_by_walking_up_and_is_null_when_there_is_none()
    {
        _ws.Write("App/App.csproj", "<Project />");
        _ws.Write("App/Views/Deep/Page.cs", "");
        _ws.Write("Loose/Note.cs", "");

        Assert.Equal("App/App.csproj", WorkspaceOverview.NearestProject(_ws.Root, "App/Views/Deep/Page.cs"));
        Assert.Null(WorkspaceOverview.NearestProject(_ws.Root, "Loose/Note.cs"));
    }

    [Fact]
    public void An_unknown_folder_gives_an_empty_overview_instead_of_throwing()
    {
        var info = WorkspaceOverview.Scan(Path.Combine(_ws.Root, "n'existe-pas"));
        Assert.Empty(info.Tree);
        Assert.Empty(info.Projects);
    }
}

public class AgentLoopGuidanceTests : IDisposable
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
        MaxSteps = 20,
        RequireVerification = verify,
    };

    private string SentToModel(int requestIndex, int messageIndex)
        => _fake.ChatRequests[requestIndex]["messages"]!.AsArray()[messageIndex]!["content"]!.GetValue<string>();

    private string LastMessage(int requestIndex)
        => _fake.ChatRequests[requestIndex]["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();

    // ── Premier message ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_first_message_shows_the_project_tree_and_the_exact_build_command()
    {
        _ws.Write("Shop.csproj", "<Project />");
        _ws.Write("Services/Inventory.cs", "class Inventory { }\n");
        _fake.Calls(("finish", A(("summary", "rien à faire"))));

        await Loop().RunAsync(Req("Explique le projet."));

        var first = SentToModel(0, 1);
        Assert.Contains("Objectif : Explique le projet.", first);
        Assert.Contains("Services/", first);
        Assert.Contains("  Inventory.cs", first);
        Assert.Contains("run_command avec : dotnet build Shop.csproj", first);
    }

    [Fact]
    public async Task With_several_projects_the_model_is_told_to_build_the_smallest_one()
    {
        _ws.Write("App/App.csproj", "<Project />");
        _ws.Write("Lib/Lib.csproj", "<Project />");
        _fake.Calls(("finish", A(("summary", "ok"))));

        await Loop().RunAsync(Req("Explique le projet."));

        var first = SentToModel(0, 1);
        Assert.Contains("Projets .NET : App/App.csproj, Lib/Lib.csproj.", first);
        Assert.Contains("plus petit projet .csproj", first);
        Assert.DoesNotContain("run_command avec : dotnet build", first);
    }

    [Fact]
    public async Task A_verify_command_given_by_the_caller_wins_over_the_detected_one()
    {
        _ws.Write("Shop.csproj", "<Project />");
        _fake.Calls(("finish", A(("summary", "ok"))));
        var request = new AgentRunRequest
        {
            Model = "fake", Goal = "Explique.", WorkspaceRoot = _ws.Root, WriteAuditLog = false, BackupFolder = _ws.Backups,
            VerifyCommand = "dotnet build Shop.csproj -f net8.0",
        };

        await Loop().RunAsync(request);

        Assert.Contains("run_command avec : dotnet build Shop.csproj -f net8.0", SentToModel(0, 1));
    }

    [Fact]
    public void No_tool_description_carries_an_example_the_model_could_copy_literally()
    {
        // Constat du banc d'essai : « dotnet build Moto.Core/Moto.Core.csproj » a été recopié tel quel dans un projet sans ce fichier.
        foreach (var tool in AgentToolSetV2.Default())
        {
            var text = tool.Description + tool.Parameters.ToJsonString();
            Assert.DoesNotContain("Moto.Core", text);
            Assert.DoesNotContain("Moto.Editor", text);
        }
    }

    // ── Tâche d'écriture restée sans écriture ───────────────────────────────

    [Fact]
    public async Task A_write_task_finished_without_any_edit_is_nudged_twice_then_the_result_carries_a_warning()
    {
        _ws.Write("A.cs", "class A { }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Calls(("finish", A(("summary", "c'est fait"))))          // rappel 1
             .Calls(("finish", A(("summary", "c'est fait"))))          // rappel 2
             .Calls(("finish", A(("summary", "c'est fait"))));         // terminé quand même

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Ajoute une méthode Foo dans A.cs."), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Empty(result.Changes);
        Assert.Equal(2, events.Count(e => e.Kind == AgentEventKind.Nudge));
        Assert.Contains("AUCUN fichier", LastMessage(2));
        Assert.Contains("sans proposer aucune modification", result.Warning);
    }

    [Fact]
    public async Task A_plan_written_as_text_is_nudged_and_the_run_then_edits_the_file()
    {
        var path = _ws.Write("A.cs", "class A { int x = 1; }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Text("Voici mon plan : je remplacerai x par 2.")          // texte seul, aucune écriture : relancé
             .Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 2;"))))
             .Calls(("finish", A(("summary", "x vaut 2."))));

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Remplace la valeur de x par 2 dans A.cs."), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("class A { int x = 2; }\n", File.ReadAllText(path));
        Assert.Single(events, e => e.Kind == AgentEventKind.Nudge);
        Assert.Null(result.Warning);
        Assert.Contains("AUCUN fichier", LastMessage(2));
    }

    [Fact]
    public async Task A_question_is_never_pushed_to_edit()
    {
        _ws.Write("A.cs", "class A { public void F() { } public void G() { } }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Calls(("finish", A(("summary", "2 méthodes."))));

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Sans rien modifier, dis-moi combien de méthodes A contient."), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("2 méthodes.", result.Summary);
        Assert.DoesNotContain(events, e => e.Kind == AgentEventKind.Nudge);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task Failed_write_attempts_are_not_told_they_did_nothing()
    {
        // Une tentative d'écriture a eu lieu (ratée) : c'est le rappel d'honnêteté qui s'applique, pas « tu n'as rien tenté ».
        _ws.Write("a.txt", "un\n");
        _fake.Calls(("edit_file", A(("path", "a.txt"), ("old_text", "absent"), ("new_text", "x"))))
             .Calls(("finish", A(("summary", "fait"))))
             .Calls(("finish", A(("summary", "rien n'a pu être modifié"))));

        var result = await Loop().RunAsync(Req("Remplace « un » par « deux » dans a.txt."));

        Assert.Contains("HONNÊTEMENT", LastMessage(2));
        Assert.Contains("Aucune modification n'a été appliquée", result.Warning);
    }

    // ── Réponse qui recopie un résultat d'outil ─────────────────────────────

    [Fact]
    public async Task A_reply_that_copies_a_tool_result_is_discarded_and_the_model_gets_another_chance()
    {
        var path = _ws.Write("A.cs", "class A { int x = 1; }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Text("<tool_response>\nA.cs — lignes 1–1 sur 1\n  1 | class A { int x = 1; }\n</tool_response>")
             .Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 5;"))))
             .Calls(("finish", A(("summary", "x vaut 5."))));

        var events = new List<AgentEvent>();
        var result = await Loop().RunAsync(Req("Change la valeur de x en 5 dans A.cs."), events.Add);

        Assert.Equal(AgentOutcome.Completed, result.Outcome);
        Assert.Equal("class A { int x = 5; }\n", File.ReadAllText(path));
        Assert.Contains(events, e => e.Kind == AgentEventKind.Nudge && e.Text.Contains("copie"));
        // La copie ne reste pas dans la conversation (elle prendrait de la place et servirait de modèle au modèle).
        Assert.DoesNotContain("<tool_response>", string.Concat(result.Transcript.Where(m => m.Role == "assistant").Select(m => m.Content)));
    }

    [Fact]
    public async Task A_model_that_keeps_copying_tool_results_fails_with_a_clear_message()
    {
        _ws.Write("A.cs", "class A { }\n");
        _fake.Calls(("read_file", A(("path", "A.cs"))))
             .Text("<tool_response>copie 1</tool_response>")
             .Text("<tool_response>copie 2</tool_response>")
             .Text("<tool_response>copie 3</tool_response>");

        var result = await Loop().RunAsync(Req("Ajoute une méthode dans A.cs."));

        Assert.Equal(AgentOutcome.Failed, result.Outcome);
        Assert.Contains("recopie", result.Summary);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void A_long_copy_of_the_last_tool_result_without_any_tag_is_also_detected()
    {
        var toolText = "A.cs — lignes 1–40 sur 40\n" + string.Join("\n", Enumerable.Range(1, 40).Select(i => $"{i,3} | var value{i} = {i};"));
        var messages = new List<LlmMessage> { LlmMessage.User("but"), LlmMessage.Tool("read_file", toolText), LlmMessage.Assistant("x") };

        Assert.True(AgentLoopV2.LooksLikeToolEcho("Voici le fichier :\n" + toolText, messages));
        Assert.False(AgentLoopV2.LooksLikeToolEcho("J'ai lu A.cs : il contient 40 variables.", messages));
        Assert.False(AgentLoopV2.LooksLikeToolEcho(string.Empty, messages));
    }

    // ── Vérification : projet le plus proche ────────────────────────────────

    [Fact]
    public async Task The_build_reminder_names_the_project_closest_to_the_changed_file()
    {
        _ws.Write("App/App.csproj", "<Project />");
        _ws.Write("Lib/Lib.csproj", "<Project />");
        _ws.Write("Lib/Calc.cs", "class Calc { int x = 1; }\n");
        _fake.Calls(("read_file", A(("path", "Lib/Calc.cs"))))
             .Calls(("edit_file", A(("path", "Lib/Calc.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 2;"))))
             .Calls(("finish", A(("summary", "modifié"))));

        await Loop().RunAsync(Req("Change x en 2 dans Lib/Calc.cs.", verify: true));

        Assert.Contains("dotnet build Lib/Lib.csproj", LastMessage(3));
    }
}
