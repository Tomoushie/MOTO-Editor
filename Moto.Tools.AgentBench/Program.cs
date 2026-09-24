// Moto.Tools.AgentBench/Program.cs
// dotnet run --project Moto.Tools.AgentBench -- --models qwen2.5-coder:7b,qwen3:8b --engine both --repeat 1 -v
using System.Text;
using System.Text.Json;
using Moto.Core.AI.Llm;

namespace Moto.Tools.AgentBench;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Pas de processus MSBuild/compilateur qui traînent après le banc (et builds plus reproductibles).
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
        Environment.SetEnvironmentVariable("UseSharedCompilation", "false");
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        Environment.SetEnvironmentVariable("DOTNET_NOLOGO", "1");
        Environment.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", "0");

        var opt = Parse(args);
        if (opt.ContainsKey("list"))
        {
            foreach (var t in BenchTasks.All) Console.WriteLine($"{t.Id,-24} {t.Title}");
            return 0;
        }

        var models = (opt.GetValueOrDefault("models") ?? "qwen2.5-coder:7b").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var engines = (opt.GetValueOrDefault("engine") ?? "v2") switch { "both" => new[] { "v1", "v2" }, var e => new[] { e } };
        var wanted = opt.GetValueOrDefault("tasks")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tasks = BenchTasks.All.Where(t => wanted is null || wanted.Contains(t.Id)).ToList();
        var repeat = int.Parse(opt.GetValueOrDefault("repeat") ?? "1");
        var keep = opt.ContainsKey("keep");
        var reportPath = Path.GetFullPath(opt.GetValueOrDefault("out") ?? "agentbench-report.json");

        var options = new BenchOptions
        {
            NumCtx = int.Parse(opt.GetValueOrDefault("num-ctx") ?? "16384"),
            Think = (opt.GetValueOrDefault("think") ?? "false") is "none" ? null : opt.GetValueOrDefault("think") ?? "false",
            MaxSteps = int.Parse(opt.GetValueOrDefault("max-steps") ?? "25"),
            MaxDuration = TimeSpan.FromMinutes(double.Parse(opt.GetValueOrDefault("timeout-min") ?? "8", System.Globalization.CultureInfo.InvariantCulture)),
            Verbose = opt.ContainsKey("v"),
        };

        using var probe = new OllamaChatClient(options.Endpoint);
        if (!await probe.IsAvailableAsync())
        {
            Console.Error.WriteLine($"Ollama ne répond pas sur {options.Endpoint}.");
            return 2;
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var tempRoot = Path.Combine(Path.GetTempPath(), "moto-agentbench-" + stamp);
        Directory.CreateDirectory(tempRoot);
        var transcriptDir = Path.Combine(Path.GetDirectoryName(reportPath)!, Path.GetFileNameWithoutExtension(reportPath) + "-transcripts");

        Console.WriteLine($"Banc d'essai de l'agent — {tasks.Count} tâche(s), modèles : {string.Join(", ", models)}, moteurs : {string.Join("+", engines)}, x{repeat}");
        Console.WriteLine($"Contexte {options.NumCtx}, think={options.Think ?? "(non envoyé)"}, {options.MaxSteps} pas max, {options.MaxDuration.TotalMinutes:0.#} min max");

        // Projet de référence : construit une fois ; sa sortie sert de référence pour les refactorisations.
        var templateDir = Path.Combine(tempRoot, "template");
        Fixture.WriteTo(templateDir);
        var (buildExit, buildOut) = Dotnet.Run("build --nologo -v q", templateDir, TimeSpan.FromMinutes(5));
        if (buildExit != 0) { Console.Error.WriteLine("Le projet de référence ne compile pas :\n" + buildOut); return 3; }
        var (runExit, baseline) = Dotnet.Run("run --no-build", templateDir, TimeSpan.FromMinutes(1));
        if (runExit != 0) { Console.Error.WriteLine("Le projet de référence plante :\n" + baseline); return 3; }
        Console.WriteLine("Projet de référence : compile, sortie mémorisée.\n");

        var attempts = new List<Attempt>();
        var counter = 0;
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        foreach (var model in models)
        {
            if (engines.Contains("v2")) await WarmUpAsync(options, model);

            foreach (var task in tasks)
            foreach (var engine in engines)
            for (var r = 1; r <= repeat; r++)
            {
                if (cts.IsCancellationRequested) goto done;
                var dir = Path.Combine(tempRoot, $"agentbench-{++counter:000}");
                CopyDirectory(templateDir, dir);
                var ws = new Workspace(dir, Fixture.Files(), baseline);
                task.Setup?.Invoke(ws);

                Console.WriteLine($"[{model}] {task.Id} ({engine}{(repeat > 1 ? $" #{r}" : "")})");
                var attempt = engine == "v1"
                    ? await BenchRunner.RunV1(model, task, ws, options)
                    : await BenchRunner.RunV2(model, task, ws, options, cts.Token);
                attempts.Add(attempt);

                if (!attempt.Passed && attempt.Transcript is { Count: > 0 })
                {
                    Directory.CreateDirectory(transcriptDir);
                    attempt.TranscriptFile = Path.Combine(transcriptDir, $"{counter:000}-{engine}-{task.Id}.json");
                    File.WriteAllText(attempt.TranscriptFile, JsonSerializer.Serialize(
                        attempt.Transcript.Select(m => new { m.Role, m.Content, m.ToolName, ToolCalls = m.ToolCalls?.Select(c => new { c.Name, Arguments = c.Arguments.ToJsonString() }) }),
                        new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                }

                Console.WriteLine($"  {(attempt.Passed ? "✓ RÉUSSI" : "✗ ÉCHEC")}  {attempt.Seconds:0.#}s  {attempt.Outcome}  pas={attempt.Steps} outils={attempt.ToolCalls} erreurs={attempt.ToolErrors} " +
                                  $"jetons={attempt.PromptTokens}/{attempt.CompletionTokens}  diff=+{attempt.Added} −{attempt.Removed} ({attempt.FilesChanged} fichier(s))");
                Console.WriteLine($"  contrôle : {attempt.Check}");
                if (!string.IsNullOrEmpty(attempt.Error)) Console.WriteLine($"  erreur : {attempt.Error}");
                Console.WriteLine();
            }
        }

    done:
        PrintSummary(attempts);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { date = stamp, options, attempts }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"Rapport : {reportPath}");

        CleanAuditFiles();
        if (!keep) { try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { } }
        else Console.WriteLine($"Dossiers conservés : {tempRoot}");
        return 0;
    }

    private static async Task WarmUpAsync(BenchOptions o, string model)
    {
        try
        {
            using var client = new OllamaChatClient(o.Endpoint);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await client.ChatAsync(model, new[] { LlmMessage.User("Réponds OK.") }, options: new LlmOptions { NumCtx = o.NumCtx, NumPredict = 4, Think = o.Think });
            Console.WriteLine($"[{model}] chargé en mémoire en {clock.Elapsed.TotalSeconds:0.#}s\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{model}] préchauffage impossible : {ex.Message}\n");
        }
    }

    private static void PrintSummary(List<Attempt> attempts)
    {
        Console.WriteLine("════════════ RÉSUMÉ ════════════");
        foreach (var g in attempts.GroupBy(a => (a.Model, a.Engine)))
        {
            var passed = g.Count(a => a.Passed);
            Console.WriteLine($"{g.Key.Model,-34} {g.Key.Engine}  {passed}/{g.Count()} réussies ({100.0 * passed / g.Count():0}%)   " +
                              $"temps moyen {g.Average(a => a.Seconds):0.#}s   erreurs d'outils {g.Sum(a => a.ToolErrors)}   jetons {g.Sum(a => a.PromptTokens)}/{g.Sum(a => a.CompletionTokens)}");
            Console.WriteLine("    " + string.Join("  ", g.Select(a => $"{(a.Passed ? "✓" : "✗")}{a.Task}")));
        }
        Console.WriteLine();
    }

    /// <summary>Le moteur v1 écrit un journal d'audit dans %LOCALAPPDATA% ; on retire ceux du banc.</summary>
    private static void CleanAuditFiles()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(AgentAuditFolder, "agentbench-*.ndjson")) File.Delete(f);
        }
        catch (Exception) { /* dossier absent : rien à nettoyer */ }
    }

    private static string AgentAuditFolder => Moto.Core.AI.Autonomy.AgentAuditLog.BaseFolder;

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var map = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith('-')) continue;
            var key = args[i].TrimStart('-');
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--") && args[i + 1] != "-v";
            map[key] = hasValue ? args[++i] : string.Empty;
        }
        return map;
    }
}
