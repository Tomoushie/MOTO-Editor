// Moto.Core/AI/Autonomy/BackgroundAgentLoop.cs
// La boucle bornée perçoit→décide→agit→observe pour UN agent. Ne connaît rien
// de l'UI — narrate(string) et run.Steps sont les deux seuls points de contact
// avec l'extérieur. Le verrou de confirmation est appelé SANS CONDITION avant
// tout outil mutant (IAgentTool.IsMutating) : c'est la seule décision qui
// compte pour la sécurité de tout ce chantier (voir CLAUDE.md/mémoire
// "selfrepairagent-supervision-requirement" — même exigence).
//
// ★ JALON 2 (03/09) : deux ajouts par rapport au jalon 1 — (1) SendMessage,
// une action non-mutante de plus, plafonnée séparément du budget de pas pour
// qu'un agent ne puisse pas passer tout son run à "discuter" ; (2) chaque
// action mutante est publiée sur AgentMessageBus AVANT la confirmation (pas
// seulement après exécution), pour qu'un 2e agent — ou la boîte de dialogue de
// confirmation elle-même — puisse voir "quelqu'un d'autre a déjà touché ce
// fichier il y a N secondes" au lieu de compter sur la chance du minutage.
// AgentAuditLog écrit directement ici (pas dans un outil) pour qu'un outil
// bogué ne puisse jamais faire sauter la trace.
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
        private const int MaxMessagesPerRun = 5;

        private readonly MotoAiKernel _kernel;
        private readonly AiConfirmationService _confirmation;
        private readonly IReadOnlyList<IAgentTool> _tools;
        private readonly AgentMessageBus _messageBus;
        private readonly AgentGlobalBudget _globalBudget;
        private readonly int _maxSteps;
        private readonly TimeSpan _maxDuration;

        public BackgroundAgentLoop(
            MotoAiKernel kernel,
            AiConfirmationService confirmation,
            IReadOnlyList<IAgentTool> tools,
            AgentMessageBus messageBus,
            AgentGlobalBudget globalBudget,
            int maxSteps = 10,
            TimeSpan? maxDuration = null)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _globalBudget = globalBudget ?? throw new ArgumentNullException(nameof(globalBudget));
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
            var auditLog = new AgentAuditLog(workspaceRoot);
            var consecutiveDeclines = 0;
            var sentMessages = 0;
            var lastMessageCheckUtc = run.StartedUtc;
            // ★ AJOUT (03/09, trouvé en testant avec Tom) : filet DÉTERMINISTE,
            // indépendant de la discipline du modèle — un petit modèle local ne
            // reconnaît pas toujours qu'un objectif du type "ajoute une ligne" est
            // déjà atteint après une écriture réussie, et continue de proposer
            // EXACTEMENT la même écriture. Comparé par chemin seul (pas contenu) :
            // suffisant pour ce cas réel, une vraie tâche multi-écritures légitime
            // sur le même fichier reste rare pour ce jalon.
            (AgentActionKind Kind, string? Path)? lastSuccessfulMutation = null;
            var repeatCount = 0;

            // ★ AJOUT (05/09, cause réelle trouvée grâce au journal "non_mutating_step"
            // ajouté juste au-dessus dans ce fichier) : le filet ci-dessus ne couvre
            // QUE les écritures répétées — un petit modèle local qui relit le MÊME
            // fichier en boucle (ReadFile, ReadFile, ReadFile...) sans jamais passer à
            // WriteFile n'était rattrapé par RIEN, et épuisait tout son budget de pas
            // sans jamais rien accomplir (observé sur /refactor, y compris sur un
            // fichier de 14 lignes — donc pas une question de taille de fichier).
            // Même patron que ci-dessus, appliqué aux actions NON mutantes.
            (AgentActionKind Kind, string? Path)? lastNonMutatingAction = null;
            var nonMutatingRepeatCount = 0;

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

                    // ★ AJOUT (jalon 3) : plafond partagé entre TOUS les agents de la
                    // session — un run individuellement raisonnable peut quand même
                    // contribuer à une consommation IA sans fin si plusieurs agents
                    // se succèdent sur une longue session.
                    if (!_globalBudget.TryConsume())
                    {
                        run.Status = AgentRunStatus.StepLimitReached;
                        narrate($"⏱ Agent « {run.AgentId} » arrêté — plafond global d'appels IA atteint pour cette session ({_globalBudget.Limit} au total, protège contre une consommation illimitée si plusieurs agents s'enchaînent).");
                        return;
                    }

                    // ★ AJOUT (jalon 2) : messages reçus d'AUTRES agents depuis le
                    // dernier tour — repliés dans le prompt pour que cet agent puisse
                    // réagir à une coordination initiée ailleurs.
                    var incoming = _messageBus.NotesSince(run.AgentId, lastMessageCheckUtc);
                    lastMessageCheckUtc = DateTime.UtcNow;

                    string raw;
                    try
                    {
                        raw = await _kernel.RouteAsync(BuildPrompt(run.Goal, history, incoming), ct);
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
                        // ★ AJOUT (05/09, diagnostic demandé en creusant pourquoi /refactor
                        // n'aboutit jamais même sur un petit fichier) : jusqu'ici, un pas
                        // Malformed ne laissait AUCUNE trace de ce que le modèle a RÉELLEMENT
                        // répondu — juste "non reconnu". Le journal existe déjà pour les
                        // propositions/décisions ; on y ajoute ce cas précis (texte tronqué,
                        // ce journal reste un fichier texte lisible par n'importe qui y ayant
                        // accès, pas la peine d'y mettre plus que nécessaire).
                        auditLog.Append(new
                        {
                            ts = DateTime.UtcNow,
                            agentId = run.AgentId,
                            kind = "malformed",
                            step,
                            rawOutput = raw.Length > 1500 ? raw.Substring(0, 1500) + "…(tronqué)" : raw
                        });
                        history.Add((action, "Format non reconnu — réponds STRICTEMENT avec le format demandé, une seule action."));
                        continue;
                    }

                    // ★ AJOUT (jalon 2) : plafond séparé du budget de pas principal —
                    // un agent qui ne ferait qu'échanger des messages ne doit jamais
                    // pouvoir consommer tout son run sans jamais agir sur de vrais
                    // fichiers/commandes.
                    if (action.Kind == AgentActionKind.SendMessage && sentMessages >= MaxMessagesPerRun)
                    {
                        history.Add((action, $"Limite de {MaxMessagesPerRun} messages atteinte pour ce run — choisis une autre action, ou termine (Finish)."));
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

                    // ★ AJOUT (jalon 3, durcissement) : confinement — refusé AVANT
                    // même de proposer une confirmation, pour ReadFile ET WriteFile
                    // (les deux seules actions avec un Path). Un chemin qui s'évade
                    // du dossier du projet ne doit jamais arriver jusqu'à l'humain.
                    if (!string.IsNullOrWhiteSpace(action.Path))
                    {
                        var resolvedPath = AgentPathResolver.Resolve(workspaceRoot, action.Path!);
                        if (!AgentPathResolver.IsWithinRoot(workspaceRoot, resolvedPath))
                        {
                            record.Confirmation = ConfirmationState.Declined;
                            record.ObservationSummary = "🛡 Refusé : chemin hors du dossier du projet.";
                            narrate($"🛡 Étape {step} : refusé — « {action.Path} » sort du dossier du projet.");
                            auditLog.Append(new
                            {
                                ts = DateTime.UtcNow,
                                agentId = run.AgentId,
                                kind = "security_violation",
                                action = action.Kind.ToString(),
                                path = action.Path,
                                resolvedPath
                            });
                            history.Add((action, $"Refusé : « {action.Path} » sort du dossier du projet — reste À L'INTÉRIEUR du dossier du projet, ou termine (Finish) si ce n'est pas possible."));

                            consecutiveDeclines++;
                            if (consecutiveDeclines >= MaxConsecutiveDeclines)
                            {
                                run.Status = AgentRunStatus.Cancelled;
                                narrate($"⏹ Agent « {run.AgentId} » arrêté après {MaxConsecutiveDeclines} refus consécutifs.");
                                return;
                            }
                            continue;
                        }
                    }

                    if (action.Kind == AgentActionKind.Finish)
                    {
                        narrate($"✅ Étape {step} : {summary}");
                        run.Status = AgentRunStatus.Completed;
                        return;
                    }

                    if (tool.IsMutating)
                    {
                        // ★ AJOUT (jalon 2) : détection de conflit AVANT de publier
                        // notre propre proposition (sinon on se retrouverait toujours
                        // soi-même) — seul WriteFile a un chemin unique et pertinent ;
                        // RunCommand ne mappe pas proprement sur un seul fichier.
                        string? touchedPath = action.Kind == AgentActionKind.WriteFile
                            ? AgentPathResolver.Resolve(workspaceRoot, action.Path ?? string.Empty)
                            : null;

                        string conflictNote = string.Empty;
                        if (touchedPath != null)
                        {
                            var recentTouch = _messageBus.FindRecentTouch(run.AgentId, touchedPath, DateTime.UtcNow);
                            if (recentTouch != null)
                            {
                                var elapsed = DateTime.UtcNow - recentTouch.SentUtc;
                                conflictNote = $"\n\n⚠ {recentTouch.FromAgentId} a touché ce même fichier il y a {elapsed.TotalSeconds:F0}s.";
                            }
                        }

                        // ★ AJOUT (jalon 3, durcissement) : indice visuel seulement,
                        // ne bloque jamais — la décision reste entièrement humaine.
                        var dangerHint = action.Kind == AgentActionKind.RunCommand
                            ? DangerousCommandHint(action.Command)
                            : null;
                        if (dangerHint != null) conflictNote += $"\n\n{dangerHint}";

                        _messageBus.Post(new AgentMessage
                        {
                            FromAgentId = run.AgentId,
                            Kind = AgentMessageKind.Proposal,
                            Text = summary,
                            TouchedPath = touchedPath
                        });

                        auditLog.Append(new
                        {
                            ts = DateTime.UtcNow,
                            agentId = run.AgentId,
                            kind = "proposal",
                            action = action.Kind.ToString(),
                            path = action.Path,
                            command = action.Command,
                            summary
                        });

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
                            // à partir de ce que le modèle raconte de lui-même. La note
                            // de conflit ci-dessus n'est PAS littérale (texte fixe +
                            // horodatage calculé ici), pas un risque d'injection.
                            Details = tool.DescribeForConfirmation(action, workspaceRoot) + conflictNote,
                            ConfirmText = "Autoriser",
                            CancelText = "Refuser",
                            IsDestructive = action.Kind == AgentActionKind.RunCommand
                        });

                        auditLog.Append(new
                        {
                            ts = DateTime.UtcNow,
                            agentId = run.AgentId,
                            kind = "decision",
                            approved = confirmation.Confirmed
                        });

                        if (!confirmation.Confirmed)
                        {
                            record.Confirmation = ConfirmationState.Declined;
                            record.ObservationSummary = "Refusé par l'utilisateur.";
                            narrate($"🚫 Étape {step} : refusé.");
                            history.Add((action, "Action refusée par l'utilisateur — choisis une autre approche, ou termine (Finish) si tu ne peux pas continuer."));
                            _messageBus.Post(new AgentMessage
                            {
                                FromAgentId = run.AgentId,
                                Kind = AgentMessageKind.Outcome,
                                Text = "Refusé par l'utilisateur.",
                                TouchedPath = touchedPath
                            });

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

                        string execObservation;
                        try
                        {
                            execObservation = await tool.ExecuteAsync(action, workspaceRoot, run.AgentId, ct);
                        }
                        catch (Exception ex)
                        {
                            execObservation = $"Erreur lors de l'exécution : {ex.Message}";
                        }

                        record.ObservationSummary = execObservation;
                        narrate($"🤖 Étape {step} : {execObservation}");
                        _messageBus.Post(new AgentMessage
                        {
                            FromAgentId = run.AgentId,
                            Kind = AgentMessageKind.Outcome,
                            Text = execObservation,
                            TouchedPath = touchedPath
                        });
                        auditLog.Append(new { ts = DateTime.UtcNow, agentId = run.AgentId, kind = "execution", observation = execObservation });

                        // ★ AJOUT (03/09, filet déterministe — voir commentaire plus haut)
                        var mutationKey = (action.Kind, action.Path);
                        repeatCount = lastSuccessfulMutation.HasValue && lastSuccessfulMutation.Value == mutationKey
                            ? repeatCount + 1
                            : 0;
                        lastSuccessfulMutation = mutationKey;

                        if (repeatCount >= 2)
                        {
                            // 3e même écriture d'affilée (2 répétitions après la 1re) :
                            // le modèle n'a pas suivi la consigne d'arrêt — on arrête
                            // nous-mêmes plutôt que de continuer à solliciter l'humain
                            // pour la même action.
                            history.Add((action, execObservation));
                            run.Status = AgentRunStatus.Completed;
                            narrate($"✅ Agent « {run.AgentId} » : objectif probablement déjà atteint (même action répétée {repeatCount + 1} fois) — arrêt automatique.");
                            return;
                        }

                        history.Add((action, repeatCount == 1
                            ? execObservation + " ⚠ Tu répètes exactement la même action que l'étape précédente. Si l'objectif est atteint, réponds MAINTENANT avec ACTION: Finish."
                            : execObservation));
                        continue;
                    }

                    // Outils non-mutants (ReadFile, SendMessage) : aucune confirmation.
                    string observation;
                    try
                    {
                        observation = await tool.ExecuteAsync(action, workspaceRoot, run.AgentId, ct);
                    }
                    catch (Exception ex)
                    {
                        observation = $"Erreur lors de l'exécution : {ex.Message}";
                    }

                    if (action.Kind == AgentActionKind.SendMessage) sentMessages++;

                    record.ObservationSummary = observation;
                    narrate($"🤖 Étape {step} : {observation}");
                    // ★ AJOUT (05/09, même diagnostic que ci-dessus pour Malformed) :
                    // sans ceci, un ReadFile qui réussit (donc jamais mutant, jamais dans
                    // le journal jusqu'ici) est invisible — impossible de distinguer "le
                    // modèle n'a jamais essayé de lire le fichier" de "il l'a lu puis a
                    // échoué à formuler l'écriture".
                    auditLog.Append(new
                    {
                        ts = DateTime.UtcNow,
                        agentId = run.AgentId,
                        kind = "non_mutating_step",
                        step,
                        action = action.Kind.ToString(),
                        path = action.Path,
                        observation = observation.Length > 300 ? observation.Substring(0, 300) + "…(tronqué)" : observation
                    });

                    // ★ AJOUT (05/09, filet anti-boucle de lecture — voir commentaire
                    // plus haut) : un rappel poli une fois ne suffisait pas forcément
                    // (le modèle peut relire 2, 3 fois de suite) — le message se durcit
                    // à chaque répétition supplémentaire au lieu de rester identique,
                    // pour augmenter la pression au fil des tentatives plutôt que de
                    // répéter un conseil déjà ignoré.
                    var nonMutKey = (action.Kind, action.Path);
                    nonMutatingRepeatCount = lastNonMutatingAction.HasValue && lastNonMutatingAction.Value == nonMutKey
                        ? nonMutatingRepeatCount + 1
                        : 0;
                    lastNonMutatingAction = nonMutKey;

                    var historyObservation = nonMutatingRepeatCount switch
                    {
                        0 => observation,
                        1 => observation + " ⚠ Tu viens DÉJÀ de faire exactement ça à l'étape précédente (même résultat ci-dessus, inutile de relire). Si c'était un ReadFile, passe MAINTENANT à ACTION: WriteFile avec le contenu corrigé sur ce même chemin.",
                        _ => observation + " 🛑 Tu répètes la MÊME action pour la " + (nonMutatingRepeatCount + 1) + "e fois d'affilée sans le moindre progrès. ARRÊTE de relire. Ta TOUTE PROCHAINE réponse doit être ACTION: WriteFile (avec le contenu corrigé) ou ACTION: Finish si tu ne peux pas continuer."
                    };
                    history.Add((action, historyObservation));
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

        /// <summary>★ AJOUT (jalon 3, durcissement) : reconnaît quelques motifs de
        /// commandes réputées risquées, pour les signaler dans la boîte de
        /// confirmation — un avertissement visuel de plus, JAMAIS un blocage
        /// automatique (la décision reste entièrement humaine, comme partout
        /// ailleurs dans ce chantier). Reconnaissance simple par sous-chaîne :
        /// pas exhaustive, ne remplace pas la vigilance de la personne qui clique
        /// "Autoriser".</summary>
        private static string? DangerousCommandHint(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            var lower = command.ToLowerInvariant();

            if (lower.Contains("rm -rf") || lower.Contains("rmdir /s") || lower.Contains("remove-item") ||
                System.Text.RegularExpressions.Regex.IsMatch(lower, @"(^|[\s&|;])del(\s|$)"))
                return "⚠ Cette commande supprime potentiellement des fichiers.";

            if (lower.Contains("git push --force") || lower.Contains("git push -f"))
                return "⚠ Ceci force un push Git — peut écraser l'historique distant.";

            if (lower.Contains("format ") || lower.Contains("diskpart"))
                return "⚠ Cette commande touche au formatage/partitionnement du disque.";

            if (lower.Contains("curl") && (lower.Contains("| sh") || lower.Contains("|sh") || lower.Contains("| bash") || lower.Contains("|bash")))
                return "⚠ Cette commande télécharge et exécute un script distant sans l'inspecter d'abord.";

            return null;
        }

        private static string DefaultSummary(AgentAction action) => action.Kind switch
        {
            AgentActionKind.ReadFile => $"lire {action.Path}",
            AgentActionKind.WriteFile => $"écrire {action.Path}",
            AgentActionKind.RunCommand => $"exécuter : {action.Command}",
            AgentActionKind.SendMessage => $"envoyer un message à {action.ToAgentId ?? "tous"}",
            AgentActionKind.Finish => "terminer",
            _ => "action inconnue"
        };

        private static string BuildPrompt(
            string goal,
            List<(AgentAction Action, string Observation)> history,
            IReadOnlyList<AgentMessage> incomingMessages)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tu es un agent autonome de MOTO Editor. Objectif à accomplir, un pas à la fois :");
            sb.AppendLine(goal);
            sb.AppendLine();
            sb.AppendLine("À CHAQUE réponse, propose UNE SEULE action, dans EXACTEMENT ce format (rien d'autre autour) :");
            sb.AppendLine("ACTION: ReadFile | WriteFile | RunCommand | SendMessage | Finish");
            sb.AppendLine("PATH: chemin/relatif au projet (pour ReadFile et WriteFile uniquement)");
            sb.AppendLine("CONTENT: <<<");
            sb.AppendLine("contenu complet du fichier (pour WriteFile uniquement)");
            sb.AppendLine(">>>");
            sb.AppendLine("COMMAND: commande shell (pour RunCommand uniquement)");
            sb.AppendLine("TO: identifiant de l'agent destinataire (pour SendMessage — laisse vide pour parler à tous)");
            sb.AppendLine("SUMMARY: courte explication en français ; pour SendMessage, c'est le texte du message");
            sb.AppendLine();
            sb.AppendLine("RÈGLES IMPORTANTES :");
            sb.AppendLine("- WriteFile REMPLACE TOUT le contenu du fichier, il n'ajoute rien tout seul. Pour AJOUTER une ligne à un fichier existant sans perdre le reste, utilise D'ABORD ReadFile pour voir le contenu actuel, puis renvoie ce contenu ET ta nouvelle ligne dans le CONTENT de ton WriteFile suivant.");
            sb.AppendLine("- Ne répète JAMAIS la même action que l'étape précédente si elle a réussi. Si le dernier résultat ci-dessous montre un succès et que ça correspond déjà à ton objectif, réponds IMMÉDIATEMENT avec ACTION: Finish — ne recommence pas \"pour vérifier\".");

            if (incomingMessages.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Messages reçus d'autres agents depuis ton dernier tour :");
                foreach (var m in incomingMessages)
                    sb.AppendLine($"- {m.FromAgentId} : {m.Text}");
            }

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
