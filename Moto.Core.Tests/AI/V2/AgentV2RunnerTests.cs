// Moto.Core.Tests/AI/V2/AgentV2RunnerTests.cs
// Le branchement de l'agent v2 sur ce que voit l'utilisateur : panneau « Agents en cours » (étapes, statut, résultat,
// annulation), messages du chat, rechargement des onglets. Faux serveur Ollama scénarisé, repostage en ligne :
// aucun modèle réel, aucune interface — la mécanique seule.
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Llm;
using Moto.Core.Settings;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class AgentV2RunnerTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FakeOllamaHandler _fake = new();
    private readonly List<string> _narration = new();
    private readonly List<string> _reloaded = new();
    public void Dispose() => _ws.Dispose();

    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    private sealed class DelegateApprover : IAgentApprover
    {
        private readonly Func<ApprovalRequest, Task<bool>> _decide;
        public DelegateApprover(Func<ApprovalRequest, Task<bool>> decide) => _decide = decide;
        public Task<bool> ApproveAsync(ApprovalRequest request, CancellationToken ct) => _decide(request);
    }

    private AgentV2Runner Runner(IAgentApprover approver, AgentV2Settings? settings = null,
        Func<string, string, CancellationToken, Task<TerminalCommandResult>>? run = null)
    {
        var s = settings ?? new AgentV2Settings { ToolMode = AgentToolMode.Native };
        return new AgentV2Runner(approver, run, () => s, endpoint => new OllamaChatClient(endpoint, _fake),
            post: action => action(), backupFolder: _ws.Backups, writeAuditLog: false)
        {
            FilesChanged = _reloaded.AddRange,
        };
    }

    private static AgentRunRecord NewRun(string goal = "mets y à 3") => new() { AgentId = "agent-1", Goal = goal };

    private async Task<AgentRunRecord> RunAsync(AgentV2Runner runner, string goal = "mets y à 3")
    {
        var run = NewRun(goal);
        await runner.RunAsync(run, goal, _ws.Root, _narration.Add).WaitAsync(TimeSpan.FromSeconds(30));
        return run;
    }

    private string FullPath(string relative) => Path.GetFullPath(Path.Combine(_ws.Root, relative));

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

    /// <summary>Une modification, puis « finish » deux fois : le premier est repoussé une fois (« vérifie d'abord que ça compile »).</summary>
    private void ScriptOneEdit(string fileName = "Program.cs")
        => _fake.Calls(("edit_file", A(("path", fileName), ("old_text", "int y = 2;"), ("new_text", "int y = 3;"))))
                .Calls(("finish", A(("summary", "y vaut 3."))))
                .Calls(("finish", A(("summary", "y vaut 3."))));

    // ── Un run réussi ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_successful_run_fills_the_panel_and_tells_the_user_what_changed()
    {
        var path = _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        _fake.Calls(("read_file", A(("path", "Program.cs"))));
        ScriptOneEdit();

        var run = await RunAsync(Runner(new AutoApprover()));

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal("v2", run.Engine);
        Assert.Equal("qwen3:8b", run.Model);
        Assert.Equal("int x = 1;\nint y = 3;\n", File.ReadAllText(path));

        Assert.Equal(new[] { AgentActionKind.ReadFile, AgentActionKind.WriteFile, AgentActionKind.Finish, AgentActionKind.Finish },
            run.Steps.Select(s => s.ActionKind));
        Assert.Equal(new[] { 1, 2, 3, 4 }, run.Steps.Select(s => s.Index));
        var edit = run.Steps[1];
        Assert.Equal(ConfirmationState.Approved, edit.Confirmation);
        Assert.Equal("Modifier Program.cs (+1 −1)", edit.Summary);
        Assert.Contains("Program.cs", edit.ObservationSummary);
        Assert.StartsWith("Pas encore : ", run.Steps[2].ObservationSummary); // « finish » repoussé : rien n'a été compilé
        Assert.Equal("y vaut 3.", run.Steps[3].ObservationSummary);

        Assert.Equal("y vaut 3.", run.Summary);
        Assert.Single(run.ChangedFiles);
        Assert.True(run.CanUndo);
        Assert.Equal("1 fichier(s) modifié(s) (+1 −1) · qwen3:8b", run.ResultLine);

        // Ce que l'utilisateur lit dans le chat : le départ, la proposition, le résultat, le compte rendu — pas les lectures.
        Assert.Contains(_narration, n => n.StartsWith("🤖 Agent « agent-1 » démarre avec qwen3:8b"));
        Assert.Contains("🤖 Étape 2 : propose — modifier Program.cs (+1 −1)", _narration);
        Assert.Contains(_narration, n => n.StartsWith("✅ Étape 2 :"));
        Assert.DoesNotContain(_narration, n => n.Contains("Étape 1"));
        var final = _narration[^1];
        Assert.StartsWith("✅ Agent « agent-1 » terminé.", final);
        Assert.Contains("Program.cs (+1 −1)", final);
        Assert.Contains(AgentV2Runner.UndoHint, final);

        // L'éditeur est prévenu, avec le chemin COMPLET, pour recharger l'onglet.
        Assert.NotEmpty(_reloaded);
        Assert.All(_reloaded, p => Assert.Equal(FullPath("Program.cs"), p));
    }

    [Fact]
    public async Task The_request_sent_to_ollama_carries_the_chosen_model_and_the_configured_context()
    {
        _ws.Write("Program.cs", "int y = 2;\n");
        ScriptOneEdit();
        var settings = new AgentV2Settings { ToolMode = AgentToolMode.Native, NumCtx = 8192, MaxSteps = 7 };

        await RunAsync(Runner(new AutoApprover(), settings));

        var first = _fake.ChatRequests[0];
        Assert.Equal("qwen3:8b", first["model"]!.GetValue<string>());
        Assert.Equal(8192, first["options"]!["num_ctx"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_panel_shows_a_step_waiting_for_confirmation_while_the_human_decides()
    {
        _ws.Write("Program.cs", "int y = 2;\n");
        ScriptOneEdit();
        var run = NewRun();
        AgentRunStatus? statusWhileAsking = null;
        ConfirmationState? stepWhileAsking = null;
        var approver = new DelegateApprover(_ =>
        {
            statusWhileAsking = run.Status;
            stepWhileAsking = run.Steps[^1].Confirmation;
            return Task.FromResult(true);
        });

        await Runner(approver).RunAsync(run, run.Goal, _ws.Root, _narration.Add).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(AgentRunStatus.AwaitingConfirmation, statusWhileAsking);
        Assert.Equal(ConfirmationState.Pending, stepWhileAsking);
        Assert.Equal(AgentRunStatus.Completed, run.Status);
    }

    // ── Refus, arrêt, échecs ────────────────────────────────────────────────

    [Fact]
    public async Task Three_refusals_stop_the_run_and_nothing_is_written_or_reloaded()
    {
        var path = _ws.Write("a.txt", "un\n");
        for (var i = 0; i < 3; i++)
            _fake.Calls(("edit_file", A(("path", "a.txt"), ("old_text", "un"), ("new_text", "deux" + i))));

        var run = await RunAsync(Runner(new AutoApprover(_ => false)));

        Assert.Equal(AgentRunStatus.Cancelled, run.Status);
        Assert.Equal("un\n", File.ReadAllText(path));
        Assert.Equal(3, run.Steps.Count);
        Assert.All(run.Steps, s =>
        {
            Assert.Equal(ConfirmationState.Declined, s.Confirmation);
            Assert.Equal("Refusé par l'utilisateur.", s.ObservationSummary); // pas le message destiné au modèle
        });
        Assert.Equal(3, _narration.Count(n => n.StartsWith("🚫 Étape")));
        Assert.DoesNotContain(_narration, n => n.StartsWith("⚠ Étape"));
        Assert.StartsWith("⏹ Agent « agent-1 » arrêté : trois refus d'affilée.", _narration[^1]);
        Assert.Equal("aucun fichier modifié · qwen3:8b", run.ResultLine);
        Assert.False(run.CanUndo);
        Assert.Empty(_reloaded);
    }

    [Fact]
    public async Task Stopping_the_agent_while_a_confirmation_is_open_writes_nothing_even_if_the_human_clicks_allow()
    {
        var path = _ws.Write("Program.cs", "int y = 2;\n");
        ScriptOneEdit();
        var run = NewRun();
        var approver = new DelegateApprover(_ =>
        {
            run.Cts.Cancel(); // « Arrêter » pendant que la boîte est ouverte…
            return Task.FromResult(true); // … puis « Autoriser » : trop tard
        });

        await Runner(approver).RunAsync(run, run.Goal, _ws.Root, _narration.Add).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(AgentRunStatus.Cancelled, run.Status);
        Assert.Equal("int y = 2;\n", File.ReadAllText(path));
        Assert.Empty(run.ChangedFiles);
        Assert.Empty(_reloaded);
    }

    [Fact]
    public async Task An_unreachable_ollama_is_reported_in_plain_words_before_anything_starts()
    {
        _fake.Unreachable = true;

        var run = await RunAsync(Runner(new AutoApprover()));

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Contains("injoignable", run.Summary);
        Assert.Contains(_narration, n => n.StartsWith("❌ Agent « agent-1 » : Ollama est injoignable"));
        Assert.Empty(run.Steps);
        Assert.False(run.CanUndo);
    }

    [Fact]
    public async Task A_configured_model_that_is_not_installed_says_how_to_install_it()
    {
        var run = await RunAsync(Runner(new AutoApprover(), new AgentV2Settings { ToolMode = AgentToolMode.Native, Model = "nope:1b" }));

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Contains("« nope:1b »", run.Summary);
        Assert.Contains("ollama pull nope:1b", run.Summary);
        Assert.Empty(_fake.ChatRequests);
    }

    [Fact]
    public async Task No_installed_model_at_all_is_reported()
    {
        _fake.Models.Clear();

        var run = await RunAsync(Runner(new AutoApprover()));

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Contains("aucun modèle installé", run.Summary);
    }

    [Fact]
    public async Task When_no_recommended_model_is_installed_the_run_does_not_guess()
    {
        _fake.Models.Clear();
        _fake.Models.Add("llama3.1:8b");

        var run = await RunAsync(Runner(new AutoApprover(), new AgentV2Settings { ToolMode = AgentToolMode.Native, FallbackModel = "absent:1b" }));

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Contains("Aucun modèle recommandé n'est installé", run.Summary);
        Assert.Contains("llama3.1:8b", run.Summary);
    }

    [Fact]
    public async Task The_second_recommended_model_is_used_when_the_first_is_missing()
    {
        _fake.Models.Clear();
        _fake.Models.AddRange(new[] { "llama3.1:8b", "qwen2.5-coder:7b" });
        _ws.Write("Program.cs", "int y = 2;\n");
        ScriptOneEdit();

        var run = await RunAsync(Runner(new AutoApprover()));

        Assert.Equal("qwen2.5-coder:7b", run.Model);
        Assert.Equal("qwen2.5-coder:7b", _fake.ChatRequests[0]["model"]!.GetValue<string>());
    }

    // ── Annulation d'un run ─────────────────────────────────────────────────

    [Fact]
    public async Task Undoing_a_run_restores_edited_files_deletes_created_ones_and_asks_the_editor_to_reload()
    {
        var program = _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        _fake.Calls(("edit_file", A(("path", "Program.cs"), ("old_text", "int y = 2;"), ("new_text", "int y = 3;"))))
             .Calls(("write_file", A(("path", "Extra.cs"), ("content", "class Extra { }\n"))))
             .Calls(("finish", A(("summary", "fait"))))
             .Calls(("finish", A(("summary", "fait"))));

        var run = await RunAsync(Runner(new AutoApprover()));
        Assert.Equal(2, run.ChangedFiles.Count);
        Assert.True(_ws.Exists("Extra.cs"));
        Assert.True(run.CanUndo);
        _reloaded.Clear();

        var touched = run.UndoChanges(out var restored);

        Assert.Equal(2, restored);
        Assert.Equal("int x = 1;\nint y = 2;\n", File.ReadAllText(program));
        Assert.False(_ws.Exists("Extra.cs"));
        Assert.Equal(new[] { Path.GetFullPath(program), FullPath("Extra.cs") }.OrderBy(p => p), touched.OrderBy(p => p));
        Assert.True(run.IsUndone);
        Assert.False(run.CanUndo);
        Assert.Equal("modifications annulées · qwen3:8b", run.ResultLine);

        // Une seconde annulation ne fait rien.
        Assert.Empty(run.UndoChanges(out var again));
        Assert.Equal(0, again);
    }

    [Fact]
    public async Task Undo_can_tell_which_files_the_user_changed_since_the_run_ended()
    {
        var program = _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        ScriptOneEdit();
        var run = await RunAsync(Runner(new AutoApprover()));

        Assert.Empty(run.FilesEditedSinceRun()); // rien touché depuis la fin du run

        File.AppendAllText(program, "// ma note\n"); // l'utilisateur retravaille le fichier
        Assert.Equal(new[] { "Program.cs" }, run.FilesEditedSinceRun());

        run.UndoChanges(out _);
        Assert.Empty(run.FilesEditedSinceRun()); // annulé : plus rien à signaler
    }

    [Fact]
    public async Task An_incomplete_undo_keeps_the_button_and_names_the_files_still_changed()
    {
        var program = _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        ScriptOneEdit();
        var run = await RunAsync(Runner(new AutoApprover()));

        using (new FileStream(program, FileMode.Open, FileAccess.Read, FileShare.None)) // ouvert dans un autre programme
        {
            run.UndoChanges(out var restored);
            Assert.Equal(0, restored);
        }

        Assert.False(run.IsUndone);
        Assert.True(run.CanUndo); // on peut réessayer
        Assert.True(run.HasWarning);
        Assert.Contains("Annulation incomplète", run.Warning);
        Assert.Contains("Program.cs", run.Warning);
        Assert.Equal("int x = 1;\nint y = 3;\n", File.ReadAllText(program)); // toujours la version de l'agent

        run.UndoChanges(out var again); // libéré : ça passe
        Assert.Equal(1, again);
        Assert.True(run.IsUndone);
        Assert.False(run.HasWarning);
        Assert.Null(run.Warning);
        Assert.Equal("int x = 1;\nint y = 2;\n", File.ReadAllText(program));
    }

    [Fact]
    public async Task The_panel_shows_the_live_activity_while_running_and_the_result_line_afterwards()
    {
        _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        _fake.Calls(("read_file", A(("path", "Program.cs"))));
        ScriptOneEdit();
        var run = NewRun();
        var seen = new List<string>();
        run.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AgentRunRecord.Activity)) return;
            lock (seen) seen.Add(run.Activity);
        };

        await Runner(new AutoApprover()).RunAsync(run, "mets y à 3", _ws.Root, _narration.Add).WaitAsync(TimeSpan.FromSeconds(30));

        lock (seen)
        {
            Assert.Contains("Chargement du modèle en mémoire…", seen);
            Assert.Contains(seen, a => a.StartsWith("Étape 1 · "));
            Assert.Contains(seen, a => a.StartsWith("Étape 2 · "));
            Assert.Contains("Le modèle réfléchit…", seen);
        }

        // Fini : plus d'activité, mais un résultat et un résumé à montrer.
        Assert.Equal(string.Empty, run.Activity);
        Assert.False(run.HasActivity);
        Assert.True(run.HasResultLine);
        Assert.True(run.HasSummary);
        Assert.False(run.HasWarning);
    }

    [Fact]
    public async Task The_panel_hides_the_result_lines_of_a_run_that_is_still_going()
    {
        _ws.Write("Program.cs", "int x = 1;\nint y = 2;\n");
        ScriptOneEdit();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var approver = new DelegateApprover(async _ => { asked.TrySetResult(true); return await gate.Task; });
        var run = NewRun();

        var finished = Runner(approver).RunAsync(run, "mets y à 3", _ws.Root, _narration.Add);
        await asked.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(AgentRunStatus.AwaitingConfirmation, run.Status);
        Assert.True(run.HasActivity);
        Assert.False(run.HasResultLine);
        Assert.False(run.HasSummary);
        Assert.False(run.CanUndo);

        gate.SetResult(true);
        await finished.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(run.HasActivity);
        Assert.True(run.HasResultLine);
    }

    [Fact]
    public async Task The_service_starts_v2_runs_and_undoes_them_through_the_editor_callback()
    {
        var path = _ws.Write("Program.cs", "int y = 2;\n");
        ScriptOneEdit();
        var runner = Runner(new AutoApprover());
        var service = new BackgroundAgentService(new MotoAiKernel(_ws.Root), new AiConfirmationService(),
            Array.Empty<IAgentTool>(), new AgentMessageBus(), new AgentGlobalBudget(), runner);

        Assert.True(service.UsesV2);
        var run = service.Start("agent-1", "mets y à 3", _ws.Root, _narration.Add, context: "Fichier ouvert : Program.cs");
        Assert.Equal("v2", run.Engine);
        Assert.Contains(run, service.Runs);
        for (var i = 0; i < 1500 && run.IsActive; i++) await Task.Delay(20);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal("int y = 3;\n", File.ReadAllText(path));
        Assert.Contains("Fichier ouvert : Program.cs", _fake.ChatRequests[0]["messages"]![1]!["content"]!.GetValue<string>());

        _reloaded.Clear();
        var touched = service.UndoChanges(run.Id, out var restored);

        Assert.Equal(1, restored);
        Assert.Equal("int y = 2;\n", File.ReadAllText(path));
        Assert.Equal(new[] { Path.GetFullPath(path) }, touched);
        Assert.Equal(touched, _reloaded); // l'éditeur recharge l'onglet restauré
    }

    [Fact]
    public void The_service_uses_the_old_engine_when_the_setting_says_v1()
    {
        var runner = Runner(new AutoApprover(), new AgentV2Settings { Engine = "v1" });
        var service = new BackgroundAgentService(new MotoAiKernel(_ws.Root), new AiConfirmationService(),
            Array.Empty<IAgentTool>(), new AgentMessageBus(), new AgentGlobalBudget(), runner);

        Assert.False(service.UsesV2);
        Assert.False(new BackgroundAgentService(new MotoAiKernel(_ws.Root), new AiConfirmationService(),
            Array.Empty<IAgentTool>(), new AgentMessageBus(), new AgentGlobalBudget()).UsesV2);
    }

    // ── Ce que le chat raconte ──────────────────────────────────────────────

    [Fact]
    public async Task A_failed_build_is_told_as_a_warning_and_a_passing_one_as_a_success()
    {
        _ws.Write("A.cs", "class A { int x = 1 }\n");
        var commands = new List<string>();
        _fake.Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 1"), ("new_text", "int x = 2"))))
             .Calls(("run_command", A(("command", "dotnet build"))))
             .Calls(("edit_file", A(("path", "A.cs"), ("old_text", "int x = 2"), ("new_text", "int x = 2;"))))
             .Calls(("run_command", A(("command", "dotnet build"))))
             .Calls(("finish", A(("summary", "corrigé"))));

        var run = await RunAsync(Runner(new AutoApprover(), run: Builds(commands, 1, 0)), "Change x en 2 dans A.cs.");

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal(2, commands.Count);
        Assert.Matches(@"^⚠ Étape 2 : Commande en échec \(code 1\) — \d+ ligne\(s\) d'erreur\.$", _narration.First(n => n.StartsWith("⚠ Étape 2")));
        Assert.Contains("✅ Étape 4 : Commande réussie (code 0).", _narration);
        Assert.Equal(RunKind(run, 2), AgentActionKind.RunCommand);
    }

    private static AgentActionKind RunKind(AgentRunRecord run, int index) => run.Steps[index - 1].ActionKind;

    [Fact]
    public async Task The_reasoning_sentence_is_shown_once_when_enabled_and_never_otherwise()
    {
        _ws.Write("a.txt", "x\n");
        void Script() => _fake.Enqueue(FakeOllamaHandler.Reply("Je lis le fichier.", ("read_file", A(("path", "a.txt")))))
                              .Calls(("finish", A(("summary", "lu"))));

        Script();
        await RunAsync(Runner(new AutoApprover(), new AgentV2Settings { ToolMode = AgentToolMode.Native, Thought = true }), "lis a.txt");
        Assert.Single(_narration, n => n == "💭 Je lis le fichier.");

        _narration.Clear();
        Script();
        await RunAsync(Runner(new AutoApprover(), new AgentV2Settings { ToolMode = AgentToolMode.Native, Thought = false }), "lis a.txt");
        Assert.DoesNotContain(_narration, n => n.StartsWith("💭"));
    }

    [Fact]
    public void A_command_result_is_reduced_to_one_plain_sentence()
    {
        Assert.Equal("Commande réussie (code 0).", AgentV2Runner.DescribeCommandResult("Commande : dotnet build\nCode de sortie : 0\nLa génération a réussi."));
        Assert.Equal("Commande en échec (code 1) — 3 ligne(s) d'erreur.",
            AgentV2Runner.DescribeCommandResult("Commande : dotnet build\nCode de sortie : 1\n3 ligne(s) d'erreur :\nA.cs(1,1): error CS1002"));
        Assert.Equal("Commande en échec (code 2).", AgentV2Runner.DescribeCommandResult("Commande : x\nCode de sortie : 2\n(aucune sortie)"));
    }

    [Fact]
    public void The_final_message_explains_a_run_that_hit_its_limit_without_changing_anything()
    {
        var run = NewRun();
        var result = new AgentRunResult
        {
            Outcome = AgentOutcome.StepLimit,
            Summary = "Nombre maximal d'étapes atteint.",
            ToolMode = "structured",
            ToolCalls = 30,
            Elapsed = TimeSpan.FromSeconds(95),
        };

        var text = AgentV2Runner.FinalMessage(run, result, "qwen3:8b");

        Assert.StartsWith("⏱ Agent « agent-1 » arrêté : nombre maximal d'étapes atteint.", text);
        Assert.Contains("Aucun fichier modifié.", text);
        Assert.DoesNotContain(AgentV2Runner.UndoHint, text);
        Assert.EndsWith("(qwen3:8b · sortie contrainte · 30 appel(s) d'outils · 1 min 35 s)", text);
    }

    [Fact]
    public void Old_backups_are_pruned_and_recent_ones_kept()
    {
        var baseFolder = Path.Combine(_ws.Root, "backups");
        var old = Directory.CreateDirectory(Path.Combine(baseFolder, "old-run"));
        var recent = Directory.CreateDirectory(Path.Combine(baseFolder, "recent-run"));
        File.WriteAllText(Path.Combine(old.FullName, "a.cs"), "x");
        Directory.SetLastWriteTimeUtc(old.FullName, DateTime.UtcNow - TimeSpan.FromDays(30));

        var removed = RunBackup.Prune(baseFolder, TimeSpan.FromDays(14));

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(old.FullName));
        Assert.True(Directory.Exists(recent.FullName));
        Assert.Equal(0, RunBackup.Prune(Path.Combine(_ws.Root, "inexistant"), TimeSpan.FromDays(1)));
    }
}

