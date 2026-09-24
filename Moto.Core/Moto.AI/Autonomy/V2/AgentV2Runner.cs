// Moto.Core/Moto.AI/Autonomy/V2/AgentV2Runner.cs
// ★ AJOUT (24/09, "écriture agentique fonctionnelle") : branche l'agent v2 (AgentLoopV2) sur ce que l'utilisateur voit déjà —
// le panneau « Agents en cours » (AgentRunRecord / AgentStepRecord) et les messages du chat (narrate).
//
// Choix de conception :
//  - la boucle tourne SUR UN THREAD D'ARRIÈRE-PLAN (Task.Run) : elle attend le modèle et l'humain ; l'interface ne doit jamais
//    être bloquée par un modèle qui charge 13 s en VRAM. La v1 tournait sur le thread UI (voir BackgroundAgentService).
//  - tout ce qui touche l'interface (étapes du panneau, statut, narrate, rechargement des onglets) est REPOSTÉ sur le thread UI,
//    dans l'ordre d'émission des événements : AgentRunRecord et ObservableCollection ne supportent pas d'écritures multi-threads.
//  - aucune dépendance MAUI : le repostage utilise le SynchronizationContext de l'appelant (l'éditeur peut injecter le sien).
//  - AgentGlobalBudget (plafond de 200 appels IA de la v1) n'est PAS utilisé : il compte des appels d'une boucle qui n'en fait
//    qu'un par pas ; la v2 a ses propres bornes (étapes, durée, refus consécutifs, boucle détectée, contexte saturé).
using System.Text;
using System.Text.RegularExpressions;
using Moto.Core.AI.Llm;
using Moto.Editor.Services; // TerminalCommandResult (namespace historique)

namespace Moto.Core.AI.Autonomy.V2;

public sealed class AgentV2Runner
{
    /// <summary>Comment défaire un run, tel que dit à l'utilisateur à la fin (à garder aligné avec le panneau « Agents en cours »).</summary>
    public const string UndoHint = "↩ Pour tout défaire : Ctrl+Maj+P → « Agents en cours » → « Annuler les modifications ».";

    private const int MaxObservationChars = 220;
    private const int MaxThoughtChars = 300;
    private static readonly TimeSpan BackupRetention = TimeSpan.FromDays(14);

    private static readonly HashSet<string> WritingTools = new(StringComparer.Ordinal)
    {
        "edit_file", "insert_lines", ReplaceInFilesToolV2.ToolName, "write_file",
    };

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ExitCodeLine = new(@"Code de sortie : (-?\d+)", RegexOptions.Compiled);
    private static readonly Regex ErrorCountLine = new(@"(\d+) ligne\(s\) d'erreur", RegexOptions.Compiled);

    private readonly IAgentApprover _approver;
    private readonly Func<string, string, CancellationToken, Task<TerminalCommandResult>>? _runCommand;
    private readonly Func<AgentV2Settings> _settings;
    private readonly Func<string, OllamaChatClient> _clientFactory;
    private readonly Action<Action>? _post;
    private readonly string? _backupFolder;
    private readonly bool _writeAuditLog;

    /// <summary>
    /// Appelé SUR LE THREAD UI avec les chemins complets des fichiers que l'agent vient d'écrire (ou de restaurer, après une
    /// annulation) : l'éditeur y recharge ses onglets ouverts — sans quoi un onglet garderait l'ancien texte et son
    /// enregistrement écraserait le travail de l'agent.
    /// </summary>
    public Action<IReadOnlyList<string>>? FilesChanged { get; set; }

    /// <param name="approver">Qui autorise chaque écriture / commande (l'éditeur branche sa boîte de confirmation).</param>
    /// <param name="runCommand">Exécute « dotnet build… » (défaut : TerminalService).</param>
    /// <param name="settings">Lit les réglages à chaque run (défaut : catalogue « IA Locale »).</param>
    /// <param name="clientFactory">Crée le client Ollama pour un endpoint (tests : faux serveur).</param>
    /// <param name="post">Reposte une action sur le thread UI (défaut : SynchronizationContext de l'appelant de RunAsync, sinon en ligne).</param>
    /// <param name="backupFolder">Dossier des sauvegardes d'originaux (défaut : %LOCALAPPDATA%\MotoEditor\AgentBackups).</param>
    public AgentV2Runner(
        IAgentApprover approver,
        Func<string, string, CancellationToken, Task<TerminalCommandResult>>? runCommand = null,
        Func<AgentV2Settings>? settings = null,
        Func<string, OllamaChatClient>? clientFactory = null,
        Action<Action>? post = null,
        string? backupFolder = null,
        bool writeAuditLog = true)
    {
        _approver = approver ?? throw new ArgumentNullException(nameof(approver));
        _runCommand = runCommand;
        _settings = settings ?? (() => AgentV2Settings.Load());
        _clientFactory = clientFactory ?? (endpoint => new OllamaChatClient(endpoint));
        _post = post;
        _backupFolder = backupFolder;
        _writeAuditLog = writeAuditLog;
    }

