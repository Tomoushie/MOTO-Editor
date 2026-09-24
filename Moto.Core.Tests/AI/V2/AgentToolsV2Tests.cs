// Moto.Core.Tests/AI/V2/AgentToolsV2Tests.cs
// Les 7 outils de l'agent v2, sur un vrai dossier temporaire : confinement des chemins, lecture par tranches,
// modification en deux temps (préparer → appliquer), sauvegarde/annulation, garde anti-troncature.
using System.Text.Json.Nodes;
using Moto.Core.AI.Autonomy.V2;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Core.Tests.AI.V2;

public class AgentToolsV2Tests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    public void Dispose() => _ws.Dispose();

    private AgentToolContext Ctx(Func<string, string, CancellationToken, Task<TerminalCommandResult>>? run = null)
        => new(_ws.Root, "test", new RunBackup(_ws.Root, "run1", _ws.Backups), run);

    private static JsonObject A(params (string Key, object Value)[] pairs) => FakeOllamaHandler.Args(pairs);

    // ── Confinement ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../evil.txt")]
    [InlineData(".git/config")]
    [InlineData("sub/.git/hooks/pre-commit")]
    public void Paths_outside_the_project_or_in_git_are_refused(string path)
    {
        Assert.Throws<ToolPathException>(() => Ctx().Resolve(path));
    }

    [Fact]
    public void Absolute_path_outside_the_project_is_refused()
    {
        var outside = Path.Combine(Path.GetTempPath(), "autre-dossier", "x.txt");
        Assert.Throws<ToolPathException>(() => Ctx().Resolve(outside));
    }

    [Fact]
    public void Leading_slash_means_relative_to_the_project()
    {
        var ctx = Ctx();
        Assert.Equal(Path.Combine(ctx.Root, "src", "a.cs"), ctx.Resolve("/src/a.cs"));
        Assert.Equal(Path.Combine(ctx.Root, "src", "a.cs"), ctx.Resolve("./src\\a.cs"));
        Assert.Equal(Path.Combine(ctx.Root, "src", "a.cs"), ctx.Resolve(Path.Combine(ctx.Root, "src", "a.cs")));
    }

    // ── Lecture ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_file_numbers_the_lines()
    {
        _ws.Write("a.txt", "un\ndeux\ntrois\n");
        var r = await new ReadFileToolV2().ExecuteAsync(A(("path", "a.txt")), Ctx(), default);

        Assert.False(r.IsError);
        Assert.Contains("a.txt — lignes 1–3 sur 3", r.Text);
        Assert.Contains("1 | un", r.Text);
        Assert.Contains("3 | trois", r.Text);
    }

    [Fact]
    public async Task Read_file_pages_through_a_long_file()
    {
        _ws.Write("long.txt", string.Join("\n", Enumerable.Range(1, 700).Select(i => "ligne " + i)) + "\n");
        var tool = new ReadFileToolV2();

        var first = await tool.ExecuteAsync(A(("path", "long.txt")), Ctx(), default);
        Assert.Contains("lignes 1–300 sur 700", first.Text);
        Assert.Contains("start_line=301", first.Text);
        Assert.DoesNotContain("ligne 301", first.Text);

        var next = await tool.ExecuteAsync(A(("path", "long.txt"), ("start_line", "301"), ("end_line", 305)), Ctx(), default);
        Assert.Contains("lignes 301–305 sur 700", next.Text);
        Assert.Contains("ligne 305", next.Text);
    }

    [Fact]
    public async Task Read_file_reports_missing_file_and_directories()
    {
        _ws.Write("dir/x.txt", "x");
        var tool = new ReadFileToolV2();

        var missing = await tool.ExecuteAsync(A(("path", "nope.txt")), Ctx(), default);
        Assert.True(missing.IsError);
        Assert.Contains("introuvable", missing.Text);

        var dir = await tool.ExecuteAsync(A(("path", "dir")), Ctx(), default);
        Assert.True(dir.IsError);
        Assert.Contains("list_dir", dir.Text);
    }

    [Fact]
    public async Task List_dir_hides_build_folders_and_lists_folders_first()
    {
        _ws.Write("src/a.cs", "x");
        _ws.Write("bin/Debug/x.dll", "x");
        _ws.Write("obj/y.txt", "x");
        _ws.Write("zzz.txt", "x");

        var r = await new ListDirToolV2().ExecuteAsync(A(), Ctx(), default);

        Assert.False(r.IsError);
        Assert.Contains("src/", r.Text);
        Assert.DoesNotContain("bin/", r.Text);
        Assert.DoesNotContain("obj/", r.Text);
        Assert.True(r.Text.IndexOf("src/", StringComparison.Ordinal) < r.Text.IndexOf("zzz.txt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_text_finds_matches_and_skips_build_output()
    {
        _ws.Write("src/Foo.cs", "class Foo\n{\n    void Salut() { }\n}\n");
        _ws.Write("obj/Foo.g.cs", "void Salut() { }\n");
        var tool = new SearchTextToolV2();

        var r = await tool.ExecuteAsync(A(("query", "salut")), Ctx(), default);
        Assert.Contains("src/Foo.cs:3:", r.Text);
        Assert.DoesNotContain("obj/", r.Text);

        var none = await tool.ExecuteAsync(A(("query", "introuvableXYZ")), Ctx(), default);
        Assert.Contains("Aucun résultat", none.Text);

        var glob = await tool.ExecuteAsync(A(("query", "class"), ("file_glob", "*.txt")), Ctx(), default);
        Assert.Contains("Aucun résultat", glob.Text);

        var regex = await tool.ExecuteAsync(A(("query", "void\\s+S\\w+"), ("is_regex", true)), Ctx(), default);
        Assert.Contains("src/Foo.cs:3:", regex.Text);
    }

    // ── Modification ────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_file_prepares_a_diff_without_touching_the_file_then_applies_after_approval()
    {
        var path = _ws.Write("a.cs", "class A\n{\n    int x = 1;\n}\n");
        var ctx = Ctx();
        var tool = new EditFileToolV2();

        var prep = await tool.PrepareAsync(A(("path", "a.cs"), ("old_text", "int x = 1;"), ("new_text", "int x = 2;")), ctx, default);

        Assert.NotNull(prep.Change);
        Assert.Contains("-    int x = 1;", prep.Change!.Details);
        Assert.Contains("+    int x = 2;", prep.Change.Details);
        Assert.Equal("+1 −1", prep.Change.Diff!.Summary);
        Assert.Equal("class A\n{\n    int x = 1;\n}\n", File.ReadAllText(path)); // rien d'écrit avant l'accord

        var applied = await prep.Change.ApplyAsync(default);

        Assert.False(applied.IsError, applied.Text);
        Assert.Equal("class A\n{\n    int x = 2;\n}\n", File.ReadAllText(path));
        Assert.Equal(new ChangedFile("a.cs", 1, 1, false), applied.Change);
        Assert.Contains("Passage modifié", applied.Text);

        // Annulation de tout le run : l'original revient.
        Assert.Equal(1, ctx.Backup.Restore());
        Assert.Equal("class A\n{\n    int x = 1;\n}\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task Edit_file_keeps_crlf()
    {
        var path = _ws.Write("crlf.cs", "a\nb\nc\n", crlf: true);
        var prep = await new EditFileToolV2().PrepareAsync(A(("path", "crlf.cs"), ("old_text", "b"), ("new_text", "B")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal("a\r\nB\r\nc\r\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task Edit_file_refuses_when_the_file_changed_while_waiting_for_confirmation()
    {
        var path = _ws.Write("a.cs", "x = 1;\n");
        var prep = await new EditFileToolV2().PrepareAsync(A(("path", "a.cs"), ("old_text", "x = 1;"), ("new_text", "x = 2;")), Ctx(), default);

        File.WriteAllText(path, "x = 1;\n// modifié ailleurs\n"); // un autre agent / l'éditeur

        var applied = await prep.Change!.ApplyAsync(default);
        Assert.True(applied.IsError);
        Assert.Contains("a changé", applied.Text);
        Assert.Equal("x = 1;\n// modifié ailleurs\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task Edit_file_rejects_bad_calls_with_a_helpful_message()
    {
        _ws.Write("a.cs", "x\n");
        var tool = new EditFileToolV2();

        var missingFile = await tool.PrepareAsync(A(("path", "b.cs"), ("old_text", "x"), ("new_text", "y")), Ctx(), default);
        Assert.Contains("write_file", missingFile.Rejected!.Text);

        var noNew = await tool.PrepareAsync(A(("path", "a.cs"), ("old_text", "x")), Ctx(), default);
        Assert.Contains("new_text", noNew.Rejected!.Text);

        var same = await tool.PrepareAsync(A(("path", "a.cs"), ("old_text", "x"), ("new_text", "x")), Ctx(), default);
        Assert.Contains("Aucun changement", same.Rejected!.Text);
    }

    [Fact]
    public async Task Write_file_refuses_a_truncated_rewrite_of_a_long_file()
    {
        _ws.Write("big.cs", string.Join("\n", Enumerable.Range(1, 60).Select(i => $"// ligne {i}")) + "\n");
        var prep = await new WriteFileToolV2().PrepareAsync(
            A(("path", "big.cs"), ("content", "// seulement dix lignes\n" + string.Join("\n", Enumerable.Range(1, 9).Select(i => "//" + i)))), Ctx(), default);

        Assert.Null(prep.Change);
        Assert.Contains("tronqué", prep.Rejected!.Text);
        Assert.Contains("edit_file", prep.Rejected.Text);
    }

    [Fact]
    public async Task Write_file_creates_a_new_file_in_new_folders_and_undo_removes_it()
    {
        var ctx = Ctx();
        var prep = await new WriteFileToolV2().PrepareAsync(A(("path", "nouveau/dossier/N.cs"), ("content", "class N { }\n")), ctx, default);

        Assert.NotNull(prep.Change);
        Assert.Contains("Créer", prep.Change!.Title);
        Assert.False(_ws.Exists("nouveau/dossier/N.cs"));

        var applied = await prep.Change.ApplyAsync(default);
        Assert.False(applied.IsError, applied.Text);
        Assert.True(applied.Change!.Created);
        Assert.Equal("class N { }\n", _ws.Read("nouveau/dossier/N.cs").Replace("\r\n", "\n"));

        ctx.Backup.Restore();
        Assert.False(_ws.Exists("nouveau/dossier/N.cs"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_new_file_takes_the_line_ending_style_of_its_project_neighbours(bool crlf)
    {
        _ws.Write("src/Existing.cs", "class E\n{\n}\n", crlf);
        var prep = await new WriteFileToolV2().PrepareAsync(A(("path", "src/deeper/New.cs"), ("content", "class N\n{\n}\n")), Ctx(), default);
        await prep.Change!.ApplyAsync(default);

        Assert.Equal(crlf, _ws.Read("src/deeper/New.cs").Contains("\r\n"));
    }

    // ── Commandes ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Run_command_shows_the_literal_command_and_folder_then_runs_through_the_injected_runner()
    {
        string? seenCommand = null, seenDir = null;
        var ctx = Ctx((command, dir, _) =>
        {
            seenCommand = command; seenDir = dir;
            return Task.FromResult(new TerminalCommandResult
            {
                ExitCode = 1,
                Output = $"Build...\n{dir}{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}A.cs(3,5): error CS1002: ; attendu [{dir}{Path.DirectorySeparatorChar}P.csproj]\nÉchec de la build.\n",
            });
        });

        var prep = await new RunCommandToolV2().PrepareAsync(A(("command", "dotnet build P.csproj")), ctx, default);
        Assert.Contains("Commande : dotnet build P.csproj", prep.Change!.Details);
        Assert.Contains(ctx.Root, prep.Change.Details);
        Assert.Null(seenCommand); // rien exécuté avant l'accord

        var result = await prep.Change.ApplyAsync(default);

        Assert.Equal("dotnet build P.csproj", seenCommand);
        Assert.Equal(ctx.Root, seenDir);
        Assert.False(result.IsError);                       // un build qui échoue est une information, pas une erreur d'outil
        Assert.Contains("Code de sortie : 1", result.Text);
        Assert.Contains("error CS1002", result.Text);
        Assert.DoesNotContain(ctx.Root, result.Text);       // chemins raccourcis, relatifs au projet
    }

    [Fact]
    public async Task Run_command_flags_dangerous_commands_for_the_human()
    {
        var prep = await new RunCommandToolV2().PrepareAsync(A(("command", "rmdir /s /q build")), Ctx(), default);
        Assert.Contains("supprime", prep.Change!.Details);
        Assert.True(prep.Change.IsDestructive);
    }

    [Fact]
    public void Output_digest_puts_errors_first_and_deduplicates()
    {
        var output = "ligne 1\nA.cs(1,1): error CS0103: nom inconnu\nligne 3\nA.cs(1,1): error CS0103: nom inconnu\nRésumé final\n";
        var digest = RunCommandToolV2.DigestOutput(output, string.Empty);

        Assert.StartsWith("1 ligne(s) d'erreur", digest);
        Assert.Single(digest.Split('\n'), l => l.Contains("CS0103") && l.StartsWith("A.cs", StringComparison.Ordinal));
        Assert.Contains("Résumé final", digest);
    }

    [Fact]
    public void Output_digest_without_errors_keeps_the_tail()
    {
        var digest = RunCommandToolV2.DigestOutput(string.Join("\n", Enumerable.Range(1, 100).Select(i => "sortie " + i)), string.Empty);
        Assert.Contains("sortie 100", digest);
        Assert.DoesNotContain("sortie 10\n", digest + "\n");
    }
}
