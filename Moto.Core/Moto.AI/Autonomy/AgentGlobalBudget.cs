// Moto.Core/AI/Autonomy/AgentGlobalBudget.cs
// ★ AJOUT (jalon 3) : plafond de pas IA partagé entre TOUS les runs enregistrés
// pendant la durée de vie de l'application — distinct du maxSteps de
// BackgroundAgentLoop, qui borne UN SEUL run. Un agent sagement sous son
// propre plafond individuel pourrait quand même, cumulé avec d'autres agents
// lancés au fil d'une longue session, consommer un nombre d'appels IA sans
// limite. Singleton en DI (voir MotoServiceCollectionExtensions.cs), partagé
// entre toutes les instances de BackgroundAgentLoop — thread-safe (Interlocked),
// puisque plusieurs loops peuvent y toucher en même temps.
using System.Threading;

namespace Moto.Core.AI.Autonomy
{
    public sealed class AgentGlobalBudget
    {
        private const int MaxStepsPerAppLifetime = 200;
        private int _consumed;

        public int Consumed => _consumed;
        public int Limit => MaxStepsPerAppLifetime;

        /// <summary>Tente de consommer un pas du budget global. Retourne false une
        /// fois le plafond atteint — l'appelant (BackgroundAgentLoop) doit alors
        /// arrêter proprement plutôt que de continuer à solliciter l'IA.</summary>
        public bool TryConsume()
        {
            while (true)
            {
                var current = _consumed;
                if (current >= MaxStepsPerAppLifetime) return false;
                if (Interlocked.CompareExchange(ref _consumed, current + 1, current) == current)
                    return true;
            }
        }
    }
}