    /// <summary>Vrai si le réglage « agent_engine » vaut « v2 » (défaut).</summary>
    public bool IsEnabled => _settings().UseV2;

    /// <summary>Prévient l'éditeur que ces fichiers ont changé sur le disque (annulation d'un run…). À appeler sur le thread UI.</summary>
    public void NotifyFilesChanged(IReadOnlyList<string> fullPaths)
    {
        if (fullPaths.Count == 0) return;
        try { FilesChanged?.Invoke(fullPaths); }
        catch (Exception) { /* un onglet impossible à recharger ne doit pas casser l'appelant */ }
    }

    /// <summary>
    /// Lance le run et rend la main tout de suite à l'appelant ; la tâche retournée se termine quand le run a atteint son état
    /// final dans <paramref name="run"/> (statut, résultat, message de fin). À appeler depuis le thread UI.
    /// </summary>
    /// <param name="context">Contexte joint à l'objectif (fichier ouvert dans l'éditeur…).</param>
    public Task RunAsync(AgentRunRecord run, string goal, string workspaceRoot, Action<string> narrate, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(narrate);

        var ui = SynchronizationContext.Current;
        Action<Action> post = _post ?? (action =>
        {
            if (ui is null) action();
            else ui.Post(_ => action(), null);
        });

        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(() => ExecuteAsync(run, goal, workspaceRoot, narrate, context, SafePost(post), finished));
        return finished.Task;
    }

    // ── Exécution (thread d'arrière-plan) ───────────────────────────────────

    private async Task ExecuteAsync(AgentRunRecord run, string goal, string workspaceRoot, Action<string> narrate, string? context,
        Action<Action> post, TaskCompletionSource<bool> finished)
    {
        var model = string.Empty;
        Action completion;
        var root = SafeFullPath(workspaceRoot);
        var view = new RunView(run, narrate, root, () => FilesChanged);

        try
        {
            var settings = _settings();
            var ct = run.Cts.Token;

            using var client = _clientFactory(settings.Endpoint);
            model = await ResolveModelAsync(client, settings, ct).ConfigureAwait(false);

            post(() => view.Started(model, settings));
            RunBackup.Prune(_backupFolder, BackupRetention);

            var request = new AgentRunRequest
            {
                Model = model,
                Goal = goal,
                WorkspaceRoot = workspaceRoot,
                AgentId = run.AgentId,
                ToolMode = settings.ToolMode,
                MaxSteps = settings.MaxSteps,
                MaxDuration = TimeSpan.FromMinutes(settings.MaxMinutes),
                Options = new LlmOptions { NumCtx = settings.NumCtx, StructuredThought = settings.Thought },
                Context = context,
                VerifyCommand = settings.VerifyCommand,
                WriteAuditLog = _writeAuditLog,
                RunId = run.Id.ToString("N")[..10],
                BackupFolder = _backupFolder,
            };

            var loop = new AgentLoopV2(client, new CancellationAwareApprover(_approver), tools: null, _runCommand);
            var showThought = settings.Thought;
            var result = await loop.RunAsync(request, ev => post(() => view.Handle(ev, showThought)), ct).ConfigureAwait(false);

            var finalModel = model;
            completion = () => view.Complete(result, finalModel);
        }
        catch (OperationCanceledException)
        {
            var finalModel = model;
            completion = () => view.Cancelled(finalModel);
        }
        catch (LlmException ex)
        {
            var finalModel = model;
            var message = ex.Message;
            completion = () => view.Failed(message, finalModel);
        }
        catch (Exception ex)
        {
            var finalModel = model;
            var message = $"Erreur inattendue : {ex.Message}";
            completion = () => view.Failed(message, finalModel);
        }

        post(() =>
        {
            try { completion(); }
            finally { finished.TrySetResult(true); }
        });
    }

