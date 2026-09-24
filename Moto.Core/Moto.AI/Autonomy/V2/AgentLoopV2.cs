// Moto.Core/Moto.AI/Autonomy/V2/AgentLoopV2.cs
// ★ AJOUT (24/09, "écriture agentique fonctionnelle") : la boucle de l'agent v2.
//
// Ce qu'elle change par rapport à BackgroundAgentLoop (v1), chaque point MESURÉ ou observé le 24/09 :
//  - appels d'outils NATIFS (Ollama /api/chat) au lieu d'un protocole texte « ACTION: … » à parser ;
//  - vraie conversation (messages system/user/assistant/tool) au lieu d'un prompt reconstruit à chaque pas
//    — le modèle voit ses propres appels et leurs résultats tels quels ;
//  - fenêtre de contexte demandée (16 k) et surveillée (ContextTrimmer) — la v1 gardait celle d'Ollama (~4 k) ;
//  - lecture par tranches numérotées, modification par « remplace ce passage » (edit_file) avec un DIFF montré
//    avant confirmation — la v1 réécrivait le fichier entier, plafonné à 256 jetons de réponse ;
//  - l'agent peut explorer (list_dir, search_text), compiler (run_command) et corriger.
// Ce qui ne change pas : rien d'écrit sans accord humain, chemins confinés au projet, journal d'audit NDJSON.
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Moto.Core.AI.Llm;
using Moto.Editor.Services; // TerminalCommandResult

namespace Moto.Core.AI.Autonomy.V2;

public sealed class AgentLoopV2
{
    private const int MaxConsecutiveDeclines = 3;
    private const int MaxConsecutiveToolErrors = 6;
    private const int MaxCallsPerMessage = 6;
    private const int LoopWarnAt = 3;
    private const int LoopStopAt = 5;

    private static readonly string[] CodeExtensions = { ".cs", ".xaml", ".csproj", ".razor" };

    private readonly OllamaChatClient _client;
    private readonly IAgentApprover _approver;
    private readonly IReadOnlyList<AgentToolV2> _tools;
    private readonly Func<string, string, CancellationToken, Task<TerminalCommandResult>>? _runCommand;

    public AgentLoopV2(
        OllamaChatClient client,
        IAgentApprover approver,
        IReadOnlyList<AgentToolV2>? tools = null,
        Func<string, string, CancellationToken, Task<TerminalCommandResult>>? runCommand = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _approver = approver ?? throw new ArgumentNullException(nameof(approver));
        _tools = tools ?? AgentToolSetV2.Default();
        _runCommand = runCommand;
    }

    // ── État d'un run ───────────────────────────────────────────────────────

    private sealed class RunState
    {
        public required AgentRunRequest Request { get; init; }
        public required AgentToolContext Ctx { get; init; }
        public required List<LlmMessage> Messages { get; init; }
        public required string RunId { get; init; }
        public required Action<AgentEvent>? Emit { get; init; }
        public AgentAuditLog? Audit { get; init; }
        public Stopwatch Clock { get; } = Stopwatch.StartNew();

        public int Step, ModelCalls, ToolCalls, ToolErrors, PromptTokens, CompletionTokens;
        public double ModelSeconds;
        public int ConsecutiveErrors, ConsecutiveDeclines, Nudges;
        public int FileWriteAttempts;
        public string? LastWriteError;
        public bool VerifyNudged, HonestyNudged;
        public bool VerifiedSinceLastChange = true;
        public readonly Dictionary<string, ChangedFile> Changes = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> RecentSignatures = new();
    }

    private sealed record CallOutcome(ToolResult Result, bool Finished = false, string? Summary = null, AgentOutcome? Stop = null, string? StopReason = null);

