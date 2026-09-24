// Moto.Tools.AgentBench/BenchRunner.cs
// Exécute UN essai (un modèle, une tâche, un moteur) dans un dossier jetable et le note.
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using Moto.Core.AI.Autonomy;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Llm;
using Moto.Core.Settings;
using Moto.Editor.Services;

namespace Moto.Tools.AgentBench;

internal sealed class Attempt
{
    public string Model { get; init; } = string.Empty;
    public string Engine { get; init; } = string.Empty;
    public string Task { get; init; } = string.Empty;
    public bool Passed { get; set; }
    public string Check { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Le contrôle objectif (fichiers, compilation…) est passé — indépendamment de la façon dont l'agent a terminé.</summary>
    public bool CheckPassed { get; set; }

    /// <summary>Mode d'appel d'outils réellement utilisé (« native », « structured », « native→structured »).</summary>
    public string ToolMode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Error { get; set; }
    public double Seconds { get; set; }
    public int Steps { get; set; }
    public int ModelCalls { get; set; }
    public int ToolCalls { get; set; }
    public int ToolErrors { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int Added { get; set; }
    public int Removed { get; set; }
    public int FilesChanged { get; set; }
    public int CommandsRefused { get; set; }
    [JsonIgnore] public IReadOnlyList<LlmMessage>? Transcript { get; set; }
    public string? TranscriptFile { get; set; }
}

internal sealed class BenchOptions
{
    public string Endpoint { get; init; } = "http://127.0.0.1:11434";
    public int NumCtx { get; init; } = 16384;
    public string? Think { get; init; } = "false";
    public int MaxSteps { get; init; } = 25;
    public AgentToolMode ToolMode { get; init; } = AgentToolMode.Native;

    /// <summary>Mode structuré : le modèle écrit une phrase de raisonnement avant chaque appel d'outil.</summary>
    public bool Thought { get; init; }
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(8);
    public bool Verbose { get; init; }
}

internal static class BenchRunner
{
    public static async Task<Attempt> RunV2(string model, BenchTask task, Workspace ws, BenchOptions o, CancellationToken ct)
    {
        var attempt = new Attempt { Model = model, Engine = "v2", Task = task.Id };
        using var client = new OllamaChatClient(o.Endpoint);
        var approver = new AutoApprover();
        var loop = new AgentLoopV2(client, approver);

        var request = new AgentRunRequest
        {
            Model = model,
            Goal = task.Goal,
            WorkspaceRoot = ws.Root,
            AgentId = "bench",
            MaxSteps = o.MaxSteps,
            MaxDuration = o.MaxDuration,
            ToolMode = o.ToolMode,
            Options = new LlmOptions { NumCtx = o.NumCtx, Think = o.Think, StructuredThought = o.Thought },
            VerifyCommand = "dotnet build",
            RequireVerification = true,
            WriteAuditLog = false,
            BackupFolder = Path.Combine(ws.Root + "-backups"),
        };

        var clock = Stopwatch.StartNew();
        AgentRunResult result;
        try
        {
            result = await loop.RunAsync(request, e => Log(o, e), ct);
        }
        catch (Exception ex)
        {
            attempt.Outcome = "Exception";
            attempt.Error = ex.Message;
            attempt.Seconds = clock.Elapsed.TotalSeconds;
            attempt.Check = "exception";
            return attempt;
        }
        clock.Stop();

        attempt.Outcome = result.Outcome.ToString();
        attempt.ToolMode = result.ToolMode;
        attempt.Summary = result.Summary;
        attempt.Error = result.Error;
        attempt.Seconds = clock.Elapsed.TotalSeconds;
        attempt.Steps = result.Steps;
        attempt.ModelCalls = result.ModelCalls;
        attempt.ToolCalls = result.ToolCalls;
        attempt.ToolErrors = result.ToolErrors;
        attempt.PromptTokens = result.PromptTokens;
        attempt.CompletionTokens = result.CompletionTokens;
        attempt.CommandsRefused = approver.Refused;
        attempt.Transcript = result.Transcript;

        Score(attempt, task, ws, result.Summary, result.Succeeded);
        return attempt;
    }

    public static async Task<Attempt> RunV1(string model, BenchTask task, Workspace ws, BenchOptions o)
    {
        var attempt = new Attempt { Model = model, Engine = "v1", Task = task.Id };

        var confirmation = new AiConfirmationService
        {
            ConfirmationHandler = req => Task.FromResult(req.Action != ConfirmationAction.ExecuteCommand
                || req.Details.Contains("Commande : dotnet build", StringComparison.OrdinalIgnoreCase)),
        };
        var bus = new AgentMessageBus();
        var tools = new IAgentTool[]
        {
            new ReadFileTool(), new WriteFileTool(), new RunCommandTool(new TerminalService()), new FinishTool(), new SendMessageTool(bus),
        };
        var loop = new BackgroundAgentLoop(new MotoAiKernel(ws.Root), confirmation, tools, bus, new AgentGlobalBudget());
        var run = new AgentRunRecord { AgentId = "bench-v1", Goal = task.Goal };

        string lastFinish = string.Empty;
        var clock = Stopwatch.StartNew();
        try
        {
            await loop.RunAsync(run, ws.Root, msg =>
            {
                if (o.Verbose) Console.WriteLine("    · " + msg.Replace('\n', ' '));
                if (msg.StartsWith("✅", StringComparison.Ordinal)) lastFinish = msg;
            });
        }
        catch (Exception ex)
        {
            attempt.Error = ex.Message;
        }
        clock.Stop();

        attempt.Outcome = run.Status.ToString();
        attempt.Summary = lastFinish;
        attempt.Seconds = clock.Elapsed.TotalSeconds;
        attempt.Steps = run.Steps.Count;
        attempt.ModelCalls = run.Steps.Count;
        attempt.ToolCalls = run.Steps.Count(s => s.ActionKind != AgentActionKind.Malformed);
        attempt.ToolErrors = run.Steps.Count(s => s.ActionKind == AgentActionKind.Malformed);

        Score(attempt, task, ws, lastFinish, run.Status == AgentRunStatus.Completed);
        return attempt;
    }

    private static void Score(Attempt attempt, BenchTask task, Workspace ws, string summary, bool agentSaidDone)
    {
        var diff = ws.DiffFromOriginal();
        attempt.Added = diff.Added;
        attempt.Removed = diff.Removed;
        attempt.FilesChanged = diff.Files;

        try
        {
            // Réussi = le résultat est bon ET l'agent a terminé proprement : des fichiers corrects derrière un run
            // « Failed » ou « LoopDetected » s'affichent comme un échec à l'utilisateur.
            var check = task.Check(ws, summary);
            attempt.CheckPassed = check.Passed;
            attempt.Passed = check.Passed && agentSaidDone;
            attempt.Check = check.Passed && !agentSaidDone
                ? check.Detail + $" — mais l'agent n'a pas terminé proprement ({attempt.Outcome})"
                : check.Detail;
        }
        catch (Exception ex)
        {
            attempt.Passed = false;
            attempt.Check = "contrôle en erreur : " + ex.Message;
        }
    }

    private static void Log(BenchOptions o, AgentEvent e)
    {
        if (!o.Verbose) return;
        switch (e.Kind)
        {
            case AgentEventKind.ToolCalled: Console.WriteLine($"    → {e.Text}"); break;
            case AgentEventKind.ToolProposed: Console.WriteLine($"    ✎ {e.Text}"); break;
            case AgentEventKind.ToolResult when e.IsError: Console.WriteLine($"    ✕ {Cut(e.Text)}"); break;
            case AgentEventKind.Nudge: Console.WriteLine($"    ↻ {e.Text}"); break;
            case AgentEventKind.Finished: Console.WriteLine($"    ■ {Cut(e.Text)}"); break;
        }
    }

    private static string Cut(string s)
    {
        s = s.Replace('\n', ' ');
        return s.Length <= 160 ? s : s[..160] + "…";
    }
}

internal static class Dotnet
{
    public static (int Exit, string Output) Run(string args, string cwd, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo("dotnet", args)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return (-1, "délai dépassé");
        }
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