    /// <summary>
    /// Le modèle du run. Un modèle imposé par « agent_model » doit être installé ; sinon le premier des modèles recommandés qui l'est.
    /// Toute impasse (Ollama éteint, modèle absent) est dite en clair, avant de démarrer la boucle.
    /// </summary>
    private static async Task<string> ResolveModelAsync(OllamaChatClient client, AgentV2Settings settings, CancellationToken ct)
    {
        var installed = await client.ListModelsAsync(ct).ConfigureAwait(false);
        if (installed.Count == 0)
            throw new LlmException("Ollama est lancé mais n'a aucun modèle installé. Installe-en un, par exemple : ollama pull " + AgentV2Settings.PreferredModels[0]);

        var model = AgentV2Settings.ChooseModel(settings.Model, settings.FallbackModel, installed);
        if (AgentV2Settings.IsInstalled(model, installed)) return model;

        var list = string.Join(", ", installed.Take(8)) + (installed.Count > 8 ? "…" : string.Empty);
        throw new LlmException(string.IsNullOrWhiteSpace(settings.Model)
            ? $"Aucun modèle recommandé n'est installé ({string.Join(", ", AgentV2Settings.PreferredModels)}). Installés : {list}. Installe l'un des deux, ou renseigne le réglage « agent_model »."
            : $"Le modèle « {model} » (réglage « agent_model ») n'est pas installé dans Ollama. Installés : {list}. Installe-le avec « ollama pull {model} » ou vide ce réglage pour un choix automatique.");
    }

    private static Action<Action> SafePost(Action<Action> post) => action => post(() =>
    {
        try { action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AgentV2Runner] Mise à jour de l'interface en échec : {ex.Message}"); }
    });

    private static string SafeFullPath(string workspaceRoot)
    {
        try { return Path.GetFullPath(AgentPathResolver.EffectiveRoot(workspaceRoot)); }
        catch (Exception) { return workspaceRoot; }
    }

    /// <summary>Si l'utilisateur arrête l'agent pendant qu'une confirmation est ouverte, rien n'est écrit même s'il a cliqué « Autoriser ».</summary>
    private sealed class CancellationAwareApprover : IAgentApprover
    {
        private readonly IAgentApprover _inner;

        public CancellationAwareApprover(IAgentApprover inner) => _inner = inner;

        public async Task<bool> ApproveAsync(ApprovalRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var approved = await _inner.ApproveAsync(request, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return approved;
        }
    }

    // ── Côté interface (thread UI uniquement) ───────────────────────────────

    /// <summary>L'état du run tel que l'interface le montre. Toutes ses méthodes s'exécutent sur le thread UI, dans l'ordre des événements.</summary>
    private sealed class RunView
    {
        private readonly AgentRunRecord _run;
        private readonly Action<string> _narrate;
        private readonly string _root;
        private readonly Func<Action<IReadOnlyList<string>>?> _filesChanged;
        private readonly StringBuilder _thought = new();
        private AgentStepRecord? _current;

        public RunView(AgentRunRecord run, Action<string> narrate, string root, Func<Action<IReadOnlyList<string>>?> filesChanged)
        {
            _run = run;
            _narrate = narrate;
            _root = root;
            _filesChanged = filesChanged;
        }

        public void Started(string model, AgentV2Settings settings)
        {
            _run.Engine = "v2";
            _run.SetModel(model);
            _run.SetActivity("Chargement du modèle en mémoire…");
            _narrate($"🤖 Agent « {_run.AgentId} » démarre avec {model} ({ToolModeLabel(settings.ToolMode.ToString().ToLowerInvariant())}). "
                     + "Le premier pas peut prendre une dizaine de secondes : le modèle se charge en mémoire.");
        }

        public void Handle(AgentEvent ev, bool showThought)
        {
            switch (ev.Kind)
            {
                case AgentEventKind.Text:
                    if (showThought) _thought.Append(ev.Text);
                    break;

                case AgentEventKind.ToolCalled:
                    FlushThought();
                    _current = NewStep(ev.Tool, ev.Text);
                    break;

                case AgentEventKind.ToolProposed:
                {
                    var step = _current ?? (_current = NewStep(ev.Tool, ev.Text));
                    if (!string.IsNullOrWhiteSpace(ev.Text)) step.Summary = Capitalize(ev.Text);
                    _run.SetActivity($"Étape {step.Index} · {step.Summary}");
                    step.Confirmation = ConfirmationState.Pending;
                    _run.Status = AgentRunStatus.AwaitingConfirmation;
                    _narrate($"🤖 Étape {step.Index} : propose — {ev.Text}");
                    break;
                }

                case AgentEventKind.ToolApproved:
                    if (_current is not null) _current.Confirmation = ConfirmationState.Approved;
                    _run.Status = AgentRunStatus.Running;
                    break;

                case AgentEventKind.ToolDeclined:
                {
                    var step = _current ?? (_current = NewStep(ev.Tool, ev.Text));
                    step.Confirmation = ConfirmationState.Declined;
                    step.ObservationSummary = "Refusé par l'utilisateur.";
                    _run.Status = AgentRunStatus.Running;
                    _narrate($"🚫 Étape {step.Index} : refusé.");
                    break;
                }

                case AgentEventKind.FilesChanged:
                    if (ev.Files is { Count: > 0 } files)
                        Notify(files.Select(f => FullPath(f.RelativePath)).ToList());
                    break;

                case AgentEventKind.ToolResult:
                    HandleResult(ev);
                    break;

                case AgentEventKind.Nudge:
                    // Une relance interne (« Aucun outil appelé »…) ne se dit pas dans le chat ; seul un « finish » repoussé
                    // reste visible dans le panneau, sinon son pas resterait sans résultat.
                    if (_current is { ActionKind: AgentActionKind.Finish } finishStep)
                    {
                        finishStep.ObservationSummary = "Pas encore : " + OneLine(ev.Text, MaxObservationChars);
                        _current = null;
                    }
                    break;

                case AgentEventKind.Finished:
                    _thought.Clear();
                    if (_current is { ActionKind: AgentActionKind.Finish } last)
                        last.ObservationSummary = OneLine(ev.Text, MaxObservationChars);
                    _current = null;
                    break;
            }
        }

