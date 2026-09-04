// Moto.Core/AI/Autonomy/BackgroundAgentService.cs
// Point d'entrée DI — même forme que ChatService.Tasks/RunTrackedAsync : démarre
// un run comme une vraie tâche d'arrière-plan détachée et l'expose tout de
// suite dans une ObservableCollection pour qu'un appelant (glue UI ou, plus
// tard, un panneau dédié) puisse le suivre en direct.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        public BackgroundAgentService(
            MotoAiKernel kernel,
            AiConfirmationService confirmation,
            IReadOnlyList<IAgentTool> tools,
            AgentMessageBus messageBus,
            AgentGlobalBudget globalBudget)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _globalBudget = globalBudget ?? throw new ArgumentNullException(nameof(globalBudget));
        }

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
        public AgentRunRecord Start(string agentId, string goal, string workspaceRoot, Action<string> narrate)
        {
            var run = new AgentRunRecord { AgentId = agentId, Goal = goal };
            Runs.Insert(0, run);

            var loop = new BackgroundAgentLoop(_kernel, _confirmation, _tools, _messageBus, _globalBudget);
            _ = loop.RunAsync(run, workspaceRoot, narrate);

            return run;
        }

        public void Cancel(Guid runId)
        {
            var run = Runs.FirstOrDefault(r => r.Id == runId);
            run?.Cts.Cancel();
        }
    }
}
