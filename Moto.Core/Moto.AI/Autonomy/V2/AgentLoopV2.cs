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
using System.Text.RegularExpressions;
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
    private const int MaxNoChangeNudges = 2;
    private const int MaxEchoNudges = 2;
    private const int MaxAnnounceNudges = 2;
    private const int MaxErrorNudges = 2;
    private const int MaxVerifyNudges = 4;
    private const int MaxGlitchRetries = 2;
    private const int ExploreWarnAt = 6;

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
        public int ConsecutiveErrors, ConsecutiveDeclines, Nudges, NoChangeNudges, EchoNudges, AnnounceNudges, ErrorNudges, VerifyNudges;
        public int FileWriteAttempts;

        /// <summary>Comment les appels d'outils sont obtenus à cet instant (peut passer de natif à structuré en cours de run).</summary>
        public LlmToolMode Mode;
        public bool SwitchedMode;
        public int TextOnlyStreak;

        /// <summary>Appels consécutifs d'outils de lecture (list_dir, read_file, search_text) sans aucune écriture entre eux.</summary>
        public int ExploreStreak;
        public string? VerifyCommand;
        public string? LastWriteError;
        public bool HonestyNudged;
        public bool LastResultWasError;
        public bool HadBuildFailure;
        public string? LastBuildErrors;
        public VerifyStatus Verify = VerifyStatus.Passed;
        public readonly Dictionary<string, ChangedFile> Changes = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> RecentSignatures = new();
    }

    /// <summary>Où en est la vérification du code modifié : rien à vérifier / modifié depuis la dernière vérification / échec / réussie.</summary>
    private enum VerifyStatus { Passed, Unverified, Failed }

    private sealed record CallOutcome(ToolResult Result, bool Finished = false, string? Summary = null, AgentOutcome? Stop = null, string? StopReason = null);

    // ── Point d'entrée ──────────────────────────────────────────────────────

    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, Action<AgentEvent>? onEvent = null, CancellationToken ct = default)
    {
        var runId = request.RunId ?? Guid.NewGuid().ToString("N")[..10];
        var root = Path.GetFullPath(AgentPathResolver.EffectiveRoot(request.WorkspaceRoot));
        var ctx = new AgentToolContext(root, request.AgentId, new RunBackup(root, runId, request.BackupFolder), _runCommand);

        var overview = WorkspaceOverview.Scan(root);
        var verify = string.IsNullOrWhiteSpace(request.VerifyCommand) ? overview.SuggestedBuild : request.VerifyCommand.Trim();

        var state = new RunState
        {
            Request = request,
            Ctx = ctx,
            RunId = runId,
            Emit = onEvent,
            Audit = request.WriteAuditLog ? new AgentAuditLog(root) : null,
            VerifyCommand = verify,
            Messages = new List<LlmMessage>
            {
                LlmMessage.System(BuildSystemPrompt(root)),
                LlmMessage.User(BuildUserPrompt(request, overview, verify)),
            },
        };

        state.Mode = request.ToolMode == AgentToolMode.Structured ? LlmToolMode.Structured : LlmToolMode.Native;

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

            var reply = await CallModelAsync(st, specs, ct);

            // Mode structuré : pas de flux de texte, mais la phrase de raisonnement (« pensee »), si elle est demandée, est montrée.
            if (st.Mode == LlmToolMode.Structured && reply.ToolCalls.Count > 0 && !string.IsNullOrWhiteSpace(reply.Content))
                Emit(st, AgentEventKind.Text, reply.Content);

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
                // Réponse en texte seul. Ce n'est la fin que si la tâche l'est vraiment : un petit modèle annonce
                // « je vais… », recopie le fichier lu, s'excuse d'une erreur d'outil ou déclare fini un code qui ne compile pas.
                var verdict = JudgeTextOnly(st, content);
                if (verdict.FailSummary is not null)
                    return Finish(st, AgentOutcome.Failed, verdict.FailSummary, verdict.FailError);
                if (verdict.Nudge is not null)
                {
                    st.Messages.Add(LlmMessage.User(verdict.Nudge));

                    // Relancé deux fois d'affilée sans appeler d'outil : ce modèle « raconte » au lieu d'agir. La sortie
                    // contrainte l'empêche d'écrire autre chose qu'un appel d'outil valide.
                    if (++st.TextOnlyStreak >= 2 && req.ToolMode == AgentToolMode.Auto && st.Mode == LlmToolMode.Native)
                        SwitchToStructured(st, "Le modèle n'appelle pas les outils malgré les relances");
                    continue;
                }
                return Finish(st, AgentOutcome.Completed, content.Trim().Length > 0 ? content.Trim() : "Terminé.");
            }

            st.TextOnlyStreak = 0;

            // Les appels d'un même message sont exécutés dans l'ordre, mais un petit modèle enchaîne parfois
            // « modifie puis compile » ou deux insertions dans le même fichier SANS avoir vu le résultat de la première.
            var index = 0;
            var wroteInThisMessage = false;
            var touchedInThisMessage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var call in calls)
            {
                ct.ThrowIfCancellationRequested();

                if (++index > MaxCallsPerMessage)
                {
                    st.Messages.Add(LlmMessage.Tool(call.Name, $"Ignoré : {MaxCallsPerMessage} appels d'outils maximum par message. Refais celui-ci au prochain pas."));
                    continue;
                }

                var tool = _tools.FirstOrDefault(t => t.Name == call.Name);
                var pathKey = NormalizePath(ToolArgs.Str(call.Arguments, "path"));
                if (tool is not null && SkipInThisMessage(tool, pathKey, wroteInThisMessage, touchedInThisMessage) is { } skipped)
                {
                    Emit(st, AgentEventKind.ToolResult, skipped, call.Name, ToolArgs.Str(call.Arguments, "path"), isError: false);
                    st.Messages.Add(LlmMessage.Tool(call.Name, skipped));
                    continue;
                }

                var outcome = await ExecuteCallAsync(st, call, ct);
                st.Messages.Add(LlmMessage.Tool(call.Name, outcome.Result.Text));

                if (tool is { WritesFiles: true })
                {
                    wroteInThisMessage = true;
                    if (pathKey.Length > 0) touchedInThisMessage.Add(pathKey);
                    if (outcome.Result.Changes is { } touchedMany)
                        foreach (var c in touchedMany) touchedInThisMessage.Add(NormalizePath(c.RelativePath));
                }

                if (outcome.Finished) return Finish(st, AgentOutcome.Completed, outcome.Summary ?? "Terminé.");
                if (outcome.Stop is { } stop)
                    return Finish(st, stop, outcome.StopReason ?? string.Empty, stop == AgentOutcome.Failed ? outcome.StopReason : null);
            }
        }

        return Finish(st, AgentOutcome.StepLimit, "Nombre maximal d'étapes atteint.");
    }

    /// <summary>
    /// Un appel au modèle, avec deux reprises : (1) modèle SANS la capacité « tools » → sortie contrainte (mode Auto) ;
    /// (2) génération interrompue parce que le modèle s'est emballé (« token repeat limit ») → même appel à température plus haute.
    /// </summary>
    private async Task<LlmReply> CallModelAsync(RunState st, IReadOnlyList<LlmToolSpec> specs, CancellationToken ct)
    {
        var req = st.Request;
        var glitches = 0;
        while (true)
        {
            var options = glitches == 0 ? req.Options : req.Options.WithTemperature(Math.Min(1.0, req.Options.Temperature + 0.35 * glitches));
            try
            {
                return await _client.ChatAsync(req.Model, st.Messages, specs, options,
                    onContent: chunk => Emit(st, AgentEventKind.Text, chunk), ct: ct, toolMode: st.Mode);
            }
            catch (LlmException ex) when (ex.ToolsNotSupported && req.ToolMode == AgentToolMode.Auto && st.Mode == LlmToolMode.Native)
            {
                SwitchToStructured(st, "Ce modèle n'a pas d'appels d'outils natifs");
            }
            catch (LlmException ex) when (ex.GenerationGlitch && glitches < MaxGlitchRetries)
            {
                glitches++;
                Emit(st, AgentEventKind.Nudge, $"Le modèle s'est emballé (« {Cut(ex.Message, 90)} ») : nouvel essai ({glitches}/{MaxGlitchRetries}).");
            }
        }
    }

    private void SwitchToStructured(RunState st, string reason)
    {
        st.Mode = LlmToolMode.Structured;
        st.SwitchedMode = true;
        st.TextOnlyStreak = 0;
        Emit(st, AgentEventKind.Nudge, $"{reason} : passage en sortie contrainte (le modèle ne peut plus répondre que par un appel d'outil).");
        Audit(st, new { kind = "tool_mode", step = st.Step, mode = "structured", reason });
    }

    // ── Réponse en texte seul : fin légitime ou à relancer ? ────────────────

    private sealed record TextVerdict(string? Nudge = null, string? FailSummary = null, string? FailError = null);

    private TextVerdict JudgeTextOnly(RunState st, string content)
    {
        // 1. Copie d'un résultat d'outil (constaté sur un gros fichier lu) : ce n'est pas une réponse.
        if (LooksLikeToolEcho(content, st.Messages))
        {
            if (st.EchoNudges >= MaxEchoNudges)
                return new TextVerdict(FailSummary: "Le modèle recopie les résultats d'outils au lieu d'agir.",
                    FailError: "Réponse inexploitable : copie d'un résultat d'outil, même après relance.");
            st.EchoNudges++;
            var preview = OneLine(content.Replace("<tool_response>", string.Empty, StringComparison.OrdinalIgnoreCase)
                                         .Replace("</tool_response>", string.Empty, StringComparison.OrdinalIgnoreCase));
            st.Messages[^1].Content = $"(réponse écartée : copie d'un résultat d'outil — début : « {Cut(preview, 120)} »)";
            Emit(st, AgentEventKind.Nudge, "Réponse écartée (copie d'un résultat d'outil) : relance.");
            return new TextVerdict("Ta réponse recopie le contenu d'un fichier ou d'un résultat d'outil, ce qui ne sert à rien. " +
                                   "N'écris jamais le contenu d'un fichier : appelle un outil (edit_file, insert_lines, write_file) ou finish.");
        }

        // 2. Aucun outil appelé depuis le début.
        if (st.ToolCalls == 0 && st.Nudges == 0)
        {
            st.Nudges++;
            Emit(st, AgentEventKind.Nudge, "Aucun outil appelé : relance.");
            return new TextVerdict("Tu n'as appelé aucun outil. Si la tâche demande de lire, chercher ou modifier des fichiers, appelle l'outil MAINTENANT. " +
                                   "Si tu as déjà la réponse, appelle finish avec ta réponse.");
        }

        // 3. Le dernier appel d'outil a échoué et le modèle s'excuse au lieu de corriger son appel.
        if (st.LastResultWasError && st.ErrorNudges < MaxErrorNudges)
        {
            st.ErrorNudges++;
            Emit(st, AgentEventKind.Nudge, "Dernier appel en erreur : relance.");
            return new TextVerdict("Ton dernier appel d'outil a échoué. Lis le message d'erreur, corrige ton appel (chemin, texte exact…) et RÉESSAIE avec un outil. " +
                                   "Ne demande rien à l'utilisateur : lire, lister et chercher ne nécessitent aucune autorisation.");
        }

        // 4. Tâche d'écriture sans écriture / échec d'écriture / code non compilé ou qui ne compile pas.
        if (PreFinishNudge(st) is { } nudge)
            return new TextVerdict(nudge);

        // 5. Il annonce une action au lieu de la faire.
        if (st.AnnounceNudges < MaxAnnounceNudges && LooksLikeAnnouncement(content))
        {
            st.AnnounceNudges++;
            Emit(st, AgentEventKind.Nudge, "Action annoncée mais pas exécutée : relance.");
            return new TextVerdict("Tu annonces une action (ou tu demandes la permission) au lieu de la faire. N'écris pas ce que tu vas faire et ne demande pas " +
                                   "l'autorisation : l'utilisateur validera chaque modification lui-même. Appelle l'outil correspondant MAINTENANT " +
                                   "(une seule action à la fois). Si tout est terminé, appelle finish.");
        }

        return new TextVerdict();
    }

    private static readonly Regex Announcement = new(
        @"\b(?:je vais|nous allons|je dois|il faut que je|appelons|utilisons|voici la (?:commande|suite|methode|marche)|maintenant,? je|ensuite,? je|d'abord,? je|" +
        @"voulez-vous|veux-tu|est-ce que (?:tu|vous) (?:veux|voulez)|souhaitez-vous|souhaites-tu|" +
        @"let me|i will|i'll|i am going to|next,? i|now,? i(?:'ll| will)?|do you want|would you like|shall i)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Vrai si la réponse annonce ce que le modèle « va faire » (texte normalisé : minuscules, sans accents).</summary>
    internal static bool LooksLikeAnnouncement(string content)
        => !string.IsNullOrWhiteSpace(content) && Announcement.IsMatch(IntentHeuristics.Normalize(content));

    // ── Plusieurs appels dans un message ────────────────────────────────────

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/').TrimStart('.', '/');

    /// <summary>Motif d'abandon d'un appel du même message que des modifications dont le modèle n'a pas vu le résultat, ou null.</summary>
    private static string? SkipInThisMessage(AgentToolV2 tool, string pathKey, bool wroteInThisMessage, HashSet<string> touched)
    {
        if (!tool.IsMutating) return null;

        if (!tool.WritesFiles)
            return wroteInThisMessage
                ? "Ignoré : ne lance pas de commande dans le même message qu'une modification. Attends le résultat de la modification (elle peut être refusée), puis refais cet appel au pas suivant."
                : null;

        return pathKey.Length > 0 && touched.Contains(pathKey)
            ? "Ignoré : ce fichier vient d'être modifié dans ce même message, donc les numéros de ligne ont changé. Relis-le avec read_file puis refais cette modification au pas suivant."
            : null;
    }

    private static string OneLine(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

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

        // Exploration sans fin : le modèle cherche et relit sans jamais écrire (qwen3:8b, 25 appels de search_text d'affilée
        // sur une simple extraction de méthode). Les appels varient un peu, la garde anti-répétition ne les voit pas.
        if (tool.IsMutating) st.ExploreStreak = 0;
        else if (call.Name != AgentToolSetV2.FinishName)
        {
            st.ExploreStreak++;
            if (st.ExploreStreak >= ExploreWarnAt && st.ExploreStreak % 3 == 0 && st.Changes.Count == 0 && st.FileWriteAttempts == 0
                && IntentHeuristics.ExpectsFileChanges(st.Request.Goal))
                loopWarning += $"\n⚠ Tu explores depuis {st.ExploreStreak} pas sans rien modifier. Tu as assez d'informations : " +
                               "fais MAINTENANT la modification demandée (edit_file, insert_lines, replace_in_files ou write_file), en changeant le minimum de lignes.";
        }

        // finish : avant de laisser terminer, (1) relancer une tâche d'écriture restée sans aucune modification,
        // (2) ne pas laisser croire à une modification qui a échoué, (3) s'assurer une fois que le code modifié a été compilé.
        if (call.Name == AgentToolSetV2.FinishName)
        {
            if (PreFinishNudge(st) is { } nudge)
                return new CallOutcome(ToolResult.Ok(nudge));

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

        var changedFiles = applied.Changes is { Count: > 0 } many ? many
            : applied.Change is { } one ? new[] { one }
            : null;
        if (changedFiles is not null)
        {
            foreach (var changed in changedFiles)
                st.Changes[changed.RelativePath] = st.Changes.TryGetValue(changed.RelativePath, out var before)
                    ? new ChangedFile(changed.RelativePath, before.Added + changed.Added, before.Removed + changed.Removed, before.Created || changed.Created)
                    : changed;
            st.Verify = VerifyStatus.Unverified;
            st.RecentSignatures.Clear(); // le fichier a changé : relire ou refaire un appel n'est plus une « répétition »
        }
        else if (!applied.IsError && call.Name == "run_command" && LooksLikeVerification(ToolArgs.Str(call.Arguments, "command")))
        {
            // Une compilation qui répond « code de sortie 1 » n'est PAS une vérification réussie.
            if (applied.ExitCode is null or 0)
            {
                st.Verify = VerifyStatus.Passed;
                // Constat du banc d'essai : le travail est fini et vérifié, mais le modèle continue d'appeler des outils
                // jusqu'à la limite d'étapes. On lui dit que c'est le moment de conclure.
                if (st.Changes.Count > 0)
                    applied = applied with { Text = applied.Text + "\n✔ La compilation a réussi. Si la tâche demandée est faite, appelle finish maintenant avec un résumé court." };
            }
            else
            {
                st.Verify = VerifyStatus.Failed;
                st.HadBuildFailure = true;
                st.LastBuildErrors = string.Join("\n", applied.Text.Split('\n')
                    .Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(3).Select(l => Cut(l.Trim(), 220)));
            }
        }

        return applied;
    }

    private CallOutcome Complete(RunState st, LlmToolCall call, ToolResult result, string loopWarning)
    {
        var text = result.Text + loopWarning;
        st.LastResultWasError = result.IsError;

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
        return new CallOutcome(new ToolResult(result.IsError, text, result.Change, result.ExitCode, result.Changes));
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

    // ── Avant de terminer ───────────────────────────────────────────────────

    /// <summary>
    /// Le modèle veut s'arrêter (finish ou réponse en texte). Renvoie le rappel à lui faire d'abord, ou null s'il peut terminer.
    /// Chaque rappel est borné : la boucle ne peut pas s'éterniser.
    /// </summary>
    private string? PreFinishNudge(RunState st)
    {
        // (1) Consigne d'écriture, et le modèle n'a même pas essayé d'écrire : il a lu, ou annoncé un plan, puis s'est arrêté.
        if (st.Changes.Count == 0 && st.FileWriteAttempts == 0 && st.NoChangeNudges < MaxNoChangeNudges
            && IntentHeuristics.ExpectsFileChanges(st.Request.Goal))
        {
            st.NoChangeNudges++;
            Emit(st, AgentEventKind.Nudge, "Aucun fichier modifié alors que la tâche le demande : relance.");
            return "Tu n'as encore modifié AUCUN fichier, or la tâche demande de le faire. " +
                   "Appelle MAINTENANT l'outil d'écriture qui convient : edit_file pour remplacer un passage, replace_in_files pour renommer un nom partout, " +
                   "insert_lines pour ajouter des lignes, write_file pour créer un fichier neuf. Relis d'abord le fichier avec read_file si tu n'en as pas le texte exact. " +
                   "N'écris pas de plan : agis.";
        }

        // (2) Toutes les tentatives d'écriture ont échoué ou ont été refusées : ne pas laisser croire que c'est fait.
        if (st.Changes.Count == 0 && st.FileWriteAttempts > 0 && !st.HonestyNudged)
        {
            st.HonestyNudged = true;
            Emit(st, AgentEventKind.Nudge, "Aucun fichier modifié : rappel avant de terminer.");
            return $"Attention : AUCUN fichier n'a été modifié (tes tentatives ont échoué ou ont été refusées ; dernière erreur : « {Cut(st.LastWriteError ?? "?", 200)} »). " +
                   "Corrige ton appel et réessaie. Si tu ne peux vraiment pas, appelle finish en disant HONNÊTEMENT que rien n'a été modifié.";
        }

        // (3) Du code a été modifié : il doit avoir été compilé avec succès APRÈS la dernière modification.
        if (NeedsVerification(st))
        {
            st.VerifyNudges++;
            var command = SuggestVerifyCommand(st);
            if (st.Verify == VerifyStatus.Failed)
            {
                Emit(st, AgentEventKind.Nudge, "La compilation a échoué : correction demandée avant de terminer.");
                return "La dernière compilation a ÉCHOUÉ, le travail n'est pas terminé." +
                       (string.IsNullOrWhiteSpace(st.LastBuildErrors) ? string.Empty : $"\nErreurs :\n{st.LastBuildErrors}") +
                       "\nCorrige ces erreurs (edit_file, ou insert_lines) en changeant le minimum de lignes, " +
                       $"puis relance run_command avec « {command} ». N'écris pas de plan : appelle l'outil.";
            }

            Emit(st, AgentEventKind.Nudge, "Vérification demandée avant de terminer.");
            return $"Pas encore : tu as modifié du code ({string.Join(", ", st.Changes.Keys.Take(4))}) sans vérifier qu'il compile. " +
                   $"Lance run_command avec « {command} », corrige les erreurs s'il y en a, puis termine.";
        }

        return null;
    }

    /// <summary>La commande demandée par l'appelant, sinon la compilation du projet le plus proche d'un fichier modifié.</summary>
    private static string SuggestVerifyCommand(RunState st)
    {
        if (!string.IsNullOrWhiteSpace(st.VerifyCommand)) return st.VerifyCommand!;
        foreach (var path in st.Changes.Keys.Where(p => CodeExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)))
            if (WorkspaceOverview.NearestProject(st.Ctx.Root, path) is { } project)
                return WorkspaceOverview.BuildCommand(project);
        return "dotnet build";
    }

    /// <summary>Vrai si la réponse en texte recopie un résultat d'outil (balise <c>tool_response</c> ou début du dernier résultat).</summary>
    internal static bool LooksLikeToolEcho(string content, IReadOnlyList<LlmMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        if (content.Contains("<tool_response>", StringComparison.OrdinalIgnoreCase)
            || content.Contains("</tool_response>", StringComparison.OrdinalIgnoreCase))
            return true;

        // Copie du dernier résultat d'outil, sans balise : au moins 200 caractères identiques d'affilée.
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role != "tool") continue;
            var last = messages[i].Content.Trim();
            return last.Length >= 200 && content.Contains(last[..200], StringComparison.Ordinal);
        }
        return false;
    }

    // ── Vérification avant de terminer ──────────────────────────────────────

    /// <summary>
    /// Vrai si du code modifié n'a pas été compilé avec succès depuis. Une compilation jamais tentée n'est rappelée qu'UNE fois
    /// (sauf si une compilation a déjà échoué dans ce run) ; une compilation échouée est rappelée jusqu'à MaxVerifyNudges fois au total.
    /// </summary>
    private bool NeedsVerification(RunState st)
    {
        if (!st.Request.RequireVerification || st.Verify == VerifyStatus.Passed || st.VerifyNudges >= MaxVerifyNudges) return false;
        if (!st.Changes.Keys.Any(p => CodeExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))) return false;
        if (!_tools.Any(t => t.Name == "run_command")) return false;
        return st.Verify == VerifyStatus.Failed || st.VerifyNudges == 0 || st.HadBuildFailure;
    }

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
        sb.AppendLine("   Sur un long fichier, ne le lis pas en entier : repère la ligne avec search_text, puis read_file avec start_line et end_line autour.");
        sb.AppendLine("2. Pour REMPLACER du code : edit_file (old_text = le passage recopié EXACTEMENT depuis read_file, sans les numéros de ligne ni le « | » ; new_text = son remplacement). Pour AJOUTER du code sans rien remplacer : insert_lines (le texte est inséré AVANT le numéro de ligne donné). Change le MINIMUM de lignes ; ne réécris jamais un fichier entier pour en changer quelques lignes.");
        sb.AppendLine("3. Pour RENOMMER ou remplacer un nom partout (plusieurs fichiers) : replace_in_files, en UNE seule opération (old_text = ancien nom, new_text = nouveau nom), jamais plusieurs edit_file.");
        sb.AppendLine("4. write_file sert uniquement à créer un fichier NEUF.");
        sb.AppendLine("5. Après avoir modifié du code, vérifie avec run_command et corrige les erreurs signalées.");
        sb.AppendLine("6. Quand c'est terminé, appelle finish avec un résumé court en français.");
        sb.AppendLine();
        sb.AppendLine("RÈGLES");
        sb.AppendLine("- Chemins toujours RELATIFS au projet (Dossier/Fichier.cs), jamais absolus.");
        sb.AppendLine("- Lire, lister et chercher (read_file, list_dir, search_text) ne demandent AUCUNE autorisation : appelle-les directement, sans rien demander à l'utilisateur.");
        sb.AppendLine("- L'utilisateur voit chaque modification sous forme de diff et l'accepte ou la refuse ; si elle est refusée, propose autre chose au lieu de recommencer à l'identique.");
        sb.AppendLine("- Un seul appel à la fois pour modifier : attends le résultat d'une modification avant d'en faire une autre ou de compiler.");
        sb.AppendLine("- Ne pose JAMAIS de question du genre « voulez-vous que je… ? » : agis. L'utilisateur validera chaque modification lui-même.");
        sb.AppendLine("- Si un outil répond par une erreur, lis le message et corrige ton appel ; ne répète jamais l'appel qui vient d'échouer.");
        sb.AppendLine("- Ne dis JAMAIS qu'un fichier est modifié tant que l'outil n'a pas répondu « Fichier modifié » ou « Fichier créé ».");
        sb.AppendLine("- N'écris jamais de plan ni le contenu d'un fichier dans ta réponse : appelle les outils. Ne recopie pas les résultats des outils.");
        sb.AppendLine("- Réponds en français. Pas de longs discours : agis avec les outils.");
        return sb.ToString().TrimEnd();
    }

    internal static string BuildUserPrompt(AgentRunRequest req, WorkspaceInfo? overview = null, string? verifyCommand = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Objectif : " + req.Goal.Trim());
        if (!string.IsNullOrWhiteSpace(req.Context))
        {
            sb.AppendLine();
            sb.AppendLine(req.Context.Trim());
        }

        if (overview is not null && overview.Tree.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Contenu du projet (racine) :");
            sb.AppendLine(overview.Tree);
        }

        var verify = string.IsNullOrWhiteSpace(verifyCommand) ? req.VerifyCommand?.Trim() : verifyCommand.Trim();
        if (!string.IsNullOrWhiteSpace(verify))
        {
            sb.AppendLine();
            sb.AppendLine($"Pour vérifier que le code compile, lance run_command avec : {verify}");
        }
        else if (overview is { Projects.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"Projets .NET : {string.Join(", ", overview.Projects.Take(8))}.");
            sb.AppendLine("Pour vérifier que le code compile, lance run_command avec « dotnet build » suivi du chemin du plus petit projet .csproj qui contient tes modifications.");
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
            Warning = NoChangeWarning(st, outcome),
            RunId = st.RunId,
            ToolMode = st.SwitchedMode ? "native→structured" : st.Mode == LlmToolMode.Structured ? "structured" : "native",
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

    /// <summary>Le run se dit terminé mais rien n'a changé : à dire à l'utilisateur au lieu de laisser croire que c'est fait.</summary>
    private static string? NoChangeWarning(RunState st, AgentOutcome outcome)
    {
        if (outcome != AgentOutcome.Completed) return null;
        if (st.Changes.Count > 0)
            return st.Verify == VerifyStatus.Failed ? "La dernière compilation a échoué : le code modifié ne compile peut-être pas." : null;
        if (st.FileWriteAttempts > 0)
            return $"Aucune modification n'a été appliquée (dernière erreur : {Cut(st.LastWriteError ?? "refus", 160)}).";
        return IntentHeuristics.ExpectsFileChanges(st.Request.Goal)
            ? "L'agent s'est arrêté sans proposer aucune modification alors que la tâche semblait en demander une."
            : null;
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
        if (call.Name == ReplaceInFilesToolV2.ToolName
            && ToolArgs.Str(call.Arguments, "old_text") is { Length: > 0 } from)
            return $"{call.Name} {Cut(from, 40)} → {Cut(ToolArgs.Str(call.Arguments, "new_text") ?? string.Empty, 40)}";

        var key = ToolArgs.Str(call.Arguments, "path") ?? ToolArgs.Str(call.Arguments, "query") ?? ToolArgs.Str(call.Arguments, "command");
        return key is null ? call.Name : $"{call.Name} {Cut(key, 80)}";
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