        private void HandleResult(AgentEvent ev)
        {
            // Un appel écarté sans être exécuté (deux modifications du même fichier dans un message) n'a pas d'événement
            // « ToolCalled » : son résultat crée son propre pas.
            var step = _current ?? NewStep(ev.Tool, ev.Tool ?? string.Empty);
            _current = null;
            _run.SetActivity("Le modèle réfléchit…");

            if (step.Confirmation == ConfirmationState.Declined) return; // le texte destiné au modèle ne doit pas écraser « Refusé »

            var text = ev.Tool == "run_command" && !ev.IsError ? DescribeCommandResult(ev.Text) : OneLine(ev.Text, MaxObservationChars);
            step.ObservationSummary = text;

            if (ev.Tool is null || !IsMutating(ev.Tool)) return;
            var failed = ev.IsError || (ev.Tool == "run_command" && text.StartsWith("Commande en échec", StringComparison.Ordinal));
            _narrate($"{(failed ? "⚠" : "✅")} Étape {step.Index} : {text}");
        }

        public void Complete(AgentRunResult result, string model)
        {
            FlushThought();
            _run.SetResult(result, model);
            _run.Status = result.Outcome switch
            {
                AgentOutcome.Completed => AgentRunStatus.Completed,
                AgentOutcome.Cancelled or AgentOutcome.Declined => AgentRunStatus.Cancelled,
                AgentOutcome.StepLimit or AgentOutcome.TimeLimit => AgentRunStatus.StepLimitReached,
                _ => AgentRunStatus.Failed,
            };

            _narrate(FinalMessage(_run, result, model));

            // Filet de sécurité : tout ce que ce run a changé est rechargé dans l'éditeur, même si un événement s'est perdu.
            if (result.Changes.Count > 0)
                Notify(result.Changes.Select(c => FullPath(c.RelativePath)).ToList());
        }

        public void Cancelled(string model)
        {
            FlushThought();
            _run.SetFailure("Arrêté par l'utilisateur.", model);
            _run.Status = AgentRunStatus.Cancelled;
            _narrate($"⏹ Agent « {_run.AgentId} » arrêté.");
        }

        public void Failed(string message, string model)
        {
            FlushThought();
            _run.SetFailure(message, model);
            _run.Status = AgentRunStatus.Failed;
            _narrate($"❌ Agent « {_run.AgentId} » : {message}");
        }

        private AgentStepRecord NewStep(string? tool, string summary)
        {
            var step = new AgentStepRecord
            {
                Index = _run.Steps.Count + 1,
                ActionKind = KindFor(tool),
                Summary = summary,
            };
            _run.Steps.Add(step);
            _run.SetActivity($"Étape {step.Index} · {summary}");
            return step;
        }

        private void FlushThought()
        {
            if (_thought.Length == 0) return;
            var text = OneLine(_thought.ToString(), MaxThoughtChars);
            _thought.Clear();
            if (text.Length > 0) _narrate($"💭 {text}");
        }

        private string FullPath(string relative)
        {
            try { return Path.GetFullPath(Path.Combine(_root, relative)); }
            catch (Exception) { return Path.Combine(_root, relative); }
        }

        private void Notify(IReadOnlyList<string> fullPaths)
        {
            try { _filesChanged()?.Invoke(fullPaths); }
            catch (Exception) { /* onglet impossible à recharger : le fichier reste correct sur le disque */ }
        }
    }

