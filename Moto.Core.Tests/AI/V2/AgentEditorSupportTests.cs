// Moto.Core.Tests/AI/V2/AgentEditorSupportTests.cs
// Ce que l'éditeur doit à l'agent v2 côté Moto.Core : consignes prêtes à l'emploi (/refactor, /test, /doc), cache de fichiers
// qu'on peut invalider, boîte de confirmation qui montre un diff (et prévient), détection du travail de l'utilisateur avant
// une annulation, annulation incomplète. Aucun modèle, aucune interface.
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.Performance;
using Moto.Core.Settings;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class AgentGoalsTests
{
    public static IEnumerable<object[]> PresetsAndEngines()
        => AgentGoals.Presets.SelectMany(p => new[] { new object[] { p, true }, new object[] { p, false } });

    [Theory]
    [MemberData(nameof(PresetsAndEngines))]
    public void Every_goal_names_the_file_and_is_understood_as_a_request_to_change_files(string preset, bool v2)
    {
        var goal = AgentGoals.ForPreset(preset, "src/Foo.cs", v2);

        Assert.Contains("\"src/Foo.cs\"", goal);
        // Sans ça, un modèle qui répond « voilà mon plan » serait accepté comme ayant terminé.
        Assert.True(IntentHeuristics.ExpectsFileChanges(goal), goal);
    }

    [Theory]
    [MemberData(nameof(PresetsAndEngines))]
    public void Each_engine_gets_the_wording_that_matches_its_tools(string preset, bool v2)
    {
        var goal = AgentGoals.ForPreset(preset, "Foo.cs", v2);

        // La v1 réécrit le fichier entier avec WriteFile ; la v2 le refuse sur un fichier existant et modifie des passages.
        if (v2) Assert.DoesNotContain("WriteFile", goal);
        else Assert.Contains("WriteFile", goal);
    }

    [Fact]
    public void The_v2_wording_points_at_the_tools_that_fit_each_task()
    {
        Assert.Contains("write_file", AgentGoals.ForPreset("test", "Foo.cs", v2: true));
        Assert.Contains("insert_lines", AgentGoals.ForPreset("doc", "Foo.cs", v2: true));
        Assert.Contains("ne réécris pas le fichier entier", AgentGoals.ForPreset("refactor", "Foo.cs", v2: true));
    }

    [Fact]
    public void An_unknown_preset_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AgentGoals.ForPreset("deploy", "Foo.cs", v2: true));
}

public class LazyFileLoaderInvalidateTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task Invalidate_makes_the_next_read_and_save_use_what_the_agent_wrote()
    {
        var path = _ws.Write("A.cs", "avant\n");
        var loader = new LazyFileLoader();
        Assert.Equal("avant\n", await loader.GetContentAsync(path));
        loader.UpdateContent(path, "avant\n"); // l'éditeur marque tout fichier chargé comme « modifié »

        File.WriteAllText(path, "après l'agent\n"); // l'agent écrit sur le disque

        // Sans invalidation, le cache serait périmé : le lire ou l'enregistrer défairait le travail de l'agent.
        Assert.Equal("avant\n", await loader.GetContentAsync(path));

        Assert.True(loader.Invalidate(path));
        Assert.Equal(0, loader.LoadedCount);

        await loader.SaveAsync(path); // plus rien en cache : n'écrit rien
        Assert.Equal("après l'agent\n", File.ReadAllText(path));
        Assert.Equal("après l'agent\n", await loader.GetContentAsync(path));
    }

    [Fact]
    public async Task Invalidate_recognises_another_spelling_of_the_same_path()
    {
        var path = _ws.Write("A.cs", "x\n");
        var loader = new LazyFileLoader();
        await loader.GetContentAsync(path);

        Assert.True(loader.Invalidate(Path.Combine(_ws.Root, "sous-dossier", "..", "A.cs")));
        Assert.Equal(0, loader.LoadedCount);
    }

    [Fact]
    public async Task Invalidate_leaves_other_files_alone_and_says_when_there_was_nothing_to_forget()
    {
        var a = _ws.Write("A.cs", "a\n");
        var b = _ws.Write("B.cs", "b\n");
        var loader = new LazyFileLoader();
        await loader.GetContentAsync(a);
        await loader.GetContentAsync(b);

        Assert.True(loader.Invalidate(a));
        Assert.False(loader.Invalidate(a));
        Assert.False(loader.Invalidate(Path.Combine(_ws.Root, "inconnu.cs")));
        Assert.Equal(1, loader.LoadedCount);
    }

    [Fact]
    public async Task An_invalidated_file_is_not_written_back_when_the_cache_evicts_others()
    {
        var a = _ws.Write("A.cs", "avant\n");
        var b = _ws.Write("B.cs", "b\n");
        var loader = new LazyFileLoader(maxDocuments: 1);
        await loader.GetContentAsync(a);
        loader.UpdateContent(a, "avant\n");

        File.WriteAllText(a, "après l'agent\n");
        loader.Invalidate(a);
        await loader.GetContentAsync(b); // remplit le cache : aurait évincé (et réécrit) A s'il y était encore

        Assert.Equal("après l'agent\n", File.ReadAllText(a));
    }
}

