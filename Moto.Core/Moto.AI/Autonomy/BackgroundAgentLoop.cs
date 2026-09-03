// Moto.Core/AI/Autonomy/BackgroundAgentLoop.cs
// La boucle bornée perçoit→décide→agit→observe pour UN agent. Ne connaît rien
// de l'UI — narrate(string) et run.Steps sont les deux seuls points de contact
// avec l'extérieur. Le verrou de confirmation est appelé SANS CONDITION avant
// tout outil mutant (IAgentTool.IsMutating) : c'est la seule décision qui
// compte pour la sécurité de tout ce chantier (voir CLAUDE.md/mémoire
// "selfrepairagent-supervision-requirement" — même exigence).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moto.Core.AI.Internal;
using Moto.Core.Settings;

namespace Moto.Core.AI.Autonomy
{
    public sealed class BackgroundAgentLoop
    {
        private const int MaxConsecutiveDeclines = 3;

        private readonly MotoAiKernel _kernel;
        private readonly AiConfirmationService _confirmation;
        private readonly IReadOnlyList<IAgentTool> _tools;
        private readonly int _maxSteps;
        private readonly TimeSpan _maxDuration;

        public BackgroundAgentLoop(
            MotoAiKernel kernel,
            AiConfirmationService confirmation,
            IReadOnlyList<IAgentTool> tools,
            int maxSteps = 10,
            TimeSpan? maxDuration = null)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _maxSteps = maxSteps;
            _maxDuration = maxDuration ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Fait tourner le run jusqu'à Finish, refus, erreur, ou une des gardes
        /// (nombre de pas / durée / annulation). `narrate` est appelé pour CHAQUE
        /// pas — à l'appelant de le marshaler vers le thread UI si besoin (ce
        /// projet est portable, pas de dépendance MAUI ici).
        /// </summary>
        public async Task RunAsync(AgentRunRecord run, string workspaceRoot, Action<string> narrate)
        {
            var ct = run.Cts.Token;
            var history = new List<(AgentAction Action, string Observation)>();
            var consecutiveDeclines = 0;

            try
            {
                for (var step = 1; step <= _maxSteps; step++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        run.Status = AgentRunStatus.Cancelled;
                        narrate($"⏹ Agent « {run.AgentId} » arrêté.");
                        return;
                    }

                    if (DateTime.UtcNow - run.StartedUtc > _maxDuration)
                    {
                        run.Status = AgentRunStatus.StepLimitReached;
                        narrate($"⏱ Agent « {run.AgentId} » arrêté (durée maximale atteinte).");
                        return;
                    }

                    string raw;
                    try
                    {
                        raw = await _kernel.RouteAsync(BuildPrompt(run.Goal, history), ct);
                    }
                    catch (Exception ex)
                    {
                        run.Status = AgentRunStatus.Failed;
                        narrate($"❌ Agent « {run.AgentId} » : erreur IA — {ex.Message}");
                        return;
                    }

                    var action = AgentActionParser.Parse(raw);

                    if (action.Kind == AgentActionKind.Malformed)
                    {
                        run.Steps.Add(new AgentStepRecord
                        {
                            Index = step,
                            ActionKind = action.Kind,
                            Summary = "Réponse du modèle non reconnue."
                        });
                        narrate($"🤖 Étape {step} : réponse non reconnue, nouvelle tentative.");
                        history.Add((action, "Format non reconnu — réponds STRICTEMENT avec le format demandé, une seule action."));
                        continue;
                    }

                    var tool = _tools.FirstOrDefault(t => t.Handles == action.Kind);
                    if (tool is null)
                    {
                        history.Add((action, $"Aucun outil disponible pour {action.Kind}."));
                        continue;
                    }

                    var summary = string.IsNullOrWhiteSpace(action.Summary) ? DefaultSummary(action) : action.Summary!;
                    var record = new AgentStepRecord { Index = step, ActionKind = action.Kind, Summary = summary };
                    run.Steps.Add(record);

                    if (action.Kind == AgentActionKind.Finish)
                    {
                        narrate($"✅ Étape {step} : {summary}");
                        run.Status = AgentRunStatus.Completed;
                        return;
                    }

                    if (tool.IsMutating)
                    {
                        record.Confirmation = ConfirmationState.Pending;
                        narrate($"🤖 Étape {step} : propose — {summary}");

                        var confirmation = await _confirmation.RequestAsync(new ConfirmationRequest
                        {
                            // Réutilise deux valeurs de l'enum déjà présentes mais jamais
                            // utilisées ailleurs (ModifyCode/ExecuteCommand, vérifié —
                            // aucune autre référence dans tout le dépôt) plutôt que d'en
                            // ajouter de nouvelles.
                            Action = action.Kind == AgentActionKind.WriteFile
                                ? ConfirmationAction.ModifyCode
                                : ConfirmationAction.ExecuteCommand,
                            Title = $"🤖 Agent « {run.AgentId} »",
                            Message = summary,
                            // Construit UNIQUEMENT à partir des champs littéraux de
                            // l'action (voir IAgentTool.DescribeForConfirmation) — jamais
                            // à partir de ce que le modèle raconte de lui-même.
                            Details = tool.DescribeForConfirmation(action, workspaceRoot),
                            ConfirmText = "Autoriser",
                            CancelText = "Refuser",
                            IsDestructive = action.Kind == AgentActionKind.RunCommand
                        });

                        if (!confirmation.Confirmed)
                        {
                            record.Confirmation = ConfirmationState.Declined;
                            record.ObservationSummary = "Refusé par l'utilisateur.";
                            narrate($"🚫 Étape {step} : refusé.");
                            history.Add((action, "Action refusée par l'utilisateur — choisis une autre approche, ou termine (Finish) si tu ne peux pas continuer."));

                            consecutiveDeclines++;
                            if (consecutiveDeclines >= MaxConsecutiveDeclines)
                            {
                                run.Status = AgentRunStatus.Cancelled;
                                narrate($"⏹ Agent « {run.AgentId} » arrêté après {MaxConsecutiveDeclines} refus consécutifs.");
                                return;
                            }
                            continue;
                        }

                        record.Confirmation = ConfirmationState.Approved;
                        consecutiveDeclines = 0;
                    }

                    string observation;
                    try
                    {
                        observation = await tool.ExecuteAsync(action, workspaceRoot, ct);
                    }
                    catch (Exception ex)
                    {
                        observation = $"Erreur lors de l'exécution : {ex.Message}";
                    }

                    record.ObservationSummary = observation;
                    narrate($"🤖 Étape {step} : {observation}");
                    history.Add((action, observation));
                }

                run.Status = AgentRunStatus.StepLimitReached;
                narrate($"⏹ Agent « {run.AgentId} » arrêté (nombre maximal d'étapes atteint).");
            }
            catch (Exception ex)
            {
                run.Status = AgentRunStatus.Failed;
                narrate($"❌ Agent « {run.AgentId} » : erreur inattendue — {ex.Message}");
            }
        }