    // ── Textes et correspondances ───────────────────────────────────────────

    private static bool IsMutating(string tool) => WritingTools.Contains(tool) || tool == "run_command";

    private static AgentActionKind KindFor(string? tool) => tool switch
    {
        "read_file" or "list_dir" or "search_text" => AgentActionKind.ReadFile,
        "edit_file" or "insert_lines" or "write_file" => AgentActionKind.WriteFile,
        ReplaceInFilesToolV2.ToolName => AgentActionKind.WriteFile,
        "run_command" => AgentActionKind.RunCommand,
        "finish" => AgentActionKind.Finish,
        _ => AgentActionKind.Malformed,
    };

    private static string OneLine(string text, int max)
    {
        var flat = WhitespaceRun.Replace(text.Trim(), " ");
        return flat.Length <= max ? flat : flat[..max] + "…";
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>« Commande : … / Code de sortie : 1 / 3 ligne(s) d'erreur : … » → une phrase.</summary>
    internal static string DescribeCommandResult(string text)
    {
        var exit = ExitCodeLine.Match(text);
        if (!exit.Success) return OneLine(text, MaxObservationChars);
        if (exit.Groups[1].Value == "0") return "Commande réussie (code 0).";

        var errors = ErrorCountLine.Match(text);
        return $"Commande en échec (code {exit.Groups[1].Value})" + (errors.Success ? $" — {errors.Groups[1].Value} ligne(s) d'erreur." : ".");
    }

    private static string ToolModeLabel(string mode) => mode switch
    {
        "structured" => "sortie contrainte",
        "native" => "appels d'outils natifs",
        "native→structured" => "appels natifs, puis sortie contrainte",
        "auto" => "mode automatique",
        _ => mode,
    };

    private static string Duration(TimeSpan elapsed)
        => elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:D2} s" : $"{Math.Max(1, (int)Math.Round(elapsed.TotalSeconds))} s";

    /// <summary>Le compte rendu de fin de run, en français simple : ce qui a changé, ce qui a coincé, comment défaire.</summary>
    internal static string FinalMessage(AgentRunRecord run, AgentRunResult result, string model)
    {
        var lines = new List<string>
        {
            result.Outcome switch
            {
                AgentOutcome.Completed => $"✅ Agent « {run.AgentId} » terminé.",
                AgentOutcome.Cancelled => $"⏹ Agent « {run.AgentId} » arrêté.",
                AgentOutcome.Declined => $"⏹ Agent « {run.AgentId} » arrêté : trois refus d'affilée.",
                AgentOutcome.StepLimit => $"⏱ Agent « {run.AgentId} » arrêté : nombre maximal d'étapes atteint.",
                AgentOutcome.TimeLimit => $"⏱ Agent « {run.AgentId} » arrêté : durée maximale atteinte.",
                AgentOutcome.LoopDetected => $"❌ Agent « {run.AgentId} » arrêté : le modèle tourne en rond.",
                _ => $"❌ Agent « {run.AgentId} » : échec.",
            },
        };

        if (result.Changes.Count > 0)
        {
            lines.Add($"{result.Changes.Count} fichier(s) modifié(s) :");
            foreach (var c in result.Changes.Take(12))
                lines.Add(c.Created ? $"  • {c.RelativePath} (créé, +{c.Added})" : $"  • {c.RelativePath} (+{c.Added} −{c.Removed})");
            if (result.Changes.Count > 12) lines.Add($"  • … et {result.Changes.Count - 12} autre(s)");
        }
        else
        {
            lines.Add("Aucun fichier modifié.");
        }

        // Le résumé du modèle n'a de sens que s'il a terminé de lui-même ; sinon c'est la raison de l'arrêt.
        if (result.Outcome is AgentOutcome.Completed or AgentOutcome.Failed or AgentOutcome.LoopDetected
            && !string.IsNullOrWhiteSpace(result.Summary))
            lines.Add(result.Summary.Trim());
        if (!string.IsNullOrWhiteSpace(result.Error) && !string.Equals(result.Error.Trim(), result.Summary.Trim(), StringComparison.Ordinal))
            lines.Add("Détail : " + result.Error.Trim());
        if (!string.IsNullOrWhiteSpace(result.Warning))
            lines.Add("⚠ " + result.Warning.Trim());
        if (result.Changes.Count > 0)
            lines.Add(UndoHint);

        var mode = ToolModeLabel(result.ToolMode);
        lines.Add($"({model} · {mode} · {result.ToolCalls} appel(s) d'outils · {Duration(result.Elapsed)})");
        return string.Join("\n", lines);
    }
}
