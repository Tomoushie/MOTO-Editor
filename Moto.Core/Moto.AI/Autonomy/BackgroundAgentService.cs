// Moto.Core/AI/Autonomy/BackgroundAgentService.cs
// Point d'entrée DI — même forme que ChatService.Tasks/RunTrackedAsync : démarre
// un run comme une vraie tâche d'arrière-plan détachée et l'expose tout de
// suite dans une ObservableCollection pour qu'un appelant (glue UI ou, plus
// tard, un panneau dédié) puisse le suivre en direct.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Moto.Core.AI.Autonomy.V2;
using Moto.Core.AI.Internal;
using Moto.Core.Settings;

namespace Moto.Core.AI.Autonomy
{
    public sealed class BackgroundAgentService
    {
        private readonly MotoAiKernel _kernel;
        private readonly AiConfirmationService _confirmation;
        private readonly IReadOnlyList<IAgentTool> _tools;
        private readonly AgentMessageBus _messageBus;
        private readonly AgentGlobalBudget _globalBudget;

        public ObservableCollection<AgentRunRecord> Runs { get; } = new();

        /// <summary>★ AJOUT (jalon 2) : UNE seule instance partagée entre TOUS les
        /// runs (créée une fois en DI, voir MotoServiceCollectionExtensions.cs) —
        /// c'est ce qui permet à deux agents lancés séparément de se voir.</summary>
        public AgentMessageBus MessageBus => _messageBus;

        /// <summary>★ AJOUT (jalon 3) : exposé pour qu'AgentRunsView puisse
        /// afficher la consommation face au plafond, sans dupliquer l'état.</summary>
        public AgentGlobalBudget GlobalBudget => _globalBudget;

        /// <summary>★ AJOUT (24/09, agent v2) : le moteur v2 (appels d'outils, diff, annulation), branché sur les mêmes
        /// AgentRunRecord que la v1. Null = v1 seule (tests, ou éditeur sans v2).</summary>
        private readonly AgentV2Runner? _v2;

        public AgentV2Runner? V2 => _v2;

        public BackgroundAgentService(
            MotoAiKernel kernel,
            AiConfirmationService confirmation,
            IReadOnlyList<IAgentTool> tools,
            AgentMessageBus messageBus,
            AgentGlobalBudget globalBudget,
            AgentV2Runner? v2 = null)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _globalBudget = globalBudget ?? throw new ArgumentNullException(nameof(globalBudget));
            _v2 = v2;
        }

        /// <summary>Vrai quand un nouveau run utilisera le moteur v2 (réglage « agent_engine », « v2 » par défaut).</summary>
        public bool UsesV2 => _v2 is not null && _v2.IsEnabled;

        /// <summary>
        /// Démarre un run et retourne IMMÉDIATEMENT — la boucle continue de
        /// tourner via ses propres `await` (elle rend la main au tout premier),
        /// exactement comme ChatService.RunTrackedAsync (async/await, jamais de
        /// Task.Run). `narrate` est appelé pour chaque pas.
        /// ★ CORRECTIF (jalon 3, trouvé EN CONCEVANT le panneau "Agents en
        /// cours") : la version précédente enveloppait la boucle dans
        /// `Task.Run(...)`, l'exécutant sur un thread d'arrière-plan — sans
        /// interface observant AgentRunRecord, c'était invisible. Un vrai
        /// panneau qui affiche Status/Steps EN DIRECT y aurait planté (ou mal
        /// affiché) : MAUI exige que les objets liés à l'UI soient modifiés sur
        /// le thread UI. En appelant directement RunAsync (sans Task.Run) depuis
        /// Start(), TOUJOURS invoqué depuis le thread UI, ses continuations
        /// `await` reprennent sur le SynchronizationContext de l'UI — même
        /// mécanisme déjà éprouvé pour ChatService.Tasks, aucune nouvelle
        /// gymnastique de marshaling nécessaire.
        /// </summary>
        public AgentRunRecord Start(string agentId, string goal, string workspaceRoot, Action<string> narrate, string? context = null)
        {
            var run = new AgentRunRecord { AgentId = agentId, Goal = goal };
            Runs.Insert(0, run);

            // ★ AJOUT (24/09) : v2 par défaut. Contrairement à la v1 (qui tourne sur le thread UI et dont chaque `await`
            // reprend sur lui), la v2 tourne sur un thread d'arrière-plan et reposte elle-même ses mises à jour sur le
            // thread UI — voir AgentV2Runner. `context` (fichier ouvert…) n'est utilisé que par la v2.
            if (_v2 is not null && _v2.IsEnabled)
            {
                run.Engine = AgentV2Settings.EngineV2;
                _ = _v2.RunAsync(run, goal, workspaceRoot, narrate, context);
                return run;
            }

            var loop = new BackgroundAgentLoop(_kernel, _confirmation, _tools, _messageBus, _globalBudget);
            _ = loop.RunAsync(run, workspaceRoot, narrate);

            return run;
        }

        public void Cancel(Guid runId)
        {
            var run = Runs.FirstOrDefault(r => r.Id == runId);
            run?.Cts.Cancel();
        }

        /// <summary>
        /// ★ AJOUT (24/09) : remet chaque fichier touché par ce run dans l'état d'avant. Retourne les chemins complets concernés
        /// (l'éditeur recharge alors ses onglets, via <see cref="AgentV2Runner.FilesChanged"/>) ; <paramref name="restored"/> = nombre
        /// de fichiers restaurés ou supprimés. À appeler sur le thread UI. Sans effet si le run n'a rien à annuler.
        /// </summary>
        public IReadOnlyList<string> UndoChanges(Guid runId, out int restored)
        {
            restored = 0;
            var run = Runs.FirstOrDefault(r => r.Id == runId);
            if (run is null) return Array.Empty<string>();

            var touched = run.UndoChanges(out restored);
            _v2?.NotifyFilesChanged(touched);
            return touched;
        }
    }
}