        private static string DefaultSummary(AgentAction action) => action.Kind switch
        {
            AgentActionKind.ReadFile => $"lire {action.Path}",
            AgentActionKind.WriteFile => $"écrire {action.Path}",
            AgentActionKind.RunCommand => $"exécuter : {action.Command}",
            AgentActionKind.Finish => "terminer",
            _ => "action inconnue"
        };

        private static string BuildPrompt(string goal, List<(AgentAction Action, string Observation)> history)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tu es un agent autonome de MOTO Editor. Objectif à accomplir, un pas à la fois :");
            sb.AppendLine(goal);
            sb.AppendLine();
            sb.AppendLine("À CHAQUE réponse, propose UNE SEULE action, dans EXACTEMENT ce format (rien d'autre autour) :");
            sb.AppendLine("ACTION: ReadFile | WriteFile | RunCommand | Finish");
            sb.AppendLine("PATH: chemin/relatif au projet (pour ReadFile et WriteFile uniquement)");
            sb.AppendLine("CONTENT: <<<");
            sb.AppendLine("contenu complet du fichier (pour WriteFile uniquement)");
            sb.AppendLine(">>>");
            sb.AppendLine("COMMAND: commande shell (pour RunCommand uniquement)");
            sb.AppendLine("SUMMARY: courte explication en français, pour un humain qui doit valider");

            if (history.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Étapes précédentes de CE run :");
                foreach (var (action, observation) in history)
                    sb.AppendLine($"- {DefaultSummary(action)} → {observation}");
            }

            sb.AppendLine();
            sb.AppendLine("Dès que l'objectif est atteint, réponds avec ACTION: Finish et un SUMMARY récapitulatif.");
            return sb.ToString();
        }
    }
}
