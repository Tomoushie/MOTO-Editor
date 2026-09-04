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

        public ObservableCollection<AgentRunRecord> Runs { get; } = new();

        /// <summary>★ AJOUT (jalon 2) : UNE seule instance partagée entre TOUS les
        /// runs (créée une fois en DI, voir MotoServiceCollectionExtensions.cs) —
        /// c'est ce qui permet à deux agents lancés séparément de se voir.</summary>
        public AgentMessageBus MessageBus => _messageBus;

        public BackgroundAgentService(
            MotoAiKernel kernel,
            AiConfirmationService confirmation,
            IReadOnlyList<IAgentTool> tools,
            AgentMessageBus messageBus)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// Démarre un run et retourne IMMÉDIATEMENT (le travail réel tourne sur une
        /// tâche détachée) — même patron que ChatService.RunTrackedAsync, qui lui
        /// attend sa tâche : ici on ne l'attend PAS, puisque tout l'intérêt est de
        /// pouvoir continuer à utiliser MOTO Editor pendant que l'agent travaille.
        /// `narrate` est appelé pour chaque pas — jamais depuis le thread UI :
        /// à l'appelant de marshaler si besoin (voir MainPage.Extensions.cs).
        /// </summary>
        public AgentRunRecord Start(string agentId, string goal, string workspaceRoot, Action<string> narrate)
        {
            var run = new AgentRunRecord { AgentId = agentId, Goal = goal };
            Runs.Insert(0, run);

            var loop = new BackgroundAgentLoop(_kernel, _confirmation, _tools, _messageBus);
            _ = System.Threading.Tasks.Task.Run(() => loop.RunAsync(run, workspaceRoot, narrate));

            return run;
        }

        public void Cancel(Guid runId)
        {
            var run = Runs.FirstOrDefault(r => r.Id == runId);
            run?.Cts.Cancel();
        }
    }
}