    // ── Point d'entrée ──────────────────────────────────────────────────────

    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, Action<AgentEvent>? onEvent = null, CancellationToken ct = default)
    {
        var runId = request.RunId ?? Guid.NewGuid().ToString("N")[..10];
        var root = Path.GetFullPath(AgentPathResolver.EffectiveRoot(request.WorkspaceRoot));
        var ctx = new AgentToolContext(root, request.AgentId, new RunBackup(root, runId, request.BackupFolder), _runCommand);

        var state = new RunState
        {
            Request = request,
            Ctx = ctx,
            RunId = runId,
            Emit = onEvent,
            Audit = request.WriteAuditLog ? new AgentAuditLog(root) : null,
            Messages = new List<LlmMessage>
            {
                LlmMessage.System(BuildSystemPrompt(root)),
                LlmMessage.User(BuildUserPrompt(request)),
            },
        };

        try
        {
            return await LoopAsync(state, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Finish(state, AgentOutcome.Cancelled, "Arrêté par l'utilisateur.");
        }
        catch (LlmException ex)
        {
            return Finish(state, AgentOutcome.Failed, "Le modèle n'a pas pu répondre.", ex.Message);
        }
        catch (Exception ex)
        {
            return Finish(state, AgentOutcome.Failed, "Erreur inattendue.", ex.Message);
        }
    }

    private async Task<AgentRunResult> LoopAsync(RunState st, CancellationToken ct)
    {
        var req = st.Request;
        var specs = _tools.Select(t => t.Spec).ToList();
        var specChars = specs.Sum(s => s.Name.Length + s.Description.Length + s.Parameters.ToJsonString().Length);
        var known = new HashSet<string>(_tools.Select(t => t.Name), StringComparer.Ordinal);

        Emit(st, AgentEventKind.Started, $"Agent « {req.AgentId} » : {req.Goal}");
        Audit(st, new { kind = "start", goal = req.Goal, model = req.Model });

        for (st.Step = 1; st.Step <= req.MaxSteps; st.Step++)
        {
            ct.ThrowIfCancellationRequested();
            if (st.Clock.Elapsed > req.MaxDuration)
                return Finish(st, AgentOutcome.TimeLimit, "Durée maximale atteinte.");

            if (!ContextTrimmer.Fit(st.Messages, req.Options.NumCtx, specChars))
                return Finish(st, AgentOutcome.Failed, "Le contexte du modèle est saturé.",
                    "La conversation ne tient plus dans la fenêtre du modèle, même après avoir retiré les anciens résultats.");

            var reply = await _client.ChatAsync(req.Model, st.Messages, specs, req.Options,
                onContent: chunk => Emit(st, AgentEventKind.Text, chunk), ct: ct);

            st.ModelCalls++;
            st.PromptTokens += reply.PromptTokens;
            st.CompletionTokens += reply.CompletionTokens;
            st.ModelSeconds += reply.TotalSeconds;

            var calls = reply.ToolCalls;
            var content = reply.Content;
            if (calls.Count == 0 && content.Length > 0)
            {
                var (salvaged, remaining) = ToolCallSalvage.Extract(content, known);
                if (salvaged.Count > 0) { calls = salvaged; content = remaining; }
            }

            st.Messages.Add(LlmMessage.Assistant(content, calls.Count > 0 ? calls : null));

            if (calls.Count == 0)
            {
                // Réponse en texte seul. Un modèle qui annonce « je vais… » sans appeler d'outil n'a rien fait :
                // on le relance UNE fois ; sinon c'est sa réponse finale.
                if (st.ToolCalls == 0 && st.Nudges == 0)
                {
                    st.Nudges++;
                    Emit(st, AgentEventKind.Nudge, "Aucun outil appelé : relance.");
                    st.Messages.Add(LlmMessage.User(
                        "Tu n'as appelé aucun outil. Si la tâche demande de lire, chercher ou modifier des fichiers, appelle l'outil MAINTENANT. " +
                        "Si tu as déjà la réponse, appelle finish avec ta réponse."));
                    continue;
                }
                return Finish(st, AgentOutcome.Completed, content.Trim().Length > 0 ? content.Trim() : "Terminé.");
            }

            var index = 0;
            foreach (var call in calls)
            {
                ct.ThrowIfCancellationRequested();

                if (++index > MaxCallsPerMessage)
                {
                    st.Messages.Add(LlmMessage.Tool(call.Name, $"Ignoré : {MaxCallsPerMessage} appels d'outils maximum par message. Refais celui-ci au prochain pas."));
                    continue;
                }

                var outcome = await ExecuteCallAsync(st, call, ct);
                st.Messages.Add(LlmMessage.Tool(call.Name, outcome.Result.Text));

                if (outcome.Finished) return Finish(st, AgentOutcome.Completed, outcome.Summary ?? "Terminé.");
                if (outcome.Stop is { } stop)
                    return Finish(st, stop, outcome.StopReason ?? string.Empty, stop == AgentOutcome.Failed ? outcome.StopReason : null);
            }
        }

        return Finish(st, AgentOutcome.StepLimit, "Nombre maximal d'étapes atteint.");
    }

    // ── Un appel d'outil ────────────────────────────────────────────────────

    private async Task<CallOutcome> ExecuteCallAsync(RunState st, LlmToolCall call, CancellationToken ct)
    {
        st.ToolCalls++;
        var tool = _tools.FirstOrDefault(t => t.Name == call.Name);
        Emit(st, AgentEventKind.ToolCalled, Describe(call), call.Name, ToolArgs.Str(call.Arguments, "path"));

        if (tool is null)
            return Failed(st, call, $"Outil inconnu « {call.Name} ». Outils disponibles : {string.Join(", ", _tools.Select(t => t.Name))}.");

        // Garde anti-boucle : le même appel exact, encore et encore, sans qu'aucune modification n'ait eu lieu entre-temps.
        var signature = call.Name + "|" + call.Arguments.ToJsonString();
        st.RecentSignatures.Add(signature);
        var repeats = st.RecentSignatures.Count(s => s == signature);
        if (repeats >= LoopStopAt && call.Name != AgentToolSetV2.FinishName)
            return new CallOutcome(ToolResult.Error("Boucle détectée."), Stop: AgentOutcome.LoopDetected,
                StopReason: $"Le modèle répète {repeats} fois le même appel ({call.Name}) sans progrès.");
        var loopWarning = repeats >= LoopWarnAt && call.Name != AgentToolSetV2.FinishName
            ? $"\n⚠ Tu as déjà fait exactement cet appel {repeats - 1} fois : change d'approche (autre passage, autre outil) ou appelle finish."
            : string.Empty;

        // finish : avant de laisser terminer, (1) ne pas laisser croire à une modification qui a échoué,
        // (2) s'assurer une fois que le code modifié a été compilé.
        if (call.Name == AgentToolSetV2.FinishName)
        {
            if (st.Changes.Count == 0 && st.FileWriteAttempts > 0 && !st.HonestyNudged)
            {
                st.HonestyNudged = true;
                Emit(st, AgentEventKind.Nudge, "Aucun fichier modifié : rappel avant de terminer.");
                return new CallOutcome(ToolResult.Ok(
                    $"Attention : AUCUN fichier n'a été modifié (tes tentatives ont échoué ou ont été refusées ; dernière erreur : « {Cut(st.LastWriteError ?? "?", 200)} »). " +
                    "Corrige ton appel et réessaie. Si tu ne peux vraiment pas, appelle finish en disant HONNÊTEMENT que rien n'a été modifié."));
            }

            if (NeedsVerification(st))
            {
                st.VerifyNudged = true;
                var command = string.IsNullOrWhiteSpace(st.Request.VerifyCommand) ? "dotnet build" : st.Request.VerifyCommand;
                var text = $"Pas encore : tu as modifié du code ({string.Join(", ", st.Changes.Keys.Take(4))}) sans vérifier qu'il compile. " +
                           $"Lance run_command avec « {command} », corrige les erreurs s'il y en a, puis rappelle finish.";
                Emit(st, AgentEventKind.Nudge, "Vérification demandée avant de terminer.");
                return new CallOutcome(ToolResult.Ok(text));
            }

            var finish = await tool.ExecuteAsync(call.Arguments, st.Ctx, ct);
            Audit(st, new { kind = "finish", step = st.Step, summary = finish.Text });
            return new CallOutcome(finish, Finished: true, Summary: finish.Text);
        }

        ToolResult result;
        if (!tool.IsMutating)
        {
            result = await SafeExecuteAsync(() => tool.ExecuteAsync(call.Arguments, st.Ctx, ct));
            Audit(st, new
            {
                kind = "non_mutating_step", step = st.Step, tool = call.Name, path = ToolArgs.Str(call.Arguments, "path"),
                error = result.IsError, observation = Cut(result.Text, 300),
            });
        }
        else
        {
            ToolPreparation preparation;
            try { preparation = await tool.PrepareAsync(call.Arguments, st.Ctx, ct); }
            catch (OperationCanceledException) { throw; }
            catch (ToolPathException ex) { preparation = ToolPreparation.Reject(ex.Message); }
            catch (Exception ex) { preparation = ToolPreparation.Reject($"Erreur de l'outil : {ex.Message}"); }

            if (tool.WritesFiles) st.FileWriteAttempts++;

            if (preparation.Change is null)
            {
                result = preparation.Rejected ?? ToolResult.Error("Rien à appliquer.");
            }
            else
            {
                result = await ProposeAndApplyAsync(st, call, preparation.Change, ct);
                if (st.ConsecutiveDeclines >= MaxConsecutiveDeclines)
                    return new CallOutcome(result, Stop: AgentOutcome.Declined,
                        StopReason: $"Arrêté après {MaxConsecutiveDeclines} refus consécutifs.");
            }
        }

        if (result.IsError && tool.WritesFiles) st.LastWriteError = result.Text;
        return Complete(st, call, result, loopWarning);
    }

    private async Task<ToolResult> ProposeAndApplyAsync(RunState st, LlmToolCall call, PendingChange change, CancellationToken ct)
    {
        Emit(st, AgentEventKind.ToolProposed, change.Summary, call.Name, change.Path);
        Audit(st, new { kind = "proposal", step = st.Step, tool = call.Name, path = change.Path, summary = change.Summary });

        bool approved;
        try
        {
            approved = await _approver.ApproveAsync(new ApprovalRequest
            {
                AgentId = st.Request.AgentId,
                Title = change.Title,
                Summary = change.Summary,
                Details = change.Details,
                Kind = change.Kind,
                IsDestructive = change.IsDestructive,
                Path = change.Path,
                Diff = change.Diff,
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            approved = false; // en cas de doute sur la validation, on n'écrit pas
        }

        Audit(st, new { kind = "decision", step = st.Step, approved });

        if (!approved)
        {
            st.ConsecutiveDeclines++;
            Emit(st, AgentEventKind.ToolDeclined, change.Summary, call.Name, change.Path);
            return ToolResult.Error("Refusé par l'utilisateur. Ne refais pas la même modification : propose une autre approche, ou appelle finish si tu ne peux pas continuer.");
        }

        st.ConsecutiveDeclines = 0;
        Emit(st, AgentEventKind.ToolApproved, change.Summary, call.Name, change.Path);

        ToolResult applied;
        try { applied = await change.ApplyAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { applied = ToolResult.Error($"Erreur pendant l'application : {ex.Message}"); }

        Audit(st, new { kind = "execution", step = st.Step, error = applied.IsError, observation = Cut(applied.Text, 400) });

        if (applied.Change is { } changed)
        {
            st.Changes[changed.RelativePath] = st.Changes.TryGetValue(changed.RelativePath, out var before)
                ? new ChangedFile(changed.RelativePath, before.Added + changed.Added, before.Removed + changed.Removed, before.Created || changed.Created)
                : changed;
            st.VerifiedSinceLastChange = false;
            st.RecentSignatures.Clear(); // le fichier a changé : relire ou refaire un appel n'est plus une « répétition »
        }
        else if (!applied.IsError && call.Name == "run_command" && LooksLikeVerification(ToolArgs.Str(call.Arguments, "command")))
        {
            st.VerifiedSinceLastChange = true;
        }

        return applied;
    }

    private CallOutcome Complete(RunState st, LlmToolCall call, ToolResult result, string loopWarning)
    {
        var text = result.Text + loopWarning;

        if (result.IsError)
        {
            st.ToolErrors++;
            st.ConsecutiveErrors++;
            if (st.ConsecutiveErrors >= MaxConsecutiveToolErrors)
                return new CallOutcome(ToolResult.Error(text), Stop: AgentOutcome.Failed,
                    StopReason: $"{MaxConsecutiveToolErrors} erreurs d'outils d'affilée : le modèle n'arrive pas à s'en sortir. Dernière erreur : {Cut(result.Text, 200)}");
            if (st.ConsecutiveErrors >= 3)
                text += "\n⚠ Plusieurs erreurs d'affilée : relis le fichier concerné avec read_file avant de réessayer.";
        }
        else
        {
            st.ConsecutiveErrors = 0;
        }

        Emit(st, AgentEventKind.ToolResult, Cut(result.Text, 400), call.Name, ToolArgs.Str(call.Arguments, "path"), result.IsError);
        return new CallOutcome(new ToolResult(result.IsError, text, result.Change));
    }

    private CallOutcome Failed(RunState st, LlmToolCall call, string message)
        => Complete(st, call, ToolResult.Error(message), string.Empty);

    /// <summary>Un outil qui lève une exception ne doit jamais faire tomber le run : le modèle reçoit l'erreur.</summary>
    private static async Task<ToolResult> SafeExecuteAsync(Func<Task<ToolResult>> action)
    {
        try { return await action(); }
        catch (OperationCanceledException) { throw; }
        catch (ToolPathException ex) { return ToolResult.Error(ex.Message); }
        catch (Exception ex) { return ToolResult.Error($"Erreur de l'outil : {ex.Message}"); }
    }

    // ── Vérification avant de terminer ──────────────────────────────────────

    private bool NeedsVerification(RunState st)
        => st.Request.RequireVerification
           && !st.VerifyNudged
           && !st.VerifiedSinceLastChange
           && st.Changes.Keys.Any(p => CodeExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
           && _tools.Any(t => t.Name == "run_command");

    private static bool LooksLikeVerification(string? command)
        => command is not null && (command.Contains("build", StringComparison.OrdinalIgnoreCase)
                                   || command.Contains("test", StringComparison.OrdinalIgnoreCase)
                                   || command.Contains("msbuild", StringComparison.OrdinalIgnoreCase)
                                   || command.Contains("tsc", StringComparison.OrdinalIgnoreCase));

    // ── Consignes ───────────────────────────────────────────────────────────

    internal static string BuildSystemPrompt(string root)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tu es l'agent de code de MOTO Editor. Tu travailles dans le projet situé dans : {root}");
        sb.AppendLine("Tu atteins l'objectif en appelant des outils, pas à pas, jusqu'à ce que ce soit fait.");
        sb.AppendLine();
        sb.AppendLine("MÉTHODE");
        sb.AppendLine("1. Explore : list_dir pour voir l'arborescence, search_text pour retrouver un nom, read_file pour lire. Lis TOUJOURS un fichier avant de le modifier.");
        sb.AppendLine("2. Pour REMPLACER du code : edit_file (old_text = le passage recopié EXACTEMENT depuis read_file, sans les numéros de ligne ni le « | » ; new_text = son remplacement). Pour AJOUTER du code sans rien remplacer : insert_lines (le texte est inséré AVANT le numéro de ligne donné). Change le MINIMUM de lignes ; ne réécris jamais un fichier entier pour en changer quelques lignes.");
        sb.AppendLine("3. write_file sert uniquement à créer un fichier NEUF.");
        sb.AppendLine("4. Après avoir modifié du code, vérifie avec run_command (par exemple dotnet build) et corrige les erreurs signalées.");
        sb.AppendLine("5. Quand c'est terminé, appelle finish avec un résumé court en français.");
        sb.AppendLine();
        sb.AppendLine("RÈGLES");
        sb.AppendLine("- Chemins toujours RELATIFS au projet (Dossier/Fichier.cs), jamais absolus.");
        sb.AppendLine("- L'utilisateur voit chaque modification sous forme de diff et l'accepte ou la refuse ; si elle est refusée, propose autre chose au lieu de recommencer à l'identique.");
        sb.AppendLine("- Si un outil répond par une erreur, lis le message et corrige ton appel ; ne répète jamais l'appel qui vient d'échouer.");
        sb.AppendLine("- Ne dis JAMAIS qu'un fichier est modifié tant que l'outil n'a pas répondu « Fichier modifié » ou « Fichier créé ».");
        sb.AppendLine("- Réponds en français. Pas de longs discours : agis avec les outils.");
        return sb.ToString().TrimEnd();
    }

    internal static string BuildUserPrompt(AgentRunRequest req)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Objectif : " + req.Goal.Trim());
        if (!string.IsNullOrWhiteSpace(req.Context))
        {
            sb.AppendLine();
            sb.AppendLine(req.Context.Trim());
        }
        if (!string.IsNullOrWhiteSpace(req.VerifyCommand))
        {
            sb.AppendLine();
            sb.AppendLine($"Pour vérifier que le code compile : {req.VerifyCommand.Trim()}");
        }
        return sb.ToString().TrimEnd();
    }

    // ── Résultat, événements, journal ───────────────────────────────────────

    private AgentRunResult Finish(RunState st, AgentOutcome outcome, string summary, string? error = null)
    {
        Emit(st, AgentEventKind.Finished, summary);
        Audit(st, new { kind = "end", outcome = outcome.ToString(), summary = Cut(summary, 300), error });

        return new AgentRunResult
        {
            Outcome = outcome,
            Summary = summary,
            Error = error,
            Warning = outcome == AgentOutcome.Completed && st.Changes.Count == 0 && st.FileWriteAttempts > 0
                ? $"Aucune modification n'a été appliquée (dernière erreur : {Cut(st.LastWriteError ?? "refus", 160)})."
                : null,
            RunId = st.RunId,
            Steps = Math.Min(st.Step, st.Request.MaxSteps),
            ModelCalls = st.ModelCalls,
            ToolCalls = st.ToolCalls,
            ToolErrors = st.ToolErrors,
            PromptTokens = st.PromptTokens,
            CompletionTokens = st.CompletionTokens,
            Elapsed = st.Clock.Elapsed,
            ModelSeconds = st.ModelSeconds,
            Changes = st.Changes.Values.ToList(),
            Backup = st.Ctx.Backup,
            Transcript = st.Messages,
        };
    }

    private static void Emit(RunState st, AgentEventKind kind, string text, string? tool = null, string? path = null, bool isError = false)
    {
        try { st.Emit?.Invoke(new AgentEvent(kind, st.Step, text, tool, path, isError)); }
        catch (Exception) { /* un observateur défaillant (interface fermée…) ne doit pas arrêter l'agent */ }
    }

    private static void Audit(RunState st, object entry)
    {
        if (st.Audit is null) return;
        var json = System.Text.Json.JsonSerializer.SerializeToNode(entry) as JsonObject ?? new JsonObject();
        json["ts"] = DateTime.UtcNow;
        json["agentId"] = st.Request.AgentId;
        json["runId"] = st.RunId;
        json["engine"] = "v2";
        st.Audit.Append(json);
    }

    private static string Describe(LlmToolCall call)
    {
        var key = ToolArgs.Str(call.Arguments, "path") ?? ToolArgs.Str(call.Arguments, "query") ?? ToolArgs.Str(call.Arguments, "command");
        return key is null ? call.Name : $"{call.Name} {Cut(key, 80)}";
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