public class ConfirmationServiceApproverTests
{
    private static ApprovalRequest Change(string details = "@@ -1 +1 @@\n- b\n+ a") => new()
    {
        AgentId = "agent-1",
        Title = "modifier Foo.cs (+1 −1)",
        Summary = "Modifier Foo.cs (+1 −1)",
        Details = details,
        Kind = ApprovalKind.FileChange,
        Path = "Foo.cs",
    };

    private static ApprovalRequest Command() => new()
    {
        AgentId = "agent-1",
        Title = "lancer une commande",
        Summary = "Lancer « dotnet build »",
        Details = "Commande : dotnet build",
        Kind = ApprovalKind.Command,
    };

    private static (ConfirmationServiceApprover Approver, List<ConfirmationRequest> Shown) Build(bool answer = true)
    {
        var service = new AiConfirmationService();
        var shown = new List<ConfirmationRequest>();
        service.ConfirmationHandler = request => { shown.Add(request); return Task.FromResult(answer); };
        return (new ConfirmationServiceApprover(service), shown);
    }

    [Fact]
    public async Task A_file_change_is_shown_as_a_diff_and_a_command_is_not()
    {
        var (approver, shown) = Build();

        await approver.ApproveAsync(Change(), CancellationToken.None);
        await approver.ApproveAsync(Command(), CancellationToken.None);

        Assert.True(shown[0].DetailsAreDiff);
        Assert.Equal(ConfirmationAction.ModifyCode, shown[0].Action);
        Assert.False(shown[1].DetailsAreDiff);
        Assert.Equal(ConfirmationAction.ExecuteCommand, shown[1].Action);
        Assert.Equal("Autoriser", shown[0].ConfirmText);
        Assert.Equal("Refuser", shown[0].CancelText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_users_answer_is_the_answer_returned_to_the_agent(bool answer)
    {
        var (approver, _) = Build(answer);
        Assert.Equal(answer, await approver.ApproveAsync(Change(), CancellationToken.None));
    }

    [Fact]
    public async Task A_warning_is_placed_above_the_summary()
    {
        var (approver, shown) = Build();
        ApprovalRequest? seen = null;
        approver.WarningProvider = request => { seen = request; return Task.FromResult<string?>("⚠ non enregistré  "); };
        var request = Change();

        await approver.ApproveAsync(request, CancellationToken.None);

        Assert.Same(request, seen);
        Assert.Equal("⚠ non enregistré\n\nModifier Foo.cs (+1 −1)", shown[0].Message);
    }

    [Fact]
    public async Task No_warning_leaves_the_summary_untouched()
    {
        var (approver, shown) = Build();
        approver.WarningProvider = _ => Task.FromResult<string?>(null);

        await approver.ApproveAsync(Change(), CancellationToken.None);

        Assert.Equal("Modifier Foo.cs (+1 −1)", shown[0].Message);
    }

    [Fact]
    public async Task A_failing_warning_provider_never_blocks_the_request()
    {
        var (approver, shown) = Build();
        approver.WarningProvider = _ => throw new InvalidOperationException("onglet inaccessible");

        Assert.True(await approver.ApproveAsync(Change(), CancellationToken.None));
        Assert.Equal("Modifier Foo.cs (+1 −1)", Assert.Single(shown).Message);
    }

    [Fact]
    public async Task An_enormous_diff_is_cut_with_a_notice_that_says_how_much_is_missing()
    {
        var (approver, shown) = Build();

        await approver.ApproveAsync(Change(new string('x', 100_000)), CancellationToken.None);

        var details = shown[0].Details;
        Assert.True(details.Length < 61_000);
        Assert.Contains("aperçu tronqué : 40000 caractères de plus", details);
    }

    [Fact]
    public async Task A_diff_under_the_cap_is_shown_whole()
    {
        var (approver, shown) = Build();
        var diff = string.Join("\n", Enumerable.Range(1, 500).Select(i => "+ ligne " + i));

        await approver.ApproveAsync(Change(diff), CancellationToken.None);

        Assert.Equal(diff, shown[0].Details);
    }
}

public class RunBackupSealTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    private RunBackup NewBackup() => new(_ws.Root, "run-test", _ws.Backups);

    [Fact]
    public void Only_files_touched_after_the_run_ended_are_reported()
    {
        var a = _ws.Write("A.cs", "1\n");
        var b = _ws.Write("B.cs", "1\n");
        var backup = NewBackup();
        backup.Save(a); File.WriteAllText(a, "2\n"); // l'agent modifie A…
        backup.Save(b); File.WriteAllText(b, "2\n"); // …et B

        Assert.Empty(backup.ChangedSinceSeal()); // pas encore scellé : rien à comparer

        backup.Seal();
        Assert.Empty(backup.ChangedSinceSeal());

        File.WriteAllText(b, "3\n"); // l'utilisateur retravaille B
        Assert.Equal(new[] { Path.GetFullPath(b) }, backup.ChangedSinceSeal());
        Assert.Equal("B.cs", backup.RelativePath(b));
    }

    [Fact]
    public void A_file_the_agent_created_and_the_user_then_deleted_or_edited_is_reported()
    {
        var created = Path.Combine(_ws.Root, "Neuf.cs");
        var backup = NewBackup();
        backup.Save(created); // n'existait pas
        File.WriteAllText(created, "class Neuf { }\n");
        backup.Seal();
        Assert.Empty(backup.ChangedSinceSeal());

        File.WriteAllText(created, "class Neuf { int x; }\n");
        Assert.Single(backup.ChangedSinceSeal());

        File.Delete(created);
        Assert.Single(backup.ChangedSinceSeal());
    }

    [Fact]
    public void A_file_locked_at_check_time_is_not_reported_as_changed()
    {
        var a = _ws.Write("A.cs", "1\n");
        var backup = NewBackup();
        backup.Save(a); File.WriteAllText(a, "2\n");
        backup.Seal();

        using (new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Empty(backup.ChangedSinceSeal()); // illisible : on ne crie pas au loup
    }

    [Fact]
    public void Restore_reports_files_it_could_not_put_back_and_can_be_retried()
    {
        var a = _ws.Write("A.cs", "avant\n");
        var b = _ws.Write("B.cs", "avant\n");
        var backup = NewBackup();
        backup.Save(a); File.WriteAllText(a, "agent\n");
        backup.Save(b); File.WriteAllText(b, "agent\n");

        int restored;
        IReadOnlyList<string> failed;
        using (new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.None)) // A est ouvert dans un autre programme
            restored = backup.Restore(out failed);

        Assert.Equal(1, restored);
        Assert.Equal(new[] { Path.GetFullPath(a) }, failed);
        Assert.Equal("avant\n", File.ReadAllText(b));

        backup.Restore(out failed); // A est libéré : refaire l'appel est sans risque
        Assert.Empty(failed);
        Assert.Equal("avant\n", File.ReadAllText(a));
    }

    [Fact]
    public void A_purged_original_copy_is_reported_and_the_file_is_left_alone()
    {
        var a = _ws.Write("A.cs", "avant\n");
        var backup = NewBackup();
        backup.Save(a); File.WriteAllText(a, "agent\n");
        Directory.Delete(backup.Folder, recursive: true); // dossier de sauvegardes vidé entre-temps

        Assert.Equal(0, backup.Restore(out var failed));
        Assert.Equal(new[] { Path.GetFullPath(a) }, failed);
        Assert.Equal("agent\n", File.ReadAllText(a));
    }
}