public class AgentV2SettingsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "moto-settings-" + Guid.NewGuid().ToString("N")[..8] + ".json");
    public void Dispose() { try { File.Delete(_file); } catch (IOException) { } }

    [Fact]
    public void Defaults_are_the_ones_the_benchmark_recommended()
    {
        var s = AgentV2Settings.Load(new SettingsEngine(_file));

        Assert.True(s.UseV2);
        Assert.Equal(AgentToolMode.Structured, s.ToolMode);
        Assert.Equal(16384, s.NumCtx);
        Assert.Equal(30, s.MaxSteps);
        Assert.Equal(15, s.MaxMinutes);
        Assert.False(s.Thought);
        Assert.Equal(string.Empty, s.Model);
        Assert.Null(s.VerifyCommand);
        Assert.Equal("http://127.0.0.1:11434", s.Endpoint);
    }

    [Fact]
    public void Values_are_read_cleaned_and_clamped()
    {
        var engine = new SettingsEngine(_file);
        engine.Set("agent_engine", "V1");
        engine.Set("agent_model", "  llama3.1:8b ");
        engine.Set("agent_num_ctx", 100);
        engine.Set("agent_max_steps", 1000);
        engine.Set("agent_max_minutes", 0);
        engine.Set("agent_tool_mode", "NATIVE");
        engine.Set("agent_thought", true);
        engine.Set("agent_verify_command", "  dotnet build X.csproj ");
        engine.Set("ollama_endpoint", "http://localhost:11434/");
        engine.Set("ollama_model", "qwen2.5-coder:14b");

        var s = AgentV2Settings.Load(engine);

        Assert.False(s.UseV2);
        Assert.Equal("llama3.1:8b", s.Model);
        Assert.Equal(2048, s.NumCtx);
        Assert.Equal(100, s.MaxSteps);
        Assert.Equal(1, s.MaxMinutes);
        Assert.Equal(AgentToolMode.Native, s.ToolMode);
        Assert.True(s.Thought);
        Assert.Equal("dotnet build X.csproj", s.VerifyCommand);
        Assert.Equal("http://127.0.0.1:11434", s.Endpoint);
        Assert.Equal("qwen2.5-coder:14b", s.FallbackModel);

        engine.Set("agent_num_ctx", 999_999);
        Assert.Equal(131072, AgentV2Settings.Load(engine).NumCtx);
    }

    [Theory]
    [InlineData("auto", AgentToolMode.Auto)]
    [InlineData("Native", AgentToolMode.Native)]
    [InlineData("structured", AgentToolMode.Structured)]
    [InlineData("n'importe quoi", AgentToolMode.Structured)]
    [InlineData("", AgentToolMode.Structured)]
    [InlineData(null, AgentToolMode.Structured)]
    public void The_tool_mode_falls_back_to_structured(string? text, AgentToolMode expected)
        => Assert.Equal(expected, AgentV2Settings.ParseToolMode(text));

    [Fact]
    public void The_model_is_chosen_from_what_is_installed()
    {
        var installed = new[] { "llama3.1:8b", "qwen2.5-coder:7b", "Qwen3:8b" };

        Assert.Equal("mon-modele", AgentV2Settings.ChooseModel(" mon-modele ", "x", installed));      // le réglage gagne
        Assert.Equal("Qwen3:8b", AgentV2Settings.ChooseModel("", "x", installed));                    // 1er recommandé installé (casse d'Ollama)
        Assert.Equal("qwen2.5-coder:7b", AgentV2Settings.ChooseModel("", "x", new[] { "llama3.1:8b", "qwen2.5-coder:7b" }));
        Assert.Equal("mon-chat:1b", AgentV2Settings.ChooseModel("", "mon-chat:1b", new[] { "llama3.1:8b" }));  // dernier recours : le modèle du chat
        Assert.Equal("qwen2.5-coder:7b", AgentV2Settings.ChooseModel("", "  ", Array.Empty<string>()));
    }

    [Fact]
    public void A_model_without_a_tag_means_latest_like_in_ollama()
    {
        Assert.True(AgentV2Settings.IsInstalled("qwen3", new[] { "qwen3:latest" }));
        Assert.True(AgentV2Settings.IsInstalled("QWEN3:8B", new[] { "qwen3:8b" }));
        Assert.False(AgentV2Settings.IsInstalled("qwen3:8b", new[] { "qwen3:latest" }));
        Assert.False(AgentV2Settings.IsInstalled("", new[] { "qwen3:8b" }));
    }

    [Fact]
    public void Every_setting_the_agent_reads_is_declared_in_the_catalog_with_the_same_default()
    {
        var defaults = new AgentV2Settings();

        foreach (var id in new[] { "agent_engine", "agent_model", "agent_num_ctx", "agent_max_steps", "agent_max_minutes", "agent_tool_mode", "agent_thought", "agent_verify_command" })
            Assert.NotNull(SettingsCatalog.ById(id));

        Assert.Equal(defaults.Engine, SettingsCatalog.ById("agent_engine").Default);
        Assert.Equal(defaults.NumCtx, SettingsCatalog.ById("agent_num_ctx").Default);
        Assert.Equal(defaults.MaxSteps, SettingsCatalog.ById("agent_max_steps").Default);
        Assert.Equal(defaults.MaxMinutes, SettingsCatalog.ById("agent_max_minutes").Default);
        Assert.Equal(defaults.ToolMode.ToString().ToLowerInvariant(), SettingsCatalog.ById("agent_tool_mode").Default);
        Assert.Equal(defaults.Thought, SettingsCatalog.ById("agent_thought").Default);
        Assert.Equal(string.Empty, SettingsCatalog.ById("agent_model").Default);
        Assert.Equal(new[] { "structured", "native", "auto" }, SettingsCatalog.ById("agent_tool_mode").Options);
    }
}
